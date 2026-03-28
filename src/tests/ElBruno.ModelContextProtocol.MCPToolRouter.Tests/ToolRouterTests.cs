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
        public int CallCount { get; private set; }
        public IList<ChatMessage>? LastMessages { get; private set; }

        public FakeChatClient(string response) => _response = response;

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken ct = default)
        {
            CallCount++;
            LastMessages = messages.ToList();
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, _response)));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken ct = default)
            => throw new NotImplementedException();

        public void Dispose() { }
        public object? GetService(Type serviceType, object? key = null) => null;
    }

    /// <summary>
    /// A fake IChatClient that throws on every call (simulates LLM failure).
    /// </summary>
    private class ThrowingChatClient : IChatClient
    {
        private readonly Exception _exception;
        public int CallCount { get; private set; }

        public ThrowingChatClient(Exception? exception = null)
            => _exception = exception ?? new InvalidOperationException("LLM inference failed");

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken ct = default)
        {
            CallCount++;
            throw _exception;
        }

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

    #region Mode 1 vs Mode 2: Distillation Changes Results

    [Fact]
    public async Task Mode1VsMode2_WithSuccessfulDistillation_ProduceDifferentScores()
    {
        // Arrange — a set of tools where the distilled intent targets a different tool than the raw prompt
        var tools = new[]
        {
            new Tool { Name = "get_weather", Description = "Get current weather information for a location" },
            new Tool { Name = "send_email", Description = "Send an email message to a recipient" },
            new Tool { Name = "search_files", Description = "Search for files by name or content" },
            new Tool { Name = "calculate_math", Description = "Perform mathematical calculations" },
            new Tool { Name = "translate_text", Description = "Translate text between languages" }
        };

        // Vague prompt mentioning many topics but the LLM distills it to email-focused intent
        var vaguePrompt = "So I had this meeting yesterday and we talked about the quarterly numbers " +
                          "and someone mentioned the weather was terrible and I need to follow up with " +
                          "my colleague about the project status via electronic message";
        var chatClient = new FakeChatClient("send email to colleague about project status");

        // Act
        var mode1Results = await ToolRouter.SearchAsync(vaguePrompt, tools, topK: 5);
        var mode2Results = await ToolRouter.SearchUsingLLMAsync(vaguePrompt, tools, chatClient, topK: 5);

        // Assert — Mode 2 should use the distilled intent, producing different rankings
        Assert.NotEmpty(mode1Results);
        Assert.NotEmpty(mode2Results);

        // The top tool should differ because Mode 2 distills to "send email" intent
        // while Mode 1 uses the vague prompt which talks about many things
        Assert.Equal("send_email", mode2Results[0].Tool.Name);

        // Scores should be different (key assertion: proves Mode 2 isn't falling back)
        Assert.NotEqual(mode1Results[0].Score, mode2Results[0].Score);
    }

    [Fact]
    public async Task Mode1VsMode2_WithFailingLLM_ProduceIdenticalResults()
    {
        // Arrange — a throwing chat client simulates LLM failure (e.g., token overflow)
        var tools = new[]
        {
            new Tool { Name = "get_weather", Description = "Get current weather information for a location" },
            new Tool { Name = "send_email", Description = "Send an email message to a recipient" },
            new Tool { Name = "search_files", Description = "Search for files by name or content" }
        };
        var prompt = "What is the weather like today in Seattle?";
        var throwingClient = new ThrowingChatClient(
            new InvalidOperationException("input_ids size exceeds max length"));

        // Act
        var mode1Results = await ToolRouter.SearchAsync(prompt, tools, topK: 3);
        var mode2Results = await ToolRouter.SearchUsingLLMAsync(prompt, tools, throwingClient, topK: 3);

        // Assert — when distillation fails, Mode 2 falls back to original prompt = same as Mode 1
        Assert.Equal(mode1Results.Count, mode2Results.Count);
        for (int i = 0; i < mode1Results.Count; i++)
        {
            Assert.Equal(mode1Results[i].Tool.Name, mode2Results[i].Tool.Name);
            Assert.Equal(mode1Results[i].Score, mode2Results[i].Score);
        }
    }

    [Fact]
    public async Task RouteAsync_WithFailingChatClient_FallsBackToEmbeddingsOnly()
    {
        // Arrange — router with a throwing chat client
        var throwingClient = new ThrowingChatClient();
        await using var router = await ToolRouter.CreateAsync(_fixture.Tools, throwingClient);

        // Act — should not throw; falls back gracefully to embeddings-only search
        var results = await router.RouteAsync("What's the weather?");

        // Assert — still returns results (using original prompt for search)
        Assert.NotEmpty(results);
        Assert.Equal(1, throwingClient.CallCount);
    }

    [Fact]
    public async Task RouteAsync_DistilledPromptActuallyUsedForSearch()
    {
        // Arrange — FakeChatClient returns "mathematical calculations" for ANY prompt.
        // If the distilled text is used for search, calculate_math should rank highest.
        // If original prompt is used, get_weather should rank highest.
        var chatClient = new FakeChatClient("perform mathematical calculations and formulas");
        await using var router = await ToolRouter.CreateAsync(_fixture.Tools, chatClient);

        // Act — original prompt is weather-related, but LLM says it's math-related
        var results = await router.RouteAsync("What's the weather like outside today?");

        // Assert — math tool should be #1 because the DISTILLED text (not original) is used for search
        Assert.NotEmpty(results);
        Assert.Equal("calculate_math", results[0].Tool.Name);
    }

    [Fact]
    public async Task SearchUsingLLMAsync_WithFailingClient_ReturnsResultsGracefully()
    {
        // Arrange
        var tools = new[]
        {
            new Tool { Name = "get_weather", Description = "Get current weather information" },
            new Tool { Name = "send_email", Description = "Send email messages" }
        };
        var throwingClient = new ThrowingChatClient();

        // Act — should not throw; falls back to embeddings-only search
        var results = await ToolRouter.SearchUsingLLMAsync("weather forecast", tools, throwingClient);

        // Assert — returns results even though LLM failed
        Assert.NotEmpty(results);
    }

    #endregion

    #region ToolRouterOptions Defaults and Mapping

    [Fact]
    public void ToolRouterOptions_DefaultMaxPromptLength_Is300()
    {
        var options = new ToolRouterOptions();
        Assert.Equal(300, options.MaxPromptLength);
    }

    [Fact]
    public void ToolRouterOptions_DefaultEnableDistillation_IsTrue()
    {
        var options = new ToolRouterOptions();
        Assert.True(options.EnableDistillation);
    }

    [Fact]
    public void ToolRouterOptions_DefaultTopK_Is5()
    {
        var options = new ToolRouterOptions();
        Assert.Equal(5, options.TopK);
    }

    [Fact]
    public void ToolRouterOptions_DefaultMinScore_IsZero()
    {
        var options = new ToolRouterOptions();
        Assert.Equal(0.0f, options.MinScore);
    }

    [Fact]
    public void ToolRouterOptions_DefaultDistillationMaxOutputTokens_Is128()
    {
        var options = new ToolRouterOptions();
        Assert.Equal(128, options.DistillationMaxOutputTokens);
    }

    [Fact]
    public void ToolRouterOptions_DefaultDistillationTemperature_Is01()
    {
        var options = new ToolRouterOptions();
        Assert.Equal(0.1f, options.DistillationTemperature);
    }

    [Fact]
    public void ToolRouterOptions_ToDistillerOptions_MapsAllProperties()
    {
        // Arrange
        var routerOptions = new ToolRouterOptions
        {
            DistillationSystemPrompt = "Custom system prompt for tests",
            DistillationMaxOutputTokens = 256,
            DistillationTemperature = 0.5f,
            MaxPromptLength = 500
        };

        // Act
        var distillerOptions = routerOptions.ToDistillerOptions();

        // Assert — all properties are mapped correctly
        Assert.Equal("Custom system prompt for tests", distillerOptions.SystemPrompt);
        Assert.Equal(256, distillerOptions.MaxOutputTokens);
        Assert.Equal(0.5f, distillerOptions.Temperature);
        Assert.Equal(500, distillerOptions.MaxPromptLength);
    }

    [Fact]
    public void ToolRouterOptions_ToDistillerOptions_MapsDefaultValues()
    {
        // Arrange
        var routerOptions = new ToolRouterOptions();

        // Act
        var distillerOptions = routerOptions.ToDistillerOptions();

        // Assert — defaults are mapped (ToolRouterOptions.MaxPromptLength now aligned with PromptDistillerOptions)
        Assert.Equal(routerOptions.DistillationSystemPrompt, distillerOptions.SystemPrompt);
        Assert.Equal(128, distillerOptions.MaxOutputTokens);
        Assert.Equal(0.1f, distillerOptions.Temperature);
        Assert.Equal(300, distillerOptions.MaxPromptLength);
    }

    #endregion

    #region Distillation with Custom MaxPromptLength

    [Fact]
    public async Task SearchUsingLLMAsync_WithCustomMaxPromptLength_TruncatesBeforeDistillation()
    {
        // Arrange — create a long prompt and a FakeChatClient that captures what it receives
        var tools = new[]
        {
            new Tool { Name = "get_weather", Description = "Get current weather information" },
            new Tool { Name = "send_email", Description = "Send email messages" }
        };

        // 500-char prompt that should be truncated to 100 chars before LLM call
        var longPrompt = "weather " + new string('x', 492);
        Assert.Equal(500, longPrompt.Length);

        var chatClient = new FakeChatClient("weather forecast");
        var options = new ToolRouterOptions
        {
            MaxPromptLength = 100,
            UseSharedResources = false
        };

        // Act
        var results = await ToolRouter.SearchUsingLLMAsync(longPrompt, tools, chatClient, options: options);

        // Assert — the chat client should have received a truncated prompt
        Assert.NotNull(chatClient.LastMessages);
        var userMessage = chatClient.LastMessages!.FirstOrDefault(m => m.Role == ChatRole.User);
        Assert.NotNull(userMessage);
        Assert.Equal(100, userMessage.Text!.Length);
    }

    [Fact]
    public async Task RouteAsync_WithCustomMaxPromptLength_TruncatesBeforeDistillation()
    {
        // Arrange — router with MaxPromptLength=80
        var chatClient = new FakeChatClient("get weather forecast");
        var options = new ToolRouterOptions { MaxPromptLength = 80 };
        await using var router = await ToolRouter.CreateAsync(_fixture.Tools, chatClient, options);

        // A long prompt that exceeds 80 chars
        var longPrompt = "What is the weather " + new string('z', 100);
        Assert.True(longPrompt.Length > 80);

        // Act
        await router.RouteAsync(longPrompt);

        // Assert — the chat client should have received a truncated prompt
        Assert.NotNull(chatClient.LastMessages);
        var userMessage = chatClient.LastMessages!.FirstOrDefault(m => m.Role == ChatRole.User);
        Assert.NotNull(userMessage);
        Assert.Equal(80, userMessage.Text!.Length);
    }

    #endregion

    #region MaxPromptLength Regression Tests

    [Fact]
    public void ToolRouterOptions_MaxPromptLength_DefaultAlignedWithDistillerOptions()
    {
        // This test prevents the bug where ToolRouterOptions.MaxPromptLength defaulted to 4096
        // while PromptDistillerOptions.MaxPromptLength defaulted to 300, causing Mode 2
        // (LLM-distilled) to produce identical results to Mode 1 (embeddings-only) because
        // the local ONNX model silently failed on long prompts and fell back to the original.
        var routerOptions = new ToolRouterOptions();
        var distillerOptions = new PromptDistillerOptions();
        Assert.Equal(distillerOptions.MaxPromptLength, routerOptions.MaxPromptLength);
    }

    [Fact]
    public void ToDistillerOptions_MaxPromptLength_MappedCorrectly()
    {
        // Verify that ToDistillerOptions() maps MaxPromptLength from ToolRouterOptions
        // to PromptDistillerOptions correctly, both with default and custom values.

        // Test with default value
        var defaultRouterOptions = new ToolRouterOptions();
        var defaultDistillerOptions = defaultRouterOptions.ToDistillerOptions();
        Assert.Equal(300, defaultDistillerOptions.MaxPromptLength);

        // Test with custom value
        var customRouterOptions = new ToolRouterOptions { MaxPromptLength = 500 };
        var customDistillerOptions = customRouterOptions.ToDistillerOptions();
        Assert.Equal(500, customDistillerOptions.MaxPromptLength);
    }

    #endregion

    #region SearchAsync Mode 1 Does Not Distill

    [Fact]
    public async Task SearchAsync_DoesNotCallChatClient()
    {
        // Arrange — Mode 1 static API should never call an LLM
        // We verify this indirectly: SearchAsync doesn't accept a chatClient parameter
        // So we test that it produces consistent results without any LLM involvement
        var tools = new[]
        {
            new Tool { Name = "get_weather", Description = "Get current weather information" },
            new Tool { Name = "send_email", Description = "Send email messages" }
        };

        // Act — two calls with the same prompt should produce identical results
        var results1 = await ToolRouter.SearchAsync("weather forecast", tools);
        var results2 = await ToolRouter.SearchAsync("weather forecast", tools);

        // Assert — deterministic results (no LLM randomness)
        Assert.Equal(results1.Count, results2.Count);
        for (int i = 0; i < results1.Count; i++)
        {
            Assert.Equal(results1[i].Tool.Name, results2[i].Tool.Name);
            Assert.Equal(results1[i].Score, results2[i].Score);
        }
    }

    #endregion
}
