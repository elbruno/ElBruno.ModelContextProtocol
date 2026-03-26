using ElBruno.LocalEmbeddings;
using ElBruno.LocalEmbeddings.Options;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Protocol;

namespace ElBruno.ModelContextProtocol.MCPToolRouter;

/// <summary>
/// Indexes MCP tool definitions for semantic search using local embeddings.
/// </summary>
public sealed class ToolIndex : IAsyncDisposable
{
    private readonly LocalEmbeddingGenerator _embeddingGenerator;
    private readonly Tool[] _tools;
    private readonly Embedding<float>[] _embeddings;

    private ToolIndex(LocalEmbeddingGenerator embeddingGenerator, Tool[] tools, Embedding<float>[] embeddings)
    {
        _embeddingGenerator = embeddingGenerator;
        _tools = tools;
        _embeddings = embeddings;
    }

    /// <summary>
    /// Creates a ToolIndex from a collection of MCP tool definitions.
    /// Generates embeddings for each tool's name and description.
    /// </summary>
    /// <param name="tools">The MCP tool definitions to index.</param>
    /// <param name="embeddingOptions">Options for the local embedding generator. If null, uses defaults.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A new ToolIndex instance.</returns>
    public static async Task<ToolIndex> CreateAsync(
        IEnumerable<Tool> tools,
        LocalEmbeddingsOptions? embeddingOptions = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tools);

        var toolArray = tools.ToArray();
        if (toolArray.Length == 0)
        {
            throw new ArgumentException("At least one tool must be provided.", nameof(tools));
        }

        var generator = embeddingOptions is not null
            ? await LocalEmbeddingGenerator.CreateAsync(embeddingOptions, cancellationToken: cancellationToken)
            : await LocalEmbeddingGenerator.CreateAsync(cancellationToken);

        try
        {
            var textsToEmbed = toolArray
                .Select(tool => $"{tool.Name}: {tool.Description ?? string.Empty}")
                .ToArray();

            var embeddings = await generator.GenerateAsync(textsToEmbed, cancellationToken: cancellationToken);
            var embeddingArray = embeddings.ToArray();

            return new ToolIndex(generator, toolArray, embeddingArray);
        }
        catch
        {
            await generator.DisposeAsync();
            throw;
        }
    }

    /// <summary>
    /// Searches for the most relevant tools for the given prompt.
    /// </summary>
    /// <param name="prompt">The search query or user prompt.</param>
    /// <param name="topK">Maximum number of results to return. Default is 5.</param>
    /// <param name="minScore">Minimum cosine similarity score (0.0 to 1.0). Results below this threshold are excluded. Default is 0.0.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of matching tools sorted by relevance (highest score first).</returns>
    public async Task<IReadOnlyList<ToolSearchResult>> SearchAsync(
        string prompt,
        int topK = 5,
        float minScore = 0.0f,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(topK);
        ArgumentOutOfRangeException.ThrowIfNegative(minScore);

        var queryEmbedding = await _embeddingGenerator.GenerateEmbeddingAsync(prompt, cancellationToken: cancellationToken);

        var results = new List<(int Index, float Score)>(_tools.Length);

        for (int i = 0; i < _tools.Length; i++)
        {
            var similarity = CosineSimilarity(queryEmbedding.Vector.ToArray(), _embeddings[i].Vector.ToArray());
            if (similarity >= minScore)
            {
                results.Add((i, similarity));
            }
        }

        results.Sort((a, b) => b.Score.CompareTo(a.Score));

        var topResults = results
            .Take(topK)
            .Select(r => new ToolSearchResult
            {
                Tool = _tools[r.Index],
                Score = r.Score
            })
            .ToList();

        return topResults;
    }

    /// <summary>
    /// Gets the total number of indexed tools.
    /// </summary>
    public int Count => _tools.Length;

    /// <summary>
    /// Disposes the embedding generator.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await _embeddingGenerator.DisposeAsync();
    }

    private static float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length)
        {
            throw new ArgumentException("Vectors must have the same length");
        }

        float dotProduct = 0;
        float magnitudeA = 0;
        float magnitudeB = 0;

        for (int i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            magnitudeA += a[i] * a[i];
            magnitudeB += b[i] * b[i];
        }

        var magnitude = (float)(Math.Sqrt(magnitudeA) * Math.Sqrt(magnitudeB));
        return magnitude == 0 ? 0 : dotProduct / magnitude;
    }
}
