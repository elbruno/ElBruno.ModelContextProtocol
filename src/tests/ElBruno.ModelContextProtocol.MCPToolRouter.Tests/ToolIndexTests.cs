using ModelContextProtocol.Protocol;
using Xunit;

namespace ElBruno.ModelContextProtocol.MCPToolRouter.Tests;

/// <summary>
/// Shared fixture to avoid downloading the ONNX model multiple times.
/// The model (~90MB) is downloaded on first use.
/// </summary>
public class SharedToolIndexFixture : IAsyncLifetime
{
    public ToolIndex Index { get; private set; } = null!;
    public Tool[] Tools { get; } = new[]
    {
        new Tool { Name = "get_weather", Description = "Get current weather information for a location" },
        new Tool { Name = "send_email", Description = "Send an email message to a recipient" },
        new Tool { Name = "search_files", Description = "Search for files by name or content" },
        new Tool { Name = "calculate_math", Description = "Perform mathematical calculations" },
        new Tool { Name = "translate_text", Description = "Translate text between languages" }
    };

    public async Task InitializeAsync() => Index = await ToolIndex.CreateAsync(Tools);
    public async Task DisposeAsync() => await Index.DisposeAsync();
}

public class ToolIndexTests : IClassFixture<SharedToolIndexFixture>
{
    private readonly SharedToolIndexFixture _fixture;

    public ToolIndexTests(SharedToolIndexFixture fixture)
    {
        _fixture = fixture;
    }

    #region Input Validation Tests

    [Fact]
    public async Task CreateAsync_WithNullTools_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await ToolIndex.CreateAsync(null!));
    }

    [Fact]
    public async Task CreateAsync_WithEmptyTools_ThrowsArgumentException()
    {
        // Arrange
        var tools = Array.Empty<Tool>();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            async () => await ToolIndex.CreateAsync(tools));
        Assert.Contains("At least one tool must be provided", exception.Message);
    }

    [Fact]
    public async Task SearchAsync_WithNullPrompt_ThrowsArgumentException()
    {
        // Act & Assert - ArgumentNullException is a subclass of ArgumentException
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await _fixture.Index.SearchAsync(null!));
    }

    [Fact]
    public async Task SearchAsync_WithEmptyPrompt_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await _fixture.Index.SearchAsync(string.Empty));
    }

    [Fact]
    public async Task SearchAsync_WithWhitespacePrompt_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await _fixture.Index.SearchAsync("   "));
    }

    [Fact]
    public async Task SearchAsync_WithZeroTopK_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            async () => await _fixture.Index.SearchAsync("test query", topK: 0));
    }

    [Fact]
    public async Task SearchAsync_WithNegativeTopK_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            async () => await _fixture.Index.SearchAsync("test query", topK: -1));
    }

    [Fact]
    public async Task SearchAsync_WithNegativeMinScore_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            async () => await _fixture.Index.SearchAsync("test query", minScore: -0.1f));
    }

    #endregion

    #region Core Functionality Tests

    [Fact]
    public async Task CreateAsync_WithSingleTool_ReturnsIndexWithCount1()
    {
        // Arrange
        var tools = new[]
        {
            new Tool { Name = "test_tool", Description = "A test tool for validation" }
        };

        // Act
        await using var index = await ToolIndex.CreateAsync(tools);

        // Assert
        Assert.NotNull(index);
        Assert.Equal(1, index.Count);
    }

    [Fact]
    public async Task CreateAsync_WithMultipleTools_ReturnsCorrectCount()
    {
        // Arrange
        var tools = new[]
        {
            new Tool { Name = "tool1", Description = "First tool" },
            new Tool { Name = "tool2", Description = "Second tool" },
            new Tool { Name = "tool3", Description = "Third tool" }
        };

        // Act
        await using var index = await ToolIndex.CreateAsync(tools);

        // Assert
        Assert.Equal(3, index.Count);
    }

    [Fact]
    public async Task SearchAsync_ReturnsResultsSortedByScoreDescending()
    {
        // Act
        var results = await _fixture.Index.SearchAsync("weather temperature forecast", topK: 5);

        // Assert
        Assert.NotEmpty(results);
        for (int i = 0; i < results.Count - 1; i++)
        {
            Assert.True(results[i].Score >= results[i + 1].Score,
                $"Results not sorted: Score at index {i} ({results[i].Score}) < Score at index {i + 1} ({results[i + 1].Score})");
        }
    }

    [Fact]
    public async Task SearchAsync_TopKLimitsResults()
    {
        // Act
        var results = await _fixture.Index.SearchAsync("search", topK: 2);

        // Assert
        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task SearchAsync_MinScoreFiltersLowRelevanceResults()
    {
        // Act - Use very high minScore to filter out results
        var results = await _fixture.Index.SearchAsync("completely unrelated quantum physics theory", topK: 10, minScore: 0.8f);

        // Assert
        Assert.True(results.Count < _fixture.Tools.Length,
            "High minScore should filter out low-relevance results");
    }

    [Fact]
    public async Task SearchAsync_TopKLargerThanToolCount_ReturnsAll()
    {
        // Act - Request more results than available tools
        var results = await _fixture.Index.SearchAsync("tool", topK: 100);

        // Assert
        Assert.Equal(_fixture.Tools.Length, results.Count);
    }

    [Fact]
    public void Count_ReflectsNumberOfIndexedTools()
    {
        // Assert
        Assert.Equal(_fixture.Tools.Length, _fixture.Index.Count);
    }

    #endregion

    #region Semantic Relevance Tests

    [Fact]
    public async Task SearchAsync_WeatherQueryRanksWeatherToolFirst()
    {
        // Act
        var results = await _fixture.Index.SearchAsync("What's the temperature?", topK: 5);

        // Assert
        Assert.NotEmpty(results);
        Assert.Equal("get_weather", results[0].Tool.Name);
    }

    [Fact]
    public async Task SearchAsync_EmailQueryRanksEmailToolFirst()
    {
        // Act
        var results = await _fixture.Index.SearchAsync("Send a message", topK: 5);

        // Assert
        Assert.NotEmpty(results);
        Assert.Equal("send_email", results[0].Tool.Name);
    }

    [Fact]
    public async Task SearchAsync_UnrelatedQueryWithHighMinScore_ReturnsEmpty()
    {
        // Act
        var results = await _fixture.Index.SearchAsync("quantum physics research paper", topK: 5, minScore: 0.9f);

        // Assert - Unrelated query with very high threshold should return no results
        Assert.Empty(results);
    }

    #endregion

    #region Disposal Tests

    [Fact]
    public async Task DisposeAsync_CanBeCalledMultipleTimes()
    {
        // Arrange
        var tools = new[] { new Tool { Name = "test", Description = "Test tool" } };
        var index = await ToolIndex.CreateAsync(tools);

        // Act & Assert - Should not throw
        await index.DisposeAsync();
        await index.DisposeAsync();
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task CreateAsync_WithToolWithNoDescription_Succeeds()
    {
        // Arrange
        var tools = new[]
        {
            new Tool { Name = "no_description_tool", Description = null }
        };

        // Act & Assert - Should not throw
        await using var index = await ToolIndex.CreateAsync(tools);
        Assert.NotNull(index);
        Assert.Equal(1, index.Count);
    }

    [Fact]
    public async Task SearchAsync_WithToolWithNoDescription_StillReturnsResults()
    {
        // Arrange
        var tools = new[]
        {
            new Tool { Name = "tool_with_description", Description = "This tool has a description" },
            new Tool { Name = "tool_without_description", Description = null }
        };

        await using var index = await ToolIndex.CreateAsync(tools);

        // Act
        var results = await index.SearchAsync("description", topK: 5);

        // Assert - Should still return results even with null description
        Assert.NotEmpty(results);
    }

    #endregion

    #region Save/Load Round-Trip Tests

    [Fact]
    public async Task SaveAsync_LoadAsync_RoundTrip_PreservesToolsAndCount()
    {
        // Arrange
        var stream = new MemoryStream();
        await _fixture.Index.SaveAsync(stream);
        stream.Position = 0;

        // Act
        await using var loaded = await ToolIndex.LoadAsync(stream);

        // Assert
        Assert.Equal(_fixture.Index.Count, loaded.Count);
    }

    [Fact]
    public async Task SaveAsync_LoadAsync_RoundTrip_SearchReturnsSameResults()
    {
        // Arrange
        var stream = new MemoryStream();
        await _fixture.Index.SaveAsync(stream);
        stream.Position = 0;
        await using var loaded = await ToolIndex.LoadAsync(stream);

        // Act
        var originalResults = await _fixture.Index.SearchAsync("weather forecast", topK: 3);
        var loadedResults = await loaded.SearchAsync("weather forecast", topK: 3);

        // Assert
        Assert.Equal(originalResults.Count, loadedResults.Count);
        Assert.Equal(originalResults[0].Tool.Name, loadedResults[0].Tool.Name);
    }

    #endregion

    #region AddTools / RemoveTools Tests

    [Fact]
    public async Task AddToolsAsync_IncreasesCount()
    {
        // Arrange — create a separate index to avoid mutating the shared fixture
        var tools = new[] { new Tool { Name = "base_tool", Description = "Base tool for testing" } };
        await using var index = await ToolIndex.CreateAsync(tools);
        var initialCount = index.Count;

        // Act
        await index.AddToolsAsync(new[] { new Tool { Name = "added_tool", Description = "Dynamically added tool" } });

        // Assert
        Assert.Equal(initialCount + 1, index.Count);
    }

    [Fact]
    public async Task RemoveTools_DecreasesCount()
    {
        // Arrange
        var tools = new[]
        {
            new Tool { Name = "keep_tool", Description = "This tool stays" },
            new Tool { Name = "remove_tool", Description = "This tool goes away" }
        };
        await using var index = await ToolIndex.CreateAsync(tools);
        Assert.Equal(2, index.Count);

        // Act
        index.RemoveTools(new[] { "remove_tool" });

        // Assert
        Assert.Equal(1, index.Count);
    }

    [Fact]
    public async Task AddToolsAsync_AddedToolIsSearchable()
    {
        // Arrange
        var tools = new[] { new Tool { Name = "original_tool", Description = "Original tool" } };
        await using var index = await ToolIndex.CreateAsync(tools);
        await index.AddToolsAsync(new[] { new Tool { Name = "cooking_recipe", Description = "Finds and suggests cooking recipes and meal ideas" } });

        // Act
        var results = await index.SearchAsync("I want to cook dinner", topK: 2);

        // Assert
        Assert.Contains(results, r => r.Tool.Name == "cooking_recipe");
    }

    [Fact]
    public async Task RemoveTools_RemovedToolNotReturnedInSearch()
    {
        // Arrange
        var tools = new[]
        {
            new Tool { Name = "get_weather", Description = "Get current weather information" },
            new Tool { Name = "send_email", Description = "Send email messages" }
        };
        await using var index = await ToolIndex.CreateAsync(tools);

        // Act
        index.RemoveTools(new[] { "get_weather" });
        var results = await index.SearchAsync("weather temperature", topK: 5);

        // Assert
        Assert.DoesNotContain(results, r => r.Tool.Name == "get_weather");
    }

    #endregion

    #region ToolIndexOptions Tests

    [Fact]
    public async Task CreateAsync_WithOptions_Succeeds()
    {
        // Arrange
        var tools = new[] { new Tool { Name = "test_tool", Description = "A test tool" } };
        var options = new ToolIndexOptions
        {
            QueryCacheSize = 10,
            EmbeddingTextTemplate = "Tool: {Name} — {Description}"
        };

        // Act
        await using var index = await ToolIndex.CreateAsync(tools, options);

        // Assert
        Assert.NotNull(index);
        Assert.Equal(1, index.Count);
    }

    [Fact]
    public async Task CreateAsync_WithQueryCache_ReturnsConsistentResults()
    {
        // Arrange
        var tools = new[]
        {
            new Tool { Name = "get_weather", Description = "Get weather information" },
            new Tool { Name = "send_email", Description = "Send email messages" }
        };
        var options = new ToolIndexOptions { QueryCacheSize = 5 };
        await using var index = await ToolIndex.CreateAsync(tools, options);

        // Act — same query twice should use cache on second call
        var results1 = await index.SearchAsync("weather", topK: 2);
        var results2 = await index.SearchAsync("weather", topK: 2);

        // Assert — results should be identical
        Assert.Equal(results1.Count, results2.Count);
        Assert.Equal(results1[0].Tool.Name, results2[0].Tool.Name);
        Assert.Equal(results1[0].Score, results2[0].Score);
    }

    #endregion
}
