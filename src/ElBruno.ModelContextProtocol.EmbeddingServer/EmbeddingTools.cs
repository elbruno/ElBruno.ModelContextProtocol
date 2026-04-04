using System.Collections.Concurrent;
using System.Text.Json;
using ElBruno.LocalEmbeddings;
using ElBruno.LocalEmbeddings.Options;
using ElBruno.ModelContextProtocol.MCPToolRouter;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace ElBruno.ModelContextProtocol.EmbeddingServer;

/// <summary>
/// MCP server tools for local embedding generation and semantic search.
/// </summary>
[McpServerToolType]
public static class EmbeddingTools
{
    private static readonly ConcurrentDictionary<string, IToolIndex> s_indexes = new(StringComparer.OrdinalIgnoreCase);

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Generate embeddings for one or more text inputs using a local ONNX model.
    /// </summary>
    /// <param name="text">The text to generate embeddings for. Separate multiple texts with newlines for batch processing.</param>
    /// <param name="modelName">Optional HuggingFace model name. Defaults to sentence-transformers/all-MiniLM-L6-v2.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>JSON containing the embedding vectors.</returns>
    [McpServerTool(Name = "embed_text"), Description("Generate embeddings for text input using a local ONNX model.")]
    public static async Task<string> EmbedText(
        string text,
        string? modelName = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return JsonSerializer.Serialize(new { error = "Text input must not be empty." }, s_jsonOptions);
        }

        var embeddingOptions = BuildEmbeddingOptions(modelName);
        await using var generator = embeddingOptions is not null
            ? await LocalEmbeddingGenerator.CreateAsync(embeddingOptions, cancellationToken: cancellationToken).ConfigureAwait(false)
            : await LocalEmbeddingGenerator.CreateAsync(cancellationToken).ConfigureAwait(false);

        // Support batch input by splitting on newlines
        var texts = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (texts.Length == 0)
        {
            return JsonSerializer.Serialize(new { error = "Text input must not be empty." }, s_jsonOptions);
        }

        if (texts.Length == 1)
        {
            var embedding = await generator.GenerateEmbeddingAsync(texts[0], cancellationToken: cancellationToken).ConfigureAwait(false);
            return JsonSerializer.Serialize(new
            {
                text = texts[0],
                dimensions = embedding.Vector.Length,
                embedding = embedding.Vector.ToArray()
            }, s_jsonOptions);
        }

        var embeddings = await generator.GenerateAsync(texts, cancellationToken: cancellationToken).ConfigureAwait(false);
        var results = embeddings.Select((e, i) => new
        {
            text = texts[i],
            dimensions = e.Vector.Length,
            embedding = e.Vector.ToArray()
        }).ToArray();

        return JsonSerializer.Serialize(new { count = results.Length, results }, s_jsonOptions);
    }

    /// <summary>
    /// Perform semantic search over an indexed collection of tools.
    /// </summary>
    /// <param name="query">The search query.</param>
    /// <param name="topK">Maximum number of results to return. Defaults to 5.</param>
    /// <param name="indexName">Name of the index to search. Defaults to "default".</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>JSON containing ranked results with similarity scores.</returns>
    [McpServerTool(Name = "search_embeddings"), Description("Semantic search over an indexed collection using embeddings.")]
    public static async Task<string> SearchEmbeddings(
        string query,
        int topK = 5,
        string? indexName = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return JsonSerializer.Serialize(new { error = "Query must not be empty." }, s_jsonOptions);
        }

        var name = indexName ?? "default";
        if (!s_indexes.TryGetValue(name, out var index))
        {
            return JsonSerializer.Serialize(new { error = $"Index '{name}' not found. Use manage_index to create one first." }, s_jsonOptions);
        }

        if (index.Count == 0)
        {
            return JsonSerializer.Serialize(new { error = $"Index '{name}' is empty. Add tools before searching." }, s_jsonOptions);
        }

        var results = await index.SearchAsync(query, topK, cancellationToken: cancellationToken).ConfigureAwait(false);

        return JsonSerializer.Serialize(new
        {
            indexName = name,
            query,
            count = results.Count,
            results = results.Select(r => new
            {
                toolName = r.Tool.Name,
                description = r.Tool.Description,
                score = r.Score
            })
        }, s_jsonOptions);
    }

    /// <summary>
    /// Check the availability and status of the local embedding model.
    /// </summary>
    /// <param name="modelName">Optional HuggingFace model name to check. Defaults to the built-in model.</param>
    /// <returns>JSON containing model status information.</returns>
    [McpServerTool(Name = "get_embedding_model_status"), Description("Check embedding model availability, cache info, and download status.")]
    public static string GetEmbeddingModelStatus(string? modelName = null)
    {
        LocalEmbeddingsOptions? options = null;
        if (!string.IsNullOrWhiteSpace(modelName))
        {
            options = new LocalEmbeddingsOptions { ModelName = modelName };
        }

        var status = EmbeddingModelInfo.GetStatus(options);

        return JsonSerializer.Serialize(new
        {
            modelName = status.ModelName,
            cacheDirectory = status.CacheDirectory,
            isDownloaded = status.IsDownloaded,
            preferQuantized = status.PreferQuantized,
            provider = "ElBruno.LocalEmbeddings (ONNX Runtime)"
        }, s_jsonOptions);
    }

    /// <summary>
    /// Manage embedding index lifecycle: create, save, load, or clear.
    /// </summary>
    /// <param name="action">The action to perform: create, save, load, or clear.</param>
    /// <param name="indexName">Name of the index to manage.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>JSON containing confirmation and index metadata.</returns>
    [McpServerTool(Name = "manage_index"), Description("Create, save, load, or clear an embedding index.")]
    public static async Task<string> ManageIndex(
        string action,
        string indexName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            return JsonSerializer.Serialize(new { error = "Action must not be empty. Valid actions: create, save, load, clear." }, s_jsonOptions);
        }

        if (string.IsNullOrWhiteSpace(indexName))
        {
            return JsonSerializer.Serialize(new { error = "Index name must not be empty." }, s_jsonOptions);
        }

        return action.Trim().ToLowerInvariant() switch
        {
            "create" => await CreateIndexAsync(indexName, cancellationToken).ConfigureAwait(false),
            "save" => await SaveIndexAsync(indexName, cancellationToken).ConfigureAwait(false),
            "load" => await LoadIndexAsync(indexName, cancellationToken).ConfigureAwait(false),
            "clear" => ClearIndex(indexName),
            _ => JsonSerializer.Serialize(new { error = $"Unknown action '{action}'. Valid actions: create, save, load, clear." }, s_jsonOptions)
        };
    }

    #region Internal helpers for testing

    internal static ConcurrentDictionary<string, IToolIndex> Indexes => s_indexes;

    internal static void ResetState()
    {
        foreach (var kvp in s_indexes)
        {
            if (kvp.Value is IAsyncDisposable asyncDisposable)
            {
                asyncDisposable.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
        }

        s_indexes.Clear();
    }

    internal static void RegisterIndex(string name, IToolIndex index)
    {
        s_indexes[name] = index;
    }

    #endregion

    #region Private Helpers

    private static LocalEmbeddingsOptions? BuildEmbeddingOptions(string? modelName)
    {
        if (string.IsNullOrWhiteSpace(modelName))
        {
            return null;
        }

        return new LocalEmbeddingsOptions { ModelName = modelName };
    }

    private static async Task<string> CreateIndexAsync(string indexName, CancellationToken cancellationToken)
    {
        if (s_indexes.ContainsKey(indexName))
        {
            return JsonSerializer.Serialize(new
            {
                action = "create",
                indexName,
                status = "already_exists",
                toolCount = s_indexes[indexName].Count
            }, s_jsonOptions);
        }

        var index = await ToolIndex.CreateEmptyAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        s_indexes[indexName] = index;

        return JsonSerializer.Serialize(new
        {
            action = "create",
            indexName,
            status = "created",
            toolCount = 0
        }, s_jsonOptions);
    }

    private static async Task<string> SaveIndexAsync(string indexName, CancellationToken cancellationToken)
    {
        if (!s_indexes.TryGetValue(indexName, out var index))
        {
            return JsonSerializer.Serialize(new { error = $"Index '{indexName}' not found." }, s_jsonOptions);
        }

        var fileName = $"{indexName}.toolindex";
        await using var stream = File.Create(fileName);
        await index.SaveAsync(stream, cancellationToken).ConfigureAwait(false);

        return JsonSerializer.Serialize(new
        {
            action = "save",
            indexName,
            status = "saved",
            filePath = Path.GetFullPath(fileName),
            toolCount = index.Count
        }, s_jsonOptions);
    }

    private static async Task<string> LoadIndexAsync(string indexName, CancellationToken cancellationToken)
    {
        var fileName = $"{indexName}.toolindex";
        if (!File.Exists(fileName))
        {
            return JsonSerializer.Serialize(new { error = $"Index file '{fileName}' not found." }, s_jsonOptions);
        }

        await using var stream = File.OpenRead(fileName);
        var index = await ToolIndex.LoadAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        s_indexes[indexName] = index;

        return JsonSerializer.Serialize(new
        {
            action = "load",
            indexName,
            status = "loaded",
            toolCount = index.Count
        }, s_jsonOptions);
    }

    private static string ClearIndex(string indexName)
    {
        if (s_indexes.TryRemove(indexName, out var index))
        {
            if (index is IAsyncDisposable asyncDisposable)
            {
                asyncDisposable.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }

            return JsonSerializer.Serialize(new
            {
                action = "clear",
                indexName,
                status = "cleared"
            }, s_jsonOptions);
        }

        return JsonSerializer.Serialize(new
        {
            action = "clear",
            indexName,
            status = "not_found"
        }, s_jsonOptions);
    }

    #endregion
}
