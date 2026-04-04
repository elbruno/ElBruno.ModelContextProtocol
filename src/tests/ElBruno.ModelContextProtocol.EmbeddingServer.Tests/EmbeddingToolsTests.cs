using System.Text.Json;
using ElBruno.ModelContextProtocol.EmbeddingServer;
using Xunit;

namespace ElBruno.ModelContextProtocol.EmbeddingServer.Tests;

public class EmbeddingToolsTests : IDisposable
{
    public EmbeddingToolsTests()
    {
        EmbeddingTools.ResetState();
    }

    public void Dispose()
    {
        EmbeddingTools.ResetState();
    }

    #region embed_text Tests

    [Fact]
    public async Task EmbedText_WithEmptyText_ReturnsError()
    {
        var result = await EmbeddingTools.EmbedText("");

        var doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.TryGetProperty("error", out var error));
        Assert.Contains("empty", error.GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EmbedText_WithWhitespaceText_ReturnsError()
    {
        var result = await EmbeddingTools.EmbedText("   ");

        var doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.TryGetProperty("error", out var error));
        Assert.Contains("empty", error.GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EmbedText_WithNullText_ReturnsError()
    {
        var result = await EmbeddingTools.EmbedText(null!);

        var doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.TryGetProperty("error", out _));
    }

    [Fact]
    public async Task EmbedText_WithNewlinesOnly_ReturnsError()
    {
        var result = await EmbeddingTools.EmbedText("\n\n\n");

        var doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.TryGetProperty("error", out _));
    }

    #endregion

    #region search_embeddings Tests

    [Fact]
    public async Task SearchEmbeddings_WithEmptyQuery_ReturnsError()
    {
        var result = await EmbeddingTools.SearchEmbeddings("");

        var doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.TryGetProperty("error", out var error));
        Assert.Contains("empty", error.GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SearchEmbeddings_WithMissingIndex_ReturnsError()
    {
        var result = await EmbeddingTools.SearchEmbeddings("test query", indexName: "nonexistent");

        var doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.TryGetProperty("error", out var error));
        Assert.Contains("not found", error.GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SearchEmbeddings_WithNoIndex_ReturnsNotFoundError()
    {
        var result = await EmbeddingTools.SearchEmbeddings("test query");

        var doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.TryGetProperty("error", out var error));
        Assert.Contains("not found", error.GetString(), StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region get_embedding_model_status Tests

    [Fact]
    public void GetEmbeddingModelStatus_WithDefaults_ReturnsValidJson()
    {
        var result = EmbeddingTools.GetEmbeddingModelStatus();

        var doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.TryGetProperty("modelName", out var modelName));
        Assert.False(string.IsNullOrEmpty(modelName.GetString()));
        Assert.True(doc.RootElement.TryGetProperty("cacheDirectory", out _));
        Assert.True(doc.RootElement.TryGetProperty("isDownloaded", out _));
        Assert.True(doc.RootElement.TryGetProperty("provider", out var provider));
        Assert.Contains("ONNX", provider.GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetEmbeddingModelStatus_WithCustomModel_ReflectsModelName()
    {
        var result = EmbeddingTools.GetEmbeddingModelStatus("custom/test-model");

        var doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.TryGetProperty("modelName", out var modelName));
        Assert.Equal("custom/test-model", modelName.GetString());
    }

    [Fact]
    public void GetEmbeddingModelStatus_WithNullModel_ReturnsDefaultModel()
    {
        var result = EmbeddingTools.GetEmbeddingModelStatus(null);

        var doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.TryGetProperty("modelName", out var modelName));
        Assert.Equal("sentence-transformers/all-MiniLM-L6-v2", modelName.GetString());
    }

    [Fact]
    public void GetEmbeddingModelStatus_ReturnsAbsoluteCachePath()
    {
        var result = EmbeddingTools.GetEmbeddingModelStatus();

        var doc = JsonDocument.Parse(result);
        var cacheDir = doc.RootElement.GetProperty("cacheDirectory").GetString();
        Assert.True(Path.IsPathRooted(cacheDir));
    }

    #endregion

    #region manage_index Tests

    [Fact]
    public async Task ManageIndex_WithEmptyAction_ReturnsError()
    {
        var result = await EmbeddingTools.ManageIndex("", "test-index");

        var doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.TryGetProperty("error", out var error));
        Assert.Contains("Action must not be empty", error.GetString());
    }

    [Fact]
    public async Task ManageIndex_WithEmptyName_ReturnsError()
    {
        var result = await EmbeddingTools.ManageIndex("create", "");

        var doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.TryGetProperty("error", out var error));
        Assert.Contains("Index name must not be empty", error.GetString());
    }

    [Fact]
    public async Task ManageIndex_WithInvalidAction_ReturnsError()
    {
        var result = await EmbeddingTools.ManageIndex("delete", "test-index");

        var doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.TryGetProperty("error", out var error));
        Assert.Contains("Unknown action", error.GetString());
    }

    [Fact]
    public async Task ManageIndex_Clear_NonExistentIndex_ReturnsNotFound()
    {
        var result = await EmbeddingTools.ManageIndex("clear", "nonexistent");

        var doc = JsonDocument.Parse(result);
        Assert.Equal("clear", doc.RootElement.GetProperty("action").GetString());
        Assert.Equal("not_found", doc.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task ManageIndex_Save_NonExistentIndex_ReturnsError()
    {
        var result = await EmbeddingTools.ManageIndex("save", "nonexistent");

        var doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.TryGetProperty("error", out var error));
        Assert.Contains("not found", error.GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ManageIndex_Load_NonExistentFile_ReturnsError()
    {
        var result = await EmbeddingTools.ManageIndex("load", "nonexistent-file-" + Guid.NewGuid());

        var doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.TryGetProperty("error", out var error));
        Assert.Contains("not found", error.GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ManageIndex_WhitespaceAction_ReturnsError()
    {
        var result = await EmbeddingTools.ManageIndex("   ", "test-index");

        var doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.TryGetProperty("error", out _));
    }

    [Fact]
    public async Task ManageIndex_WhitespaceName_ReturnsError()
    {
        var result = await EmbeddingTools.ManageIndex("create", "   ");

        var doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.TryGetProperty("error", out _));
    }

    #endregion
}
