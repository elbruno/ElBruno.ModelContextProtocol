using ModelContextProtocol.Protocol;

namespace ElBruno.ModelContextProtocol.MCPToolRouter;

/// <summary>
/// Represents a tool matched by semantic search, with its relevance score.
/// </summary>
public sealed class ToolSearchResult
{
    /// <summary>
    /// The matched MCP tool definition.
    /// </summary>
    public required Tool Tool { get; init; }

    /// <summary>
    /// Cosine similarity score between the search prompt and the tool's embedding (0.0 to 1.0, higher is more relevant).
    /// </summary>
    public required float Score { get; init; }
}
