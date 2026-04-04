using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using System.Text;
using System.Text.Json;
using ElBruno.LocalEmbeddings;
using ElBruno.LocalEmbeddings.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Protocol;

namespace ElBruno.ModelContextProtocol.MCPToolRouter;

/// <summary>
/// Indexes MCP tool definitions for semantic search using local embeddings.
/// Implements <see cref="IToolIndex"/> for DI-friendly usage.
/// </summary>
public sealed partial class ToolIndex : IToolIndex
{
    private const int FormatVersion = 1;
    private const int MaxToolCount = 100_000;
    private const int MaxEmbeddingDimension = 8192;

    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly bool _ownsGenerator;
    private readonly ToolIndexOptions _options;
    private readonly ILogger _logger;
    private readonly ReaderWriterLockSlim _lock = new();

    // Query embedding cache (FIFO eviction when QueryCacheSize > 0)
    private readonly ConcurrentDictionary<string, float[]> _queryCache = new();
    private readonly ConcurrentQueue<string> _queryCacheOrder = new();

    private List<Tool> _tools;
    private List<float[]> _vectors;
    private int _disposed;

    private ToolIndex(
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        bool ownsGenerator,
        List<Tool> tools,
        List<float[]> vectors,
        ToolIndexOptions options)
    {
        _embeddingGenerator = embeddingGenerator;
        _ownsGenerator = ownsGenerator;
        _tools = tools;
        _vectors = vectors;
        _options = options;
        _logger = options.Logger ?? NullLogger.Instance;
    }

    #region Factory Methods

    /// <summary>
    /// Creates a ToolIndex from a collection of MCP tool definitions.
    /// </summary>
    /// <param name="tools">The MCP tool definitions to index.</param>
    /// <param name="embeddingOptions">Options for the local embedding generator. If null, uses defaults.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A new ToolIndex instance.</returns>
    [Obsolete("Use the overload accepting ToolIndexOptions instead.")]
    public static async Task<ToolIndex> CreateAsync(
        IEnumerable<Tool> tools,
        LocalEmbeddingsOptions? embeddingOptions,
        CancellationToken cancellationToken = default)
    {
        var options = new ToolIndexOptions { EmbeddingOptions = embeddingOptions };
        return await CreateAsync(tools, options, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a ToolIndex from a collection of MCP tool definitions.
    /// Generates embeddings for each tool's name and description.
    /// </summary>
    /// <param name="tools">The MCP tool definitions to index.</param>
    /// <param name="options">Configuration options. If null, uses defaults.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A new ToolIndex instance.</returns>
    public static async Task<ToolIndex> CreateAsync(
        IEnumerable<Tool> tools,
        ToolIndexOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return await CreateAsync(tools, embeddingGenerator: null, options, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a ToolIndex from a collection of MCP tool definitions with a custom embedding generator.
    /// </summary>
    /// <param name="tools">The MCP tool definitions to index.</param>
    /// <param name="embeddingGenerator">Custom embedding generator. If null, creates a local generator.</param>
    /// <param name="options">Configuration options. If null, uses defaults.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A new ToolIndex instance.</returns>
    public static async Task<ToolIndex> CreateAsync(
        IEnumerable<Tool> tools,
        IEmbeddingGenerator<string, Embedding<float>>? embeddingGenerator,
        ToolIndexOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tools);

        var toolArray = tools.ToArray();
        if (toolArray.Length == 0)
        {
            throw new ArgumentException("At least one tool must be provided.", nameof(tools));
        }

        options ??= new ToolIndexOptions();
        var logger = options.Logger ?? NullLogger.Instance;
        var sw = Stopwatch.StartNew();

        bool ownsGenerator = embeddingGenerator is null;
        var generator = embeddingGenerator ?? await CreateDefaultGeneratorAsync(options, cancellationToken).ConfigureAwait(false);

        try
        {
            var textsToEmbed = toolArray
                .Select(t => FormatEmbeddingText(t, options.EmbeddingTextTemplate))
                .ToArray();

            var embeddings = await generator.GenerateAsync(textsToEmbed, cancellationToken: cancellationToken).ConfigureAwait(false);
            var vectors = embeddings.Select(e => e.Vector.ToArray()).ToList();

            sw.Stop();
            LogMessages.IndexCreated(logger, toolArray.Length, sw.ElapsedMilliseconds);

            return new ToolIndex(generator, ownsGenerator, new List<Tool>(toolArray), vectors, options);
        }
        catch
        {
            if (ownsGenerator)
                await DisposeGeneratorAsync(generator).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Creates an empty ToolIndex that can have tools added later via <see cref="AddToolsAsync"/>.
    /// </summary>
    internal static async Task<ToolIndex> CreateEmptyAsync(
        IEmbeddingGenerator<string, Embedding<float>>? embeddingGenerator = null,
        ToolIndexOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new ToolIndexOptions();
        bool ownsGenerator = embeddingGenerator is null;
        var generator = embeddingGenerator ?? await CreateDefaultGeneratorAsync(options, cancellationToken).ConfigureAwait(false);
        return new ToolIndex(generator, ownsGenerator, new List<Tool>(), new List<float[]>(), options);
    }

    /// <summary>
    /// Loads a previously saved index from a stream.
    /// </summary>
    /// <param name="stream">The source stream containing a saved index.</param>
    /// <param name="embeddingGenerator">Custom embedding generator for future searches. If null, creates a local generator.</param>
    /// <param name="options">Configuration options. If null, uses defaults.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A ToolIndex restored from the stream.</returns>
    public static async Task<ToolIndex> LoadAsync(
        Stream stream,
        IEmbeddingGenerator<string, Embedding<float>>? embeddingGenerator = null,
        ToolIndexOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        options ??= new ToolIndexOptions();
        bool ownsGenerator = embeddingGenerator is null;
        var generator = embeddingGenerator ?? await CreateDefaultGeneratorAsync(options, cancellationToken).ConfigureAwait(false);

        try
        {
            using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);

            var version = reader.ReadInt32();
            if (version != FormatVersion)
                throw new InvalidDataException($"Unsupported index format version: {version}. Expected: {FormatVersion}.");

            var toolCount = reader.ReadInt32();
            if (toolCount < 0 || toolCount > MaxToolCount)
                throw new InvalidDataException($"Tool count {toolCount} is out of range [0, {MaxToolCount}].");

            var embeddingDim = reader.ReadInt32();
            if (embeddingDim < 0 || embeddingDim > MaxEmbeddingDimension)
                throw new InvalidDataException($"Embedding dimension {embeddingDim} is out of range [0, {MaxEmbeddingDimension}].");

            var tools = new List<Tool>(toolCount);
            var vectors = new List<float[]>(toolCount);

            for (int i = 0; i < toolCount; i++)
            {
                var name = reader.ReadString();
                var description = reader.ReadString();
                tools.Add(new Tool
                {
                    Name = name,
                    Description = description.Length == 0 ? null : description
                });

                var vectorLength = reader.ReadInt32();
                if (vectorLength != embeddingDim)
                    throw new InvalidDataException($"Vector {i} has dimension {vectorLength}, expected {embeddingDim}.");

                var vector = new float[vectorLength];
                for (int j = 0; j < vectorLength; j++)
                    vector[j] = reader.ReadSingle();
                vectors.Add(vector);
            }

            return new ToolIndex(generator, ownsGenerator, tools, vectors, options);
        }
        catch
        {
            if (ownsGenerator)
                await DisposeGeneratorAsync(generator).ConfigureAwait(false);
            throw;
        }
    }

    #endregion

    #region IToolIndex Implementation

    /// <summary>
    /// Gets information about the embedding model used by a tool index with the given options.
    /// Includes model name, cache directory, and whether the model is downloaded.
    /// </summary>
    /// <param name="options">Index options to inspect. If null, uses defaults.</param>
    /// <returns>An <see cref="EmbeddingModelStatus"/> describing the model configuration.</returns>
    public static EmbeddingModelStatus GetEmbeddingModelStatus(ToolIndexOptions? options = null)
    {
        return EmbeddingModelInfo.GetStatus(options?.EmbeddingOptions);
    }

    /// <inheritdoc/>
    public int Count
    {
        get
        {
            _lock.EnterReadLock();
            try { return _tools.Count; }
            finally { _lock.ExitReadLock(); }
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ToolSearchResult>> SearchAsync(
        string prompt,
        int topK = 5,
        float minScore = 0.0f,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(topK);
        ArgumentOutOfRangeException.ThrowIfNegative(minScore);

        float[] queryVector;
        if (_options.QueryCacheSize > 0 && _queryCache.TryGetValue(prompt, out var cached))
        {
            queryVector = cached;
            LogMessages.QueryCacheHit(_logger, prompt.Length > 50 ? prompt[..50] + "..." : prompt);
        }
        else
        {
            var queryEmbedding = await _embeddingGenerator.GenerateEmbeddingAsync(prompt, cancellationToken: cancellationToken).ConfigureAwait(false);
            queryVector = queryEmbedding.Vector.ToArray();

            if (_options.QueryCacheSize > 0)
            {
                LogMessages.QueryCacheMiss(_logger, prompt.Length > 50 ? prompt[..50] + "..." : prompt);
                if (_queryCache.TryAdd(prompt, queryVector))
                {
                    _queryCacheOrder.Enqueue(prompt);

                    // Evict oldest entries when cache exceeds configured size
                    while (_queryCache.Count > _options.QueryCacheSize
                           && _queryCacheOrder.TryDequeue(out var oldest))
                    {
                        _queryCache.TryRemove(oldest, out _);
                    }
                }
            }
        }

        _lock.EnterReadLock();
        try
        {
            var results = new List<(int Index, float Score)>(_tools.Count);

            for (int i = 0; i < _tools.Count; i++)
            {
                var similarity = CosineSimilarity(queryVector.AsSpan(), _vectors[i].AsSpan());
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

            var topScore = topResults.Count > 0 ? topResults[0].Score : 0f;
            var promptPreview = prompt.Length > 50 ? prompt[..50] + "..." : prompt;
            LogMessages.SearchCompleted(_logger, promptPreview, topResults.Count, topScore);

            return topResults;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <inheritdoc/>
    public async Task AddToolsAsync(IEnumerable<Tool> tools, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tools);

        var toolArray = tools.ToArray();
        if (toolArray.Length == 0) return;

        var textsToEmbed = toolArray
            .Select(t => FormatEmbeddingText(t, _options.EmbeddingTextTemplate))
            .ToArray();

        var embeddings = await _embeddingGenerator.GenerateAsync(textsToEmbed, cancellationToken: cancellationToken).ConfigureAwait(false);
        var newVectors = embeddings.Select(e => e.Vector.ToArray()).ToList();

        _lock.EnterWriteLock();
        try
        {
            _tools.AddRange(toolArray);
            _vectors.AddRange(newVectors);
            ClearQueryCache();
        }
        finally
        {
            _lock.ExitWriteLock();
        }

        LogMessages.ToolsAdded(_logger, toolArray.Length, _tools.Count);
    }

    /// <inheritdoc/>
    public void RemoveTools(IEnumerable<string> toolNames)
    {
        ArgumentNullException.ThrowIfNull(toolNames);

        var nameSet = new HashSet<string>(toolNames, StringComparer.Ordinal);
        if (nameSet.Count == 0) return;

        _lock.EnterWriteLock();
        try
        {
            var newTools = new List<Tool>(_tools.Count);
            var newVectors = new List<float[]>(_vectors.Count);

            for (int i = 0; i < _tools.Count; i++)
            {
                if (!nameSet.Contains(_tools[i].Name))
                {
                    newTools.Add(_tools[i]);
                    newVectors.Add(_vectors[i]);
                }
            }

            var removedCount = _tools.Count - newTools.Count;
            _tools = newTools;
            _vectors = newVectors;
            ClearQueryCache();

            LogMessages.ToolsRemoved(_logger, removedCount, _tools.Count);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <inheritdoc/>
    public async Task SaveAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        _lock.EnterReadLock();
        try
        {
            using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);

            var embeddingDim = _vectors.Count > 0 ? _vectors[0].Length : 0;
            writer.Write(FormatVersion);
            writer.Write(_tools.Count);
            writer.Write(embeddingDim);

            for (int i = 0; i < _tools.Count; i++)
            {
                writer.Write(_tools[i].Name);
                writer.Write(_tools[i].Description ?? string.Empty);
                writer.Write(_vectors[i].Length);
                foreach (var f in _vectors[i])
                    writer.Write(f);
            }

            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    #endregion

    /// <summary>
    /// Disposes the embedding generator if owned by this instance.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1) return;

        if (_ownsGenerator)
            await DisposeGeneratorAsync(_embeddingGenerator).ConfigureAwait(false);

        _lock.Dispose();
    }

    #region Private Helpers

    internal static async Task<IEmbeddingGenerator<string, Embedding<float>>> CreateDefaultGeneratorAsync(
        ToolIndexOptions options,
        CancellationToken cancellationToken)
    {
        return options.EmbeddingOptions is not null
            ? await LocalEmbeddingGenerator.CreateAsync(options.EmbeddingOptions, cancellationToken: cancellationToken).ConfigureAwait(false)
            : await LocalEmbeddingGenerator.CreateAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask DisposeGeneratorAsync(IEmbeddingGenerator<string, Embedding<float>> generator)
    {
        if (generator is IAsyncDisposable asyncDisposable)
            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        else if (generator is IDisposable disposable)
            disposable.Dispose();
    }

    internal static string FormatEmbeddingText(Tool tool, string template)
    {
        var result = template
            .Replace("{Name}", tool.Name)
            .Replace("{Description}", tool.Description ?? string.Empty);

        if (result.Contains("{Parameters}"))
        {
            result = result.Replace("{Parameters}", FormatParameters(tool));
        }

        if (result.Contains("{InputSchema}"))
        {
            result = result.Replace("{InputSchema}", FormatInputSchema(tool));
        }

        return result;
    }

    internal static string FormatParameters(Tool tool)
    {
        if (tool.InputSchema.ValueKind == JsonValueKind.Undefined ||
            tool.InputSchema.ValueKind == JsonValueKind.Null)
        {
            return string.Empty;
        }

        if (!tool.InputSchema.TryGetProperty("properties", out var properties) ||
            properties.ValueKind != JsonValueKind.Object)
        {
            return string.Empty;
        }

        var parts = new List<string>();
        foreach (var prop in properties.EnumerateObject())
        {
            var sb = new StringBuilder(prop.Name);

            if (prop.Value.TryGetProperty("type", out var typeElement) &&
                typeElement.ValueKind == JsonValueKind.String)
            {
                sb.Append(" (").Append(typeElement.GetString()).Append(')');
            }

            if (prop.Value.TryGetProperty("description", out var descElement) &&
                descElement.ValueKind == JsonValueKind.String)
            {
                sb.Append(" - ").Append(descElement.GetString());
            }

            parts.Add(sb.ToString());
        }

        return string.Join(", ", parts);
    }

    internal static string FormatInputSchema(Tool tool)
    {
        if (tool.InputSchema.ValueKind == JsonValueKind.Undefined ||
            tool.InputSchema.ValueKind == JsonValueKind.Null)
        {
            return string.Empty;
        }

        return tool.InputSchema.GetRawText();
    }

    private static float CosineSimilarity(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        float dot = 0, magA = 0, magB = 0;
        int i = 0;
        int vectorSize = Vector<float>.Count;

        // SIMD loop
        for (; i <= a.Length - vectorSize; i += vectorSize)
        {
            var va = new Vector<float>(a.Slice(i, vectorSize));
            var vb = new Vector<float>(b.Slice(i, vectorSize));
            dot += Vector.Dot(va, vb);
            magA += Vector.Dot(va, va);
            magB += Vector.Dot(vb, vb);
        }

        // Scalar remainder
        for (; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            magA += a[i] * a[i];
            magB += b[i] * b[i];
        }

        var mag = MathF.Sqrt(magA) * MathF.Sqrt(magB);
        return mag == 0 ? 0 : dot / mag;
    }

    private void ClearQueryCache()
    {
        _queryCache.Clear();
        while (_queryCacheOrder.TryDequeue(out _)) { }
    }

    #endregion

    #region High-Performance Logging

    private static partial class LogMessages
    {
        [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "ToolIndex created with {ToolCount} tools in {ElapsedMs}ms")]
        public static partial void IndexCreated(ILogger logger, int toolCount, long elapsedMs);

        [LoggerMessage(EventId = 2, Level = LogLevel.Debug, Message = "Search for '{PromptPreview}' returned {ResultCount} results (top score: {TopScore})")]
        public static partial void SearchCompleted(ILogger logger, string promptPreview, int resultCount, float topScore);

        [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "Added {NewCount} tools (total: {TotalCount})")]
        public static partial void ToolsAdded(ILogger logger, int newCount, int totalCount);

        [LoggerMessage(EventId = 4, Level = LogLevel.Information, Message = "Removed {RemovedCount} tools (total: {TotalCount})")]
        public static partial void ToolsRemoved(ILogger logger, int removedCount, int totalCount);

        [LoggerMessage(EventId = 5, Level = LogLevel.Debug, Message = "Query cache hit for '{PromptPreview}'")]
        public static partial void QueryCacheHit(ILogger logger, string promptPreview);

        [LoggerMessage(EventId = 6, Level = LogLevel.Debug, Message = "Query cache miss for '{PromptPreview}'")]
        public static partial void QueryCacheMiss(ILogger logger, string promptPreview);
    }

    #endregion
}
