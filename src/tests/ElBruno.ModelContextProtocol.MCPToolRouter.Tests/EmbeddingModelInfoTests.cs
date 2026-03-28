using ElBruno.LocalEmbeddings.Options;
using Xunit;

namespace ElBruno.ModelContextProtocol.MCPToolRouter.Tests;

public class EmbeddingModelInfoTests
{
    [Fact]
    public void DefaultModelName_IsExpectedValue()
    {
        Assert.Equal("sentence-transformers/all-MiniLM-L6-v2", EmbeddingModelInfo.DefaultModelName);
    }

    [Fact]
    public void GetDefaultCacheDirectory_ReturnsNonEmptyString()
    {
        var dir = EmbeddingModelInfo.GetDefaultCacheDirectory();
        Assert.False(string.IsNullOrEmpty(dir));
    }

    [Fact]
    public void GetDefaultCacheDirectory_ContainsModelName()
    {
        var dir = EmbeddingModelInfo.GetDefaultCacheDirectory();
        // Should contain the model name path segments
        Assert.Contains("sentence-transformers", dir);
    }

    [Fact]
    public void GetModelDirectory_WithNullOptions_ReturnsDefaultPath()
    {
        var dir = EmbeddingModelInfo.GetModelDirectory(null);
        var defaultDir = EmbeddingModelInfo.GetDefaultCacheDirectory();
        Assert.Equal(defaultDir, dir);
    }

    [Fact]
    public void GetModelDirectory_WithCustomCacheDirectory_UsesCustomPath()
    {
        var customCache = Path.Combine(Path.GetTempPath(), "custom-model-cache");
        var options = new LocalEmbeddingsOptions { CacheDirectory = customCache };
        var dir = EmbeddingModelInfo.GetModelDirectory(options);
        Assert.StartsWith(customCache, dir);
    }

    [Fact]
    public void GetModelDirectory_WithCustomModelPath_UsesModelPath()
    {
        var customPath = Path.Combine(Path.GetTempPath(), "my-model");
        var options = new LocalEmbeddingsOptions { ModelPath = customPath };
        var dir = EmbeddingModelInfo.GetModelDirectory(options);
        Assert.Equal(customPath, dir);
    }

    [Fact]
    public void IsModelDownloaded_WithNonExistentPath_ReturnsFalse()
    {
        var options = new LocalEmbeddingsOptions
        {
            CacheDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
        };
        Assert.False(EmbeddingModelInfo.IsModelDownloaded(options));
    }

    [Fact]
    public void GetStatus_ReturnsValidStatus()
    {
        var status = EmbeddingModelInfo.GetStatus();
        Assert.NotNull(status);
        Assert.False(string.IsNullOrEmpty(status.ModelName));
        Assert.False(string.IsNullOrEmpty(status.CacheDirectory));
        Assert.Equal(EmbeddingModelInfo.DefaultModelName, status.ModelName);
    }

    [Fact]
    public void GetStatus_WithCustomOptions_ReflectsOptions()
    {
        var options = new LocalEmbeddingsOptions
        {
            PreferQuantized = true,
            ModelName = "custom/model-name"
        };
        var status = EmbeddingModelInfo.GetStatus(options);
        Assert.True(status.PreferQuantized);
        Assert.Equal("custom/model-name", status.ModelName);
    }

    [Fact]
    public void GetStatus_CacheDirectoryIsAbsolutePath()
    {
        var status = EmbeddingModelInfo.GetStatus();
        Assert.True(Path.IsPathRooted(status.CacheDirectory));
    }
}
