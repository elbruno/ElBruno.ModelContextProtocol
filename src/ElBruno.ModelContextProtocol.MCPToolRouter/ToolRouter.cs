using System.Diagnostics;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Protocol;

namespace ElBruno.ModelContextProtocol.MCPToolRouter;

/// <summary>
/// High-level facade that combines <see cref="ToolIndex"/> with <see cref="PromptDistiller"/>
/// for semantic tool routing with optional prompt distillation.
/// </summary>
/// <remarks>
/// Use the static <see cref="CreateAsync(IEnumerable{Tool}, IChatClient?, ToolRouterOptions?, CancellationToken)"/>
/// factory method to build an instance. The router can then be reused for multiple routing calls.
/// </remarks>
public sealed partial class ToolRouter : IAsyncDisposable
{
    private readonly IToolIndex _index;
    private readonly bool _ownsIndex;
    private readonly IChatClient? _chatClient;
    private readonly ToolRouterOptions _options;
    private readonly ILogger _logger;
    private int _disposed;

    private ToolRouter(
        IToolIndex index,
        bool ownsIndex,
        IChatClient? chatClient,
        ToolRouterOptions options)
    {
        _index = index;
        _ownsIndex = ownsIndex;
        _chatClient = chatClient;
        _options = options;
        _logger = options.IndexOptions?.Logger ?? NullLogger.Instance;
    }

    #region Factory Methods

    /// <summary>
    /// Creates a <see cref="ToolRouter"/> from a collection of MCP tool definitions.
    /// </summary>
    /// <param name="tools">The MCP tool definitions to index for routing.</param>
    /// <param name="chatClient">
    /// Optional chat client for prompt distillation. When provided and
    /// <see cref="ToolRouterOptions.EnableDistillation"/> is true, user prompts are
    /// distilled into single-sentence intents before semantic search.
    /// </param>
    /// <param name="options">Configuration options. If null, uses defaults.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A new <see cref="ToolRouter"/> instance.</returns>
    public static async Task<ToolRouter> CreateAsync(
        IEnumerable<Tool> tools,
        IChatClient? chatClient = null,
        ToolRouterOptions? options = null,
        CancellationToken ct = default)
    {
        return await CreateAsync(tools, chatClient, embeddingGenerator: null, options, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a <see cref="ToolRouter"/> from a collection of MCP tool definitions with a custom embedding generator.
    /// </summary>
    /// <param name="tools">The MCP tool definitions to index for routing.</param>
    /// <param name="chatClient">
    /// Optional chat client for prompt distillation. When provided and
    /// <see cref="ToolRouterOptions.EnableDistillation"/> is true, user prompts are
    /// distilled into single-sentence intents before semantic search.
    /// </param>
    /// <param name="embeddingGenerator">
    /// Custom embedding generator. If null, uses the default local embedding generator.
    /// </param>
    /// <param name="options">Configuration options. If null, uses defaults.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A new <see cref="ToolRouter"/> instance.</returns>
    public static async Task<ToolRouter> CreateAsync(
        IEnumerable<Tool> tools,
        IChatClient? chatClient,
        IEmbeddingGenerator<string, Embedding<float>>? embeddingGenerator,
        ToolRouterOptions? options = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(tools);

        options ??= new ToolRouterOptions();
        var indexOptions = options.IndexOptions ?? new ToolIndexOptions();

        // Apply the convenience EmbeddingModelCacheDirectory if set
        if (!string.IsNullOrEmpty(options.EmbeddingModelCacheDirectory))
        {
            indexOptions.EmbeddingOptions ??= new ElBruno.LocalEmbeddings.Options.LocalEmbeddingsOptions();
            indexOptions.EmbeddingOptions.CacheDirectory = options.EmbeddingModelCacheDirectory;
        }

        var index = await ToolIndex.CreateAsync(tools, embeddingGenerator, indexOptions, ct).ConfigureAwait(false);
        return new ToolRouter(index, ownsIndex: true, chatClient, options);
    }

    /// <summary>
    /// Creates a <see cref="ToolRouter"/> from a pre-built <see cref="IToolIndex"/>.
    /// The router does not take ownership of the index and will not dispose it.
    /// </summary>
    /// <param name="index">An existing tool index.</param>
    /// <param name="chatClient">Optional chat client for prompt distillation.</param>
    /// <param name="options">Configuration options. If null, uses defaults.</param>
    /// <returns>A new <see cref="ToolRouter"/> instance.</returns>
    internal static ToolRouter FromIndex(
        IToolIndex index,
        IChatClient? chatClient = null,
        ToolRouterOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(index);
        return new ToolRouter(index, ownsIndex: false, chatClient, options ?? new ToolRouterOptions());
    }

    #endregion

    #region Public API

    /// <summary>
    /// Gets the underlying tool index for advanced usage such as adding/removing tools.
    /// </summary>
    public IToolIndex Index => _index;

    /// <summary>
    /// Gets information about the embedding model used by this router's underlying index.
    /// Includes model name, cache directory, and whether the model is downloaded.
    /// </summary>
    /// <returns>An <see cref="EmbeddingModelStatus"/> describing the current model configuration.</returns>
    public EmbeddingModelStatus GetEmbeddingModelStatus()
    {
        return EmbeddingModelInfo.GetStatus(_options.IndexOptions?.EmbeddingOptions);
    }

    /// <summary>
    /// Routes a user prompt to the most relevant tools using semantic search,
    /// optionally distilling the prompt first via an LLM.
    /// </summary>
    /// <param name="userPrompt">The user prompt to route.</param>
    /// <param name="topK">
    /// Maximum number of tools to return. If null, uses <see cref="ToolRouterOptions.TopK"/>.
    /// </param>
    /// <param name="minScore">
    /// Minimum cosine similarity score. If null, uses <see cref="ToolRouterOptions.MinScore"/>.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of matching tools sorted by relevance (highest score first).</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="userPrompt"/> is null, empty, or whitespace.</exception>
    public async Task<IReadOnlyList<ToolSearchResult>> RouteAsync(
        string userPrompt,
        int? topK = null,
        float? minScore = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userPrompt);

        var sw = Stopwatch.StartNew();
        var effectiveTopK = topK ?? _options.TopK;
        var effectiveMinScore = minScore ?? _options.MinScore;

        var searchText = userPrompt;

        if (_chatClient is not null && _options.EnableDistillation)
        {
            var distillerOptions = _options.ToDistillerOptions();
            searchText = await PromptDistiller.DistillIntentAsync(_chatClient, userPrompt, distillerOptions, _logger, ct).ConfigureAwait(false);

            var promptPreview = userPrompt.Length > 50 ? userPrompt[..50] + "..." : userPrompt;
            var distilledPreview = searchText.Length > 80 ? searchText[..80] + "..." : searchText;
            LogMessages.DistillationCompleted(_logger, promptPreview, distilledPreview);
        }

        var results = await _index.SearchAsync(searchText, effectiveTopK, effectiveMinScore, ct).ConfigureAwait(false);

        sw.Stop();
        LogMessages.RoutingCompleted(_logger, results.Count, sw.ElapsedMilliseconds);

        return results;
    }

    #endregion

    #region Shared Resources for Static API

    private static readonly SemaphoreSlim _sharedResourceLock = new(1, 1);
    private static IEmbeddingGenerator<string, Embedding<float>>? _sharedEmbeddingGenerator;
    private static IChatClient? _sharedChatClient;

    private static async Task<IEmbeddingGenerator<string, Embedding<float>>> GetOrCreateSharedEmbeddingGeneratorAsync(
        ToolRouterOptions options, CancellationToken ct)
    {
        if (_sharedEmbeddingGenerator is not null)
            return _sharedEmbeddingGenerator;

        await _sharedResourceLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_sharedEmbeddingGenerator is not null)
                return _sharedEmbeddingGenerator;

            var indexOptions = options.IndexOptions ?? new ToolIndexOptions();

            if (!string.IsNullOrEmpty(options.EmbeddingModelCacheDirectory))
            {
                indexOptions.EmbeddingOptions ??= new ElBruno.LocalEmbeddings.Options.LocalEmbeddingsOptions();
                indexOptions.EmbeddingOptions.CacheDirectory = options.EmbeddingModelCacheDirectory;
            }

            _sharedEmbeddingGenerator = await ToolIndex.CreateDefaultGeneratorAsync(indexOptions, ct).ConfigureAwait(false);
            return _sharedEmbeddingGenerator;
        }
        finally
        {
            _sharedResourceLock.Release();
        }
    }

    private static async Task<IChatClient> GetOrCreateSharedChatClientAsync(
        ToolRouterOptions options, CancellationToken ct)
    {
        if (_sharedChatClient is not null)
            return _sharedChatClient;

        await _sharedResourceLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_sharedChatClient is not null)
                return _sharedChatClient;

            var llmOptions = new ElBruno.LocalLLMs.LocalLLMsOptions();
            if (options.LocalLLMModel is not null)
                llmOptions.Model = options.LocalLLMModel;

            _sharedChatClient = await ElBruno.LocalLLMs.LocalChatClient.CreateAsync(
                llmOptions, progress: null, ct).ConfigureAwait(false);

            return _sharedChatClient;
        }
        finally
        {
            _sharedResourceLock.Release();
        }
    }

    /// <summary>
    /// Releases shared resources (embedding generator, chat client) used by static API methods.
    /// Call this when your application shuts down or in test cleanup.
    /// Do not call while static API searches are in flight.
    /// </summary>
    public static async Task ResetSharedResourcesAsync()
    {
        await _sharedResourceLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_sharedEmbeddingGenerator is not null)
            {
                if (_sharedEmbeddingGenerator is IAsyncDisposable asyncDisposable)
                    await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                else if (_sharedEmbeddingGenerator is IDisposable disposable)
                    disposable.Dispose();
                _sharedEmbeddingGenerator = null;
            }

            if (_sharedChatClient is not null)
            {
                if (_sharedChatClient is IAsyncDisposable asyncDisposable)
                    await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                else if (_sharedChatClient is IDisposable disposable)
                    disposable.Dispose();
                _sharedChatClient = null;
            }
        }
        finally
        {
            _sharedResourceLock.Release();
        }
    }

    #endregion

    #region Simplified Static API

    /// <summary>
    /// Embeddings-only semantic search — one-liner for finding the most relevant tools.
    /// Creates a temporary index, searches, and disposes. No LLM required.
    /// When <see cref="ToolRouterOptions.UseSharedResources"/> is true (default),
    /// the expensive ONNX embedding session is shared across calls for ~15-35× faster repeated use.
    /// </summary>
    /// <param name="userPrompt">The user prompt to search for.</param>
    /// <param name="tools">The MCP tool definitions to search.</param>
    /// <param name="topK">Maximum number of tools to return. Default is 5.</param>
    /// <param name="minScore">Minimum cosine similarity score. Default is 0.0.</param>
    /// <param name="options">Configuration options. If null, uses defaults.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of matching tools sorted by relevance (highest score first).</returns>
    public static async Task<IReadOnlyList<ToolSearchResult>> SearchAsync(
        string userPrompt,
        IEnumerable<Tool> tools,
        int topK = 5,
        float minScore = 0.0f,
        ToolRouterOptions? options = null,
        CancellationToken ct = default)
    {
        options ??= new ToolRouterOptions();
        options.EnableDistillation = false;

        if (options.UseSharedResources)
        {
            var generator = await GetOrCreateSharedEmbeddingGeneratorAsync(options, ct).ConfigureAwait(false);
            await using var router = await CreateAsync(tools, chatClient: null, generator, options, ct).ConfigureAwait(false);
            return await router.RouteAsync(userPrompt, topK, minScore, ct).ConfigureAwait(false);
        }

        await using var freshRouter = await CreateAsync(tools, chatClient: null, options, ct).ConfigureAwait(false);
        return await freshRouter.RouteAsync(userPrompt, topK, minScore, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// One-shot search with LLM prompt distillation using a local LLM.
    /// Internally downloads and uses a small local model for prompt distillation.
    /// When <see cref="ToolRouterOptions.UseSharedResources"/> is true (default),
    /// both the ONNX embedding session and the local chat client are shared across calls.
    /// For custom LLM backends (Azure OpenAI, Ollama, etc.), use the overload that accepts an IChatClient.
    /// </summary>
    /// <param name="userPrompt">The user prompt to distill and search for.</param>
    /// <param name="tools">The MCP tool definitions to search.</param>
    /// <param name="topK">Maximum number of tools to return. Default is 5.</param>
    /// <param name="minScore">Minimum cosine similarity score. Default is 0.0.</param>
    /// <param name="options">Configuration options. If null, uses defaults.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of matching tools sorted by relevance (highest score first).</returns>
    public static async Task<IReadOnlyList<ToolSearchResult>> SearchUsingLLMAsync(
        string userPrompt,
        IEnumerable<Tool> tools,
        int topK = 5,
        float minScore = 0.0f,
        ToolRouterOptions? options = null,
        CancellationToken ct = default)
    {
        options ??= new ToolRouterOptions();

        if (options.UseSharedResources)
        {
            var chatClient = await GetOrCreateSharedChatClientAsync(options, ct).ConfigureAwait(false);
            return await SearchUsingLLMAsync(userPrompt, tools, chatClient, topK, minScore, options, ct)
                .ConfigureAwait(false);
        }

        var llmOptions = new ElBruno.LocalLLMs.LocalLLMsOptions();
        if (options.LocalLLMModel is not null)
            llmOptions.Model = options.LocalLLMModel;

        using var freshClient = await ElBruno.LocalLLMs.LocalChatClient.CreateAsync(
            llmOptions, progress: null, ct).ConfigureAwait(false);

        return await SearchUsingLLMAsync(userPrompt, tools, freshClient, topK, minScore, options, ct)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// LLM-distilled semantic search — distills the prompt via an LLM before searching.
    /// Creates a temporary index, distills, searches, and disposes.
    /// When <see cref="ToolRouterOptions.UseSharedResources"/> is true (default),
    /// the ONNX embedding session is shared across calls.
    /// </summary>
    /// <param name="userPrompt">The user prompt to distill and search for.</param>
    /// <param name="tools">The MCP tool definitions to search.</param>
    /// <param name="chatClient">The chat client used for prompt distillation.</param>
    /// <param name="topK">Maximum number of tools to return. Default is 5.</param>
    /// <param name="minScore">Minimum cosine similarity score. Default is 0.0.</param>
    /// <param name="options">Configuration options. If null, uses defaults.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of matching tools sorted by relevance (highest score first).</returns>
    public static async Task<IReadOnlyList<ToolSearchResult>> SearchUsingLLMAsync(
        string userPrompt,
        IEnumerable<Tool> tools,
        IChatClient chatClient,
        int topK = 5,
        float minScore = 0.0f,
        ToolRouterOptions? options = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(chatClient);
        options ??= new ToolRouterOptions();
        options.EnableDistillation = true;

        if (options.UseSharedResources)
        {
            var generator = await GetOrCreateSharedEmbeddingGeneratorAsync(options, ct).ConfigureAwait(false);
            await using var router = await CreateAsync(tools, chatClient, generator, options, ct).ConfigureAwait(false);
            return await router.RouteAsync(userPrompt, topK, minScore, ct).ConfigureAwait(false);
        }

        await using var freshRouter = await CreateAsync(tools, chatClient, options, ct).ConfigureAwait(false);
        return await freshRouter.RouteAsync(userPrompt, topK, minScore, ct).ConfigureAwait(false);
    }

    #endregion

    /// <summary>
    /// Disposes the underlying tool index if owned by this instance.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1) return;

        if (_ownsIndex)
            await _index.DisposeAsync().ConfigureAwait(false);
    }

    #region High-Performance Logging

    private static partial class LogMessages
    {
        [LoggerMessage(EventId = 100, Level = LogLevel.Debug, Message = "Prompt distilled: '{OriginalPreview}' → '{DistilledPreview}'")]
        public static partial void DistillationCompleted(ILogger logger, string originalPreview, string distilledPreview);

        [LoggerMessage(EventId = 101, Level = LogLevel.Information, Message = "Routing completed: {ResultCount} tools matched in {ElapsedMs}ms")]
        public static partial void RoutingCompleted(ILogger logger, int resultCount, long elapsedMs);
    }

    #endregion
}
