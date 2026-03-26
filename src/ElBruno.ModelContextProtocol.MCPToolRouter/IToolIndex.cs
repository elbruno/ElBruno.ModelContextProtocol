using ModelContextProtocol.Protocol;

namespace ElBruno.ModelContextProtocol.MCPToolRouter;

/// <summary>
/// Abstraction for a semantic tool index supporting search, mutation, and serialization.
/// </summary>
public interface IToolIndex : IAsyncDisposable
{
    /// <summary>
    /// Gets the total number of indexed tools.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Searches for the most relevant tools for the given prompt.
    /// </summary>
    /// <param name="prompt">The search query or user prompt.</param>
    /// <param name="topK">Maximum number of results to return. Default is 5.</param>
    /// <param name="minScore">Minimum cosine similarity score (0.0 to 1.0). Default is 0.0.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of matching tools sorted by relevance (highest score first).</returns>
    Task<IReadOnlyList<ToolSearchResult>> SearchAsync(string prompt, int topK = 5, float minScore = 0.0f, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds new tools to the index, generating embeddings for them.
    /// </summary>
    /// <param name="tools">The MCP tool definitions to add.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task AddToolsAsync(IEnumerable<Tool> tools, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes tools by name from the index.
    /// </summary>
    /// <param name="toolNames">The names of the tools to remove.</param>
    void RemoveTools(IEnumerable<string> toolNames);

    /// <summary>
    /// Saves the index (tool metadata + embeddings) to a stream.
    /// </summary>
    /// <param name="stream">The target stream.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SaveAsync(Stream stream, CancellationToken cancellationToken = default);
}
