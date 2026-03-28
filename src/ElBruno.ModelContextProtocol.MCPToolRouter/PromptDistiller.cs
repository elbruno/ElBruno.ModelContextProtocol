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
    /// </summary>
    public string SystemPrompt { get; set; } =
        "Extract the user's primary intent in a single sentence. " +
        "Be specific about what action or information is requested. " +
        "Do not add any explanation or commentary — output only the distilled sentence.";

    /// <summary>
    /// Maximum number of output tokens for the distillation response. Default is 128.
    /// </summary>
    public int MaxOutputTokens { get; set; } = 128;

    /// <summary>
    /// Temperature for the distillation LLM call. Lower values produce more deterministic output. Default is 0.1.
    /// </summary>
    public float Temperature { get; set; } = 0.1f;

    /// <summary>
    /// Maximum character length for prompts sent to the LLM for distillation.
    /// Prompts exceeding this length are truncated. Default is 4096.
    /// Set to 0 or negative to disable truncation.
    /// </summary>
    public int MaxPromptLength { get; set; } = 4096;

    /// <summary>
    /// Maximum sequence length (context window) of the LLM model, in tokens.
    /// When set, the effective prompt length limit is automatically computed
    /// to fit within the model's context window, accounting for system prompt
    /// and output token reservation. Takes precedence over MaxPromptLength when
    /// the computed safe length is smaller. Defaults to null (use MaxPromptLength as-is).
    /// </summary>
    public int? ModelMaxSequenceLength { get; set; }
}

/// <summary>
/// Static helper that uses an <see cref="IChatClient"/> to distill complex user prompts
/// into single-sentence intents for better semantic tool matching.
/// </summary>
public static partial class PromptDistiller
{
    /// <summary>
    /// The default system prompt used when no <see cref="PromptDistillerOptions"/> is provided.
    /// </summary>
    internal const string DefaultSystemPrompt =
        "Extract the user's primary intent in a single sentence. " +
        "Be specific about what action or information is requested. " +
        "Do not add any explanation or commentary — output only the distilled sentence.";

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

        // Auto-compute effective max prompt length from model metadata
        var maxPromptLength = options.MaxPromptLength;
        if (options.ModelMaxSequenceLength is > 0)
        {
            const int reservedTokens = 70; // ~40 system prompt + ~30 output buffer
            var availableTokens = Math.Max(1, options.ModelMaxSequenceLength.Value - reservedTokens);
            var safeChars = availableTokens * 4; // ~4 chars per token estimate
            if (maxPromptLength <= 0 || safeChars < maxPromptLength)
            {
                maxPromptLength = safeChars;
                LogMessages.AutoConfiguredFromModelMetadata(logger, maxPromptLength, options.ModelMaxSequenceLength.Value);
            }
        }

        if (maxPromptLength > 0 && userPrompt.Length > maxPromptLength)
        {
            var originalLength = userPrompt.Length;
            userPrompt = userPrompt[..maxPromptLength];
            LogMessages.PromptTruncated(logger, originalLength, maxPromptLength);
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

    #region High-Performance Logging

    private static partial class LogMessages
    {
        [LoggerMessage(EventId = 200, Level = LogLevel.Warning, Message = "User prompt truncated from {OriginalLength} to {MaxLength} characters before distillation")]
        public static partial void PromptTruncated(ILogger logger, int originalLength, int maxLength);

        [LoggerMessage(EventId = 201, Level = LogLevel.Warning, Message = "LLM distillation failed, falling back to original prompt: {ErrorMessage}")]
        public static partial void DistillationFailed(ILogger logger, string errorMessage);

        [LoggerMessage(EventId = 202, Level = LogLevel.Debug, Message = "Auto-configured max prompt length to {MaxLength} chars from model context window of {MaxSequenceLength} tokens")]
        public static partial void AutoConfiguredFromModelMetadata(ILogger logger, int maxLength, int maxSequenceLength);
    }

    #endregion
}
