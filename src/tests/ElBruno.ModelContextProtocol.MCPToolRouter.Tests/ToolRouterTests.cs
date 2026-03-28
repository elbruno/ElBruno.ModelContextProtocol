using Microsoft.Extensions.AI;
using ModelContextProtocol.Protocol;
using Xunit;

namespace ElBruno.ModelContextProtocol.MCPToolRouter.Tests;

/// <summary>
/// Shared fixture that creates a ToolRouter once for all routing tests.
/// Avoids repeated ONNX model downloads and index creation.
/// </summary>
public class SharedToolRouterFixture : IAsyncLifetime
{
    public ToolRouter Router { get; private set; } = null!;
    public Tool[] Tools { get; } = new[]
    {
        new Tool { Name = "get_weather", Description = "Get current weather information for a location" },
        new Tool { Name = "send_email", Description = "Send an email message to a recipient" },
        new Tool { Name = "search_files", Description = "Search for files by name or content" },
        new Tool { Name = "calculate_math", Description = "Perform mathematical calculations" },
        new Tool { Name = "translate_text", Description = "Translate text between languages" }
    };

    public async Task InitializeAsync()
    {
        Router = await ToolRouter.CreateAsync(Tools);
    }

    public async Task DisposeAsync() => await Router.DisposeAsync();
}

public class ToolRouterTests : IClassFixture<SharedToolRouterFixture>
{
    private readonly SharedToolRouterFixture _fixture;

    public ToolRouterTests(SharedToolRouterFixture fixture)
    {
        _fixture = fixture;
    }

    #region Helper: Fake IChatClient

    private class FakeChatClient : IChatClient
    {
        private readonly string _response;
        public FakeChatClient(string response) => _response = response;

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken ct = default)
            => Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, _response)));

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken ct = default)
            => throw new NotImplementedException();

        public void Dispose() { }
        public object? GetService(Type serviceType, object? key = null) => null;
    }

    #endregion

    #region CreateAsync Tests

    [Fact]
    public void CreateAsync_WithTools_ReturnsRouter()
    {
        // Assert — fixture creates the router successfully
        Assert.NotNull(_fixture.Router);
        Assert.Equal(_fixture.Tools.Length, _fixture.Router.Index.Count);
    }

    [Fact]
    public async Task CreateAsync_WithNullTools_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await ToolRouter.CreateAsync(null!));
    }

    [Fact]
    public async Task CreateAsync_WithEmptyTools_ThrowsArgumentException()
    {
        // Arrange
        var tools = Array.Empty<Tool>();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await ToolRouter.CreateAsync(tools));
    }

    #endregion

    #region RouteAsync Instance Tests

    [Fact]
    public async Task RouteAsync_WithSimplePrompt_ReturnsRelevantTools()
    {
        // Act
        var results = await _fixture.Router.RouteAsync("What's the weather?");

        // Assert
        Assert.NotEmpty(results);
        Assert.Equal("get_weather", results[0].Tool.Name);
    }

    [Fact]
    public async Task RouteAsync_WithTopK_LimitsResults()
    {
        // Act
        var results = await _fixture.Router.RouteAsync("search for tools", topK: 2);

        // Assert
        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task RouteAsync_WithMinScore_FiltersResults()
    {
        // Act — very high minScore should filter most results
        var results = await _fixture.Router.RouteAsync(
            "completely unrelated quantum physics theory", topK: 10, minScore: 0.9f);

        // Assert
        Assert.True(results.Count < _fixture.Tools.Length,
            "High minScore should filter out low-relevance results");
    }

    [Fact]
    public async Task RouteAsync_WithNullPrompt_ThrowsArgumentException()
    {
        // Act & Assert — null triggers ArgumentNullException (subclass of ArgumentException)
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await _fixture.Router.RouteAsync(null!));
    }

    #endregion

    #region Static One-Shot Tests

    [Fact]
    public async Task RouteAsync_StaticOneShot_ReturnsResults()
    {
        // Arrange
        var tools = new[]
        {
            new Tool { Name = "get_weather", Description = "Get current weather information for a location" },
            new Tool { Name = "send_email", Description = "Send an email message to a recipient" }
        };

        // Act
        var results = await ToolRouter.RouteAsync(tools, "What's the weather like?");

        // Assert
        Assert.NotEmpty(results);
        Assert.Equal("get_weather", results[0].Tool.Name);
    }

    #endregion

    #region Distillation Tests

    [Fact]
    public async Task RouteAsync_WithChatClient_DistillsPrompt()
    {
        // Arrange — FakeChatClient distills ambiguous prompt to "weather forecast"
        var chatClient = new FakeChatClient("weather forecast");
        await using var router = await ToolRouter.CreateAsync(_fixture.Tools, chatClient);

        // Act — vague prompt that after distillation becomes weather-related
        var results = await router.RouteAsync("I need to know about the atmospheric conditions");

        // Assert — weather tool should be top result after distillation
        Assert.NotEmpty(results);
        Assert.Equal("get_weather", results[0].Tool.Name);
    }

    [Fact]
    public async Task RouteAsync_WithNullChatClient_SkipsDistillation()
    {
        // Arrange — create router without chat client
        await using var router = await ToolRouter.CreateAsync(_fixture.Tools);

        // Act — should work fine without distillation
        var results = await router.RouteAsync("What's the weather?");

        // Assert
        Assert.NotEmpty(results);
    }

    [Fact]
    public async Task RouteAsync_WithDistillationDisabled_SkipsDistillation()
    {
        // Arrange — create router WITH chat client but disable distillation at creation time
        var chatClient = new FakeChatClient("this should not be used");
        var options = new ToolRouterOptions { EnableDistillation = false };
        await using var router = await ToolRouter.CreateAsync(_fixture.Tools, chatClient, options);

        // Act — distillation is disabled, so the original prompt is used for search
        var results = await router.RouteAsync("What's the weather?");

        // Assert — should still work, routing without distillation
        Assert.NotEmpty(results);
    }

    #endregion

    #region Property and Disposal Tests

    [Fact]
    public void Index_ReturnsUnderlyingToolIndex()
    {
        // Act
        var index = _fixture.Router.Index;

        // Assert
        Assert.NotNull(index);
        Assert.Equal(_fixture.Tools.Length, index.Count);
    }

    [Fact]
    public async Task DisposeAsync_CanBeCalledMultipleTimes()
    {
        // Arrange
        var tools = new[] { new Tool { Name = "test", Description = "Test tool" } };
        var router = await ToolRouter.CreateAsync(tools);

        // Act & Assert — should not throw on multiple dispose
        await router.DisposeAsync();
        await router.DisposeAsync();
    }

    #endregion
}
