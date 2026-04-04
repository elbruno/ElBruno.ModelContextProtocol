using ElBruno.LocalEmbeddings.Options;
using Microsoft.Extensions.Logging;

namespace ElBruno.ModelContextProtocol.MCPToolRouter;

/// <summary>
/// Configuration options for <see cref="ToolIndex"/>.
/// </summary>
public sealed class ToolIndexOptions
{
    /// <summary>
    /// Template for embedding text. Supported placeholders:
    /// <list type="bullet">
    ///   <item><c>{Name}</c> — tool name</item>
    ///   <item><c>{Description}</c> — tool description</item>
    ///   <item><c>{Parameters}</c> — formatted parameter list extracted from InputSchema (e.g. "a (number) - First number, b (number) - Second number")</item>
    ///   <item><c>{InputSchema}</c> — raw JSON InputSchema string</item>
    /// </list>
    /// </summary>
    public string EmbeddingTextTemplate { get; set; } = "{Name}: {Description}. Parameters: {Parameters}";

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
