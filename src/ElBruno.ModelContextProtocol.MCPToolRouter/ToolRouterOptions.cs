namespace ElBruno.ModelContextProtocol.MCPToolRouter;

/// <summary>
/// Configuration options for <see cref="ToolRouter"/>.
/// Combines routing parameters, prompt distillation settings, and underlying index options.
/// </summary>
public sealed class ToolRouterOptions
{
    /// <summary>
    /// Maximum number of tools to return from a routing query. Default is 5.
    /// </summary>
    public int TopK { get; set; } = 5;

    /// <summary>
    /// Minimum cosine similarity score (0.0 to 1.0) for a tool to be included in results. Default is 0.0.
    /// </summary>
    public float MinScore { get; set; } = 0.0f;

    /// <summary>
    /// When true, user prompts are distilled into keyword-rich action phrases via LLM before semantic search.
    /// Uses hybrid search: combines baseline (Mode 1) results with multi-query phrase results.
    /// Requires an <see cref="Microsoft.Extensions.AI.IChatClient"/> to be provided. Default is true.
    /// </summary>
    public bool EnableDistillation { get; set; } = true;

    /// <summary>
    /// The system prompt used to instruct the LLM on how to distill user intent.
    /// The default prompt extracts comma-separated action phrases with technical vocabulary,
    /// optimized for embedding-based cosine similarity against tool descriptions.
    /// Only used when <see cref="EnableDistillation"/> is true.
    /// </summary>
    public string DistillationSystemPrompt { get; set; } = PromptDistiller.DefaultSystemPrompt;

    /// <summary>
    /// Maximum number of output tokens for the distillation response. Default is 384.
    /// Note: for local ONNX models this maps to max_length (total sequence length including
    /// system + user tokens), so the actual output budget is MaxOutputTokens minus input tokens.
    /// Only used when <see cref="EnableDistillation"/> is true.
    /// </summary>
    public int DistillationMaxOutputTokens { get; set; } = 384;

    /// <summary>
    /// Temperature for the distillation LLM call. Default is 0.1 (near-deterministic with slight
    /// diversity to avoid repetition loops on small models).
    /// Only used when <see cref="EnableDistillation"/> is true.
    /// </summary>
    public float DistillationTemperature { get; set; } = 0.1f;

    /// <summary>
    /// Maximum character length for prompts sent to the LLM for distillation.
    /// Prompts exceeding this length are truncated. Default is 500 (suitable for local ONNX models).
    /// Increase this value when using cloud LLMs with larger context windows (e.g., 4096 or higher).
    /// Set to 0 or negative to disable truncation.
    /// Only used when <see cref="EnableDistillation"/> is true.
    /// </summary>
    public int MaxPromptLength { get; set; } = 500;

    /// <summary>
    /// Convenience property: sets the embedding model cache directory.
    /// Shorthand for <c>IndexOptions.EmbeddingOptions.CacheDirectory</c>.
    /// When set, this value takes precedence over any cache directory specified in
    /// <see cref="IndexOptions"/>.<see cref="ToolIndexOptions.EmbeddingOptions"/>.
    /// </summary>
    public string? EmbeddingModelCacheDirectory { get; set; }

    /// <summary>
    /// The local LLM model to use for prompt distillation when no IChatClient is provided.
    /// Only used by the zero-setup SearchUsingLLMAsync overload.
    /// Default uses ElBruno.LocalLLMs default model.
    /// </summary>
    public ElBruno.LocalLLMs.ModelDefinition? LocalLLMModel { get; set; }

    /// <summary>
    /// When true (default), static API methods share a process-level embedding generator
    /// and chat client for better performance on repeated calls.
    /// When false, static API methods create fresh resources per call (slower but isolated).
    /// </summary>
    public bool UseSharedResources { get; set; } = true;

    /// <summary>
    /// Options for the underlying <see cref="ToolIndex"/>. If null, uses defaults.
    /// </summary>
    public ToolIndexOptions? IndexOptions { get; set; }

    /// <summary>
    /// Creates a <see cref="PromptDistillerOptions"/> instance from the distillation settings in this options object.
    /// </summary>
    internal PromptDistillerOptions ToDistillerOptions() => new()
    {
        SystemPrompt = DistillationSystemPrompt,
        MaxOutputTokens = DistillationMaxOutputTokens,
        Temperature = DistillationTemperature,
        MaxPromptLength = MaxPromptLength
    };
}
