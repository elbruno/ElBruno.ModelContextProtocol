using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ElBruno.ModelContextProtocol.MCPToolRouter;

/// <summary>
/// Configuration options for <see cref="PromptDistiller"/>.
/// </summary>
public sealed class PromptDistillerOptions
{
    /// <summary>
    /// The system prompt used to instruct the LLM on how to distill user intent.
    /// The default prompt extracts comma-separated action phrases with technical vocabulary,
    /// optimized for embedding-based cosine similarity against tool descriptions.
    /// </summary>
    public string SystemPrompt { get; set; } = PromptDistiller.DefaultSystemPrompt;

    /// <summary>
    /// Maximum number of output tokens for the distillation response. Default is 384.
    /// Note: for local ONNX models this maps to max_length (total sequence length including
    /// system + user tokens), so the actual output budget is MaxOutputTokens minus input tokens.
    /// </summary>
    public int MaxOutputTokens { get; set; } = 384;

    /// <summary>
    /// Temperature for the distillation LLM call. Default is 0.1 (near-deterministic with slight
    /// diversity to avoid repetition loops on small models).
    /// </summary>
    public float Temperature { get; set; } = 0.1f;

    /// <summary>
    /// Maximum character length for prompts sent to the LLM for distillation.
    /// Prompts exceeding this length are truncated. Default is 500 (suitable for local ONNX models).
    /// Set to 0 or negative to disable truncation.
    /// </summary>
    public int MaxPromptLength { get; set; } = 500;
}

/// <summary>
/// Static helper that uses an <see cref="IChatClient"/> to distill complex user prompts
/// into keyword-rich comma-separated action phrases for better semantic tool matching.
/// Includes post-processing to clean up degenerate LLM output (repetitions, duplicates).
/// </summary>
public static partial class PromptDistiller
{
    /// <summary>
    /// The default system prompt used when no <see cref="PromptDistillerOptions"/> is provided.
    /// Produces comma-separated action phrases with technical vocabulary, optimized for
    /// embedding-based cosine similarity against tool descriptions.
    /// </summary>
    internal const string DefaultSystemPrompt =
        "Extract the key tasks from the user's message as a comma-separated list of specific action phrases (2-5 words each). " +
        "Cover all distinct topics mentioned. Use technical terms that match what software tools do. " +
        "Example: \"check database health, deploy container, rotate API credentials, generate usage report\" " +
        "Output ONLY the comma-separated list — no preamble, no numbering.";

    /// <summary>
    /// Minimum length a distilled result must have to be considered valid.
    /// Results shorter than this fall back to the original prompt.
    /// </summary>
    private const int MinDistilledLength = 5;

    /// <summary>
    /// Distills a complex user prompt into a single-sentence intent using an LLM.
    /// Falls back to the original prompt if the distillation result is empty or too short.
    /// </summary>
    /// <param name="client">The chat client used for LLM inference.</param>
    /// <param name="userPrompt">The user's original prompt to distill.</param>
    /// <param name="options">Optional configuration for the distillation call. If null, uses defaults.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The distilled single-sentence intent, or the original prompt if distillation fails.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="client"/> or <paramref name="userPrompt"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="userPrompt"/> is empty or whitespace.</exception>
    public static async Task<string> DistillIntentAsync(
        IChatClient client,
        string userPrompt,
        PromptDistillerOptions? options = null,
        CancellationToken ct = default)
    {
        return await DistillIntentAsync(client, userPrompt, options, NullLogger.Instance, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Distills a complex user prompt into a single-sentence intent using an LLM.
    /// Falls back to the original prompt if the distillation result is empty or too short.
    /// </summary>
    /// <param name="client">The chat client used for LLM inference.</param>
    /// <param name="userPrompt">The user's original prompt to distill.</param>
    /// <param name="options">Optional configuration for the distillation call. If null, uses defaults.</param>
    /// <param name="logger">Logger for diagnostics. If null, no logging occurs.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The distilled single-sentence intent, or the original prompt if distillation fails.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="client"/> or <paramref name="userPrompt"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="userPrompt"/> is empty or whitespace.</exception>
    public static async Task<string> DistillIntentAsync(
        IChatClient client,
        string userPrompt,
        PromptDistillerOptions? options,
        ILogger logger,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentException.ThrowIfNullOrWhiteSpace(userPrompt);

        options ??= new PromptDistillerOptions();
        logger ??= NullLogger.Instance;

        if (options.MaxPromptLength > 0 && userPrompt.Length > options.MaxPromptLength)
        {
            var originalLength = userPrompt.Length;
            userPrompt = userPrompt[..options.MaxPromptLength];
            LogMessages.PromptTruncated(logger, originalLength, options.MaxPromptLength);
        }

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, options.SystemPrompt),
            new(ChatRole.User, userPrompt)
        };

        var chatOptions = new ChatOptions
        {
            MaxOutputTokens = options.MaxOutputTokens,
            Temperature = options.Temperature
        };

        try
        {
            var response = await client.GetResponseAsync(messages, chatOptions, ct).ConfigureAwait(false);
            var distilled = response.Text?.Trim() ?? userPrompt;

            if (distilled.Length < MinDistilledLength)
                return userPrompt;

            // Post-process to clean up degenerate LLM output (repeated words, duplicate phrases)
            distilled = PostProcessDistilledOutput(distilled);
            return distilled.Length < MinDistilledLength ? userPrompt : distilled;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogMessages.DistillationFailed(logger, ex.Message);
            return userPrompt;
        }
    }

    /// <summary>
    /// Cleans up degenerate LLM output that small models often produce:
    /// removes trailing word repetitions, deduplicates phrases, and strips noise.
    /// </summary>
    internal static string PostProcessDistilledOutput(string distilled)
    {
        if (string.IsNullOrWhiteSpace(distilled))
            return distilled;

        // Split by commas and clean each phrase
        var phrases = distilled.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var cleaned = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var phrase in phrases)
        {
            var p = RemoveTrailingRepetition(phrase).Trim();
            if (p.Length < 3) continue; // skip degenerate single-word/letter fragments
            if (!seen.Add(p)) continue; // skip exact duplicates
            cleaned.Add(p);
        }

        return cleaned.Count > 0 ? string.Join(", ", cleaned) : distilled;
    }

    /// <summary>
    /// Removes trailing word repetitions caused by ONNX model degeneration
    /// (e.g., "check transactions transactions" → "check transactions").
    /// </summary>
    private static string RemoveTrailingRepetition(string phrase)
    {
        var words = phrase.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length < 2) return phrase;

        // Remove repeated trailing words (e.g., "word word" or "word word word")
        var lastWord = words[^1];
        var trimIndex = words.Length;
        for (int i = words.Length - 2; i >= 0; i--)
        {
            if (string.Equals(words[i], lastWord, StringComparison.OrdinalIgnoreCase))
                trimIndex = i + 1;
            else
                break;
        }

        if (trimIndex < words.Length)
            return string.Join(' ', words[..trimIndex]);

        return phrase;
    }

    #region High-Performance Logging

    private static partial class LogMessages
    {
        [LoggerMessage(EventId = 200, Level = LogLevel.Warning, Message = "User prompt truncated from {OriginalLength} to {MaxLength} characters before distillation")]
        public static partial void PromptTruncated(ILogger logger, int originalLength, int maxLength);

        [LoggerMessage(EventId = 201, Level = LogLevel.Warning, Message = "LLM distillation failed, falling back to original prompt: {ErrorMessage}")]
        public static partial void DistillationFailed(ILogger logger, string errorMessage);


    }

    #endregion
}
