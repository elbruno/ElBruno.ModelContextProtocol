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

    #region SearchAsync Static Tests (Mode 1: Embeddings-only)

    [Fact]
    public async Task SearchAsync_ReturnsRelevantTools()
    {
        // Arrange
        var tools = new[]
        {
            new Tool { Name = "get_weather", Description = "Get current weather information for a location" },
            new Tool { Name = "send_email", Description = "Send an email message to a recipient" }
        };

        // Act
        var results = await ToolRouter.SearchAsync("What's the weather like?", tools);

        // Assert
        Assert.NotEmpty(results);
        Assert.Equal("get_weather", results[0].Tool.Name);
    }

    [Fact]
    public async Task SearchAsync_RespectsTopK()
    {
        // Arrange
        var tools = new[]
        {
            new Tool { Name = "get_weather", Description = "Get current weather information for a location" },
            new Tool { Name = "send_email", Description = "Send an email message to a recipient" },
            new Tool { Name = "search_files", Description = "Search for files by name or content" },
            new Tool { Name = "calculate_math", Description = "Perform mathematical calculations" },
            new Tool { Name = "translate_text", Description = "Translate text between languages" }
        };

        // Act
        var results = await ToolRouter.SearchAsync("find something useful", tools, topK: 2);

        // Assert
        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task SearchAsync_WithOptions_UsesCustomSettings()
    {
        // Arrange
        var tools = new[]
        {
            new Tool { Name = "get_weather", Description = "Get current weather information for a location" },
            new Tool { Name = "send_email", Description = "Send an email message to a recipient" }
        };
        var options = new ToolRouterOptions { TopK = 1, MinScore = 0.0f };

        // Act
        var results = await ToolRouter.SearchAsync("weather forecast", tools, options: options);

        // Assert
        Assert.NotEmpty(results);
    }

    [Fact]
    public async Task SearchAsync_NullPrompt_Throws()
    {
        // Arrange
        var tools = new[]
        {
            new Tool { Name = "test_tool", Description = "A test tool" }
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await ToolRouter.SearchAsync(null!, tools));
    }

    [Fact]
    public async Task SearchAsync_NullTools_Throws()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await ToolRouter.SearchAsync("test prompt", null!));
    }

    #endregion

    #region SearchUsingLLMAsync Static Tests (Mode 2: LLM-distilled)

    [Fact]
    public async Task SearchUsingLLMAsync_WithFakeChatClient_ReturnsResults()
    {
        // Arrange — LLM distills the prompt to something weather-related
        var tools = new[]
        {
            new Tool { Name = "get_weather", Description = "Get current weather information for a location" },
            new Tool { Name = "send_email", Description = "Send an email message to a recipient" }
        };
        var chatClient = new FakeChatClient("weather forecast for today");

        // Act
        var results = await ToolRouter.SearchUsingLLMAsync(
            "What's the atmospheric condition outside?", tools, chatClient);

        // Assert
        Assert.NotEmpty(results);
        Assert.Equal("get_weather", results[0].Tool.Name);
    }

    [Fact]
    public async Task SearchUsingLLMAsync_NullChatClient_Throws()
    {
        // Arrange
        var tools = new[]
        {
            new Tool { Name = "test_tool", Description = "A test tool" }
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await ToolRouter.SearchUsingLLMAsync("test prompt", tools, null!));
    }

    [Fact]
    public async Task SearchUsingLLMAsync_DistillsPrompt()
    {
        // Arrange — The fake LLM will "distill" the vague prompt to "send email message"
        // which should make the email tool rank higher than with the raw prompt
        var tools = new[]
        {
            new Tool { Name = "get_weather", Description = "Get current weather information for a location" },
            new Tool { Name = "send_email", Description = "Send an email message to a recipient" },
            new Tool { Name = "search_files", Description = "Search for files by name or content" }
        };
        var chatClient = new FakeChatClient("send email message to someone");

        // Act — vague prompt that the LLM distills to email-related intent
        var results = await ToolRouter.SearchUsingLLMAsync(
            "I need to communicate with my colleague electronically", tools, chatClient);

        // Assert — email tool should rank first because the distilled prompt is email-specific
        Assert.NotEmpty(results);
        Assert.Equal("send_email", results[0].Tool.Name);
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

    #region Concurrent Dispose Tests

    [Fact]
    public async Task ToolRouter_ConcurrentDispose_DoesNotThrow()
    {
        // Arrange — create a fresh router (not the shared fixture)
        var tools = new[] { new Tool { Name = "test_tool", Description = "A test tool" } };
        var router = await ToolRouter.CreateAsync(tools);

        // Act — fire 10 concurrent DisposeAsync calls
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => router.DisposeAsync().AsTask())
            .ToArray();

        // Assert — no exceptions from concurrent disposal
        var exception = await Record.ExceptionAsync(() => Task.WhenAll(tasks));
        Assert.Null(exception);
    }

    [Fact]
    public async Task ToolRouter_DoubleDispose_DoesNotThrow()
    {
        // Arrange
        var tools = new[] { new Tool { Name = "test_tool", Description = "A test tool" } };
        var router = await ToolRouter.CreateAsync(tools);

        // Act & Assert — sequential double dispose must be safe
        await router.DisposeAsync();
        var exception = await Record.ExceptionAsync(async () => await router.DisposeAsync());
        Assert.Null(exception);
    }

    #endregion

    #region Shared Singleton Tests (Phase 2)

    [Fact]
    public async Task StaticSearch_WithSharedResources_ReturnsResults()
    {
        try
        {
            var tools = new[]
            {
                new Tool { Name = "get_weather", Description = "Get current weather information for a location" },
                new Tool { Name = "send_email", Description = "Send an email message to a recipient" }
            };

            var results = await ToolRouter.SearchAsync("What's the weather?", tools);

            Assert.NotEmpty(results);
            Assert.Equal("get_weather", results[0].Tool.Name);
        }
        finally
        {
            await ToolRouter.ResetSharedResourcesAsync();
        }
    }

    [Fact]
    public async Task StaticSearch_CalledTwice_ProducesConsistentResults()
    {
        try
        {
            var tools = new[]
            {
                new Tool { Name = "get_weather", Description = "Get current weather information" },
                new Tool { Name = "send_email", Description = "Send email messages" }
            };

            var results1 = await ToolRouter.SearchAsync("weather forecast", tools);
            var results2 = await ToolRouter.SearchAsync("weather forecast", tools);

            Assert.Equal(results1.Count, results2.Count);
            for (int i = 0; i < results1.Count; i++)
            {
                Assert.Equal(results1[i].Tool.Name, results2[i].Tool.Name);
                Assert.Equal(results1[i].Score, results2[i].Score);
            }
        }
        finally
        {
            await ToolRouter.ResetSharedResourcesAsync();
        }
    }

    [Fact]
    public async Task ResetSharedResources_DoesNotThrow()
    {
        var exception = await Record.ExceptionAsync(() => ToolRouter.ResetSharedResourcesAsync());
        Assert.Null(exception);
    }

    [Fact]
    public async Task ResetSharedResources_ThenSearch_StillWorks()
    {
        try
        {
            var tools = new[]
            {
                new Tool { Name = "get_weather", Description = "Get current weather information" },
                new Tool { Name = "send_email", Description = "Send email messages" }
            };

            // First search to populate shared resources
            await ToolRouter.SearchAsync("weather", tools);

            // Reset shared resources
            await ToolRouter.ResetSharedResourcesAsync();

            // Search again — should recreate shared resources and still work
            var results = await ToolRouter.SearchAsync("weather forecast", tools);

            Assert.NotEmpty(results);
            Assert.Equal("get_weather", results[0].Tool.Name);
        }
        finally
        {
            await ToolRouter.ResetSharedResourcesAsync();
        }
    }

    [Fact]
    public async Task StaticSearch_WithUseSharedResourcesFalse_StillWorks()
    {
        var tools = new[]
        {
            new Tool { Name = "get_weather", Description = "Get current weather information" },
            new Tool { Name = "send_email", Description = "Send email messages" }
        };
        var options = new ToolRouterOptions { UseSharedResources = false };

        var results = await ToolRouter.SearchAsync("weather forecast", tools, options: options);

        Assert.NotEmpty(results);
        Assert.Equal("get_weather", results[0].Tool.Name);
    }

    #endregion
}
