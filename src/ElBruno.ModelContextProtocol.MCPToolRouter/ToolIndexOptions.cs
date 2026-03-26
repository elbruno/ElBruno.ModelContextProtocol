using ElBruno.LocalEmbeddings.Options;
using Microsoft.Extensions.Logging;

namespace ElBruno.ModelContextProtocol.MCPToolRouter;

/// <summary>
/// Configuration options for <see cref="ToolIndex"/>.
/// </summary>
public sealed class ToolIndexOptions
{
    /// <summary>
    /// Template for embedding text. Use {Name} and {Description} placeholders.
    /// </summary>
    public string EmbeddingTextTemplate { get; set; } = "{Name}: {Description}";

    /// <summary>
    /// Size of the LRU cache for query embeddings. 0 = disabled.
    /// </summary>
    public int QueryCacheSize { get; set; } = 0;

    /// <summary>
    /// Optional logger for diagnostic output.
    /// </summary>
    public ILogger? Logger { get; set; }

    /// <summary>
    /// Options for the local embedding generator (used when no custom IEmbeddingGenerator is provided).
    /// </summary>
    public LocalEmbeddingsOptions? EmbeddingOptions { get; set; }
}
