namespace ElBruno.ModelContextProtocol.MCPToolRouter;

/// <summary>
/// Status information about the embedding model configuration and availability.
/// </summary>
public sealed class EmbeddingModelStatus
{
    /// <summary>
    /// The HuggingFace model name.
    /// </summary>
    public required string ModelName { get; init; }

    /// <summary>
    /// The resolved cache directory path where the model is (or would be) stored.
    /// </summary>
    public required string CacheDirectory { get; init; }

    /// <summary>
    /// Whether the model files appear to be already downloaded at the cache location.
    /// </summary>
    public required bool IsDownloaded { get; init; }

    /// <summary>
    /// Whether the quantized (INT8) model variant is preferred.
    /// </summary>
    public required bool PreferQuantized { get; init; }
}
