using ElBruno.LocalEmbeddings.Options;

namespace ElBruno.ModelContextProtocol.MCPToolRouter;

/// <summary>
/// Provides information about the embedding model used by <see cref="ToolIndex"/> and <see cref="ToolRouter"/>.
/// Use this class to inspect model cache paths, check download status, or get a diagnostic summary.
/// </summary>
public static class EmbeddingModelInfo
{
    /// <summary>
    /// The default HuggingFace model used for embeddings.
    /// </summary>
    public const string DefaultModelName = "sentence-transformers/all-MiniLM-L6-v2";

    /// <summary>
    /// Gets the default directory where embedding models are cached.
    /// Uses the same path convention as <c>ElBruno.LocalEmbeddings</c>:
    /// <c>{LocalApplicationData}/ElBruno/LocalEmbeddings/models/{model-name}</c>.
    /// </summary>
    /// <returns>The absolute path to the default model cache directory.</returns>
    public static string GetDefaultCacheDirectory()
    {
        return ResolveModelDirectory(new LocalEmbeddingsOptions());
    }

    /// <summary>
    /// Gets the model cache directory that would be used with the given options.
    /// </summary>
    /// <param name="options">
    /// Embedding options to resolve. If null, uses default options
    /// (default model name, default cache location).
    /// </param>
    /// <returns>The absolute path to the resolved model directory.</returns>
    public static string GetModelDirectory(LocalEmbeddingsOptions? options = null)
    {
        options ??= new LocalEmbeddingsOptions();
        return ResolveModelDirectory(options);
    }

    /// <summary>
    /// Checks whether the embedding model files appear to be already downloaded
    /// at the expected cache location.
    /// </summary>
    /// <param name="options">
    /// Embedding options to check. If null, checks the default model at the default cache location.
    /// </param>
    /// <returns><see langword="true"/> if at least one <c>.onnx</c> model file exists in the resolved directory.</returns>
    public static bool IsModelDownloaded(LocalEmbeddingsOptions? options = null)
    {
        var dir = GetModelDirectory(options);
        if (!Directory.Exists(dir))
            return false;

        return Directory.GetFiles(dir, "*.onnx", SearchOption.AllDirectories).Length > 0;
    }

    /// <summary>
    /// Gets a summary of the model configuration: name, cache path, and download status.
    /// Useful for diagnostics and logging.
    /// </summary>
    /// <param name="options">
    /// Embedding options to inspect. If null, uses default options.
    /// </param>
    /// <returns>An <see cref="EmbeddingModelStatus"/> with the resolved configuration details.</returns>
    public static EmbeddingModelStatus GetStatus(LocalEmbeddingsOptions? options = null)
    {
        options ??= new LocalEmbeddingsOptions();
        var dir = GetModelDirectory(options);
        var downloaded = IsModelDownloaded(options);
        return new EmbeddingModelStatus
        {
            ModelName = options.ModelName ?? DefaultModelName,
            CacheDirectory = dir,
            IsDownloaded = downloaded,
            PreferQuantized = options.PreferQuantized
        };
    }

    /// <summary>
    /// Resolves the model directory from the given options, mirroring the path logic
    /// used by <c>ElBruno.LocalEmbeddings.ModelDownloader</c>.
    /// </summary>
    private static string ResolveModelDirectory(LocalEmbeddingsOptions options)
    {
        // If explicit ModelPath is set, use it directly (skip download scenario)
        if (!string.IsNullOrEmpty(options.ModelPath))
            return options.ModelPath;

        var modelName = options.ModelName ?? DefaultModelName;

        // If explicit CacheDirectory is set, combine with sanitized model name
        if (!string.IsNullOrEmpty(options.CacheDirectory))
        {
            return Path.Combine(
                options.CacheDirectory,
                modelName.Replace('/', Path.DirectorySeparatorChar));
        }

        // Default: matches ModelDownloader's default path:
        // {LocalApplicationData}/ElBruno/LocalEmbeddings/models/{model-name}
        var basePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ElBruno", "LocalEmbeddings", "models");

        return Path.Combine(basePath, modelName.Replace('/', Path.DirectorySeparatorChar));
    }
}
