using Microsoft.Extensions.AI;

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
}

/// <summary>
/// Static helper that uses an <see cref="IChatClient"/> to distill complex user prompts
/// into single-sentence intents for better semantic tool matching.
/// </summary>
public static class PromptDistiller
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
        ArgumentNullException.ThrowIfNull(client);
        ArgumentException.ThrowIfNullOrWhiteSpace(userPrompt);

        options ??= new PromptDistillerOptions();

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

        var response = await client.GetResponseAsync(messages, chatOptions, ct).ConfigureAwait(false);
        var distilled = response.Text?.Trim() ?? userPrompt;

        return distilled.Length < MinDistilledLength ? userPrompt : distilled;
    }
}
