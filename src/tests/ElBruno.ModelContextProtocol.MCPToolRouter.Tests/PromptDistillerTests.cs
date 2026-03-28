using Microsoft.Extensions.AI;
using Xunit;

namespace ElBruno.ModelContextProtocol.MCPToolRouter.Tests;

public class PromptDistillerTests
{
    #region Helper: Fake IChatClient

    /// <summary>
    /// A fake IChatClient that returns a predetermined response and captures the messages it receives.
    /// </summary>
    private class FakeChatClient : IChatClient
    {
        private readonly string _response;
        public IList<ChatMessage>? LastMessages { get; private set; }
        public ChatOptions? LastOptions { get; private set; }

        public FakeChatClient(string response) => _response = response;

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken ct = default)
        {
            LastMessages = messages.ToList();
            LastOptions = options;
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, _response)));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken ct = default)
            => throw new NotImplementedException();

        public void Dispose() { }
        public object? GetService(Type serviceType, object? key = null) => null;
    }

    /// <summary>
    /// A fake IChatClient that always throws an exception (simulates LLM failure).
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

    #region Valid Responses

    [Fact]
    public async Task DistillIntentAsync_WithValidPrompt_ReturnsDistilledIntent()
    {
        // Arrange
        var client = new FakeChatClient("Check weather in Tokyo");

        // Act
        var result = await PromptDistiller.DistillIntentAsync(client, "What is the weather like in Tokyo right now?");

        // Assert
        Assert.Equal("Check weather in Tokyo", result);
    }

    [Fact]
    public async Task DistillIntentAsync_ResponseIsTrimmed()
    {
        // Arrange
        var client = new FakeChatClient("  weather check  ");

        // Act
        var result = await PromptDistiller.DistillIntentAsync(client, "What is the weather forecast?");

        // Assert
        Assert.Equal("weather check", result);
    }

    #endregion

    #region Fallback to Original Prompt

    [Fact]
    public async Task DistillIntentAsync_WithEmptyResponse_FallsBackToOriginalPrompt()
    {
        // Arrange
        var originalPrompt = "Tell me the weather in Paris";
        var client = new FakeChatClient("");

        // Act
        var result = await PromptDistiller.DistillIntentAsync(client, originalPrompt);

        // Assert
        Assert.Equal(originalPrompt, result);
    }

    [Fact]
    public async Task DistillIntentAsync_WithShortResponse_FallsBackToOriginalPrompt()
    {
        // Arrange — "Hi" is < 5 chars, should fall back
        var originalPrompt = "Tell me the weather in London";
        var client = new FakeChatClient("Hi");

        // Act
        var result = await PromptDistiller.DistillIntentAsync(client, originalPrompt);

        // Assert
        Assert.Equal(originalPrompt, result);
    }

    #endregion

    #region Input Validation

    [Fact]
    public async Task DistillIntentAsync_WithNullClient_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await PromptDistiller.DistillIntentAsync(null!, "test prompt"));
    }

    [Fact]
    public async Task DistillIntentAsync_WithNullPrompt_ThrowsArgumentException()
    {
        // Arrange
        var client = new FakeChatClient("response");

        // Act & Assert — null triggers ArgumentNullException (subclass of ArgumentException)
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await PromptDistiller.DistillIntentAsync(client, null!));
    }

    [Fact]
    public async Task DistillIntentAsync_WithWhitespacePrompt_ThrowsArgumentException()
    {
        // Arrange
        var client = new FakeChatClient("response");

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await PromptDistiller.DistillIntentAsync(client, "   "));
    }

    #endregion

    #region Custom Options

    [Fact]
    public async Task DistillIntentAsync_WithCustomOptions_UsesCustomSystemPrompt()
    {
        // Arrange
        var client = new FakeChatClient("custom distilled intent");
        var customSystemPrompt = "You are a specialized intent extractor for weather queries.";
        var options = new PromptDistillerOptions { SystemPrompt = customSystemPrompt };

        // Act
        await PromptDistiller.DistillIntentAsync(client, "What is the weather?", options);

        // Assert — verify the system prompt was passed to the chat client
        Assert.NotNull(client.LastMessages);
        var systemMessage = client.LastMessages!.FirstOrDefault(m => m.Role == ChatRole.System);
        Assert.NotNull(systemMessage);
        Assert.Contains(customSystemPrompt, systemMessage.Text ?? string.Empty);
    }

    [Fact]
    public async Task DistillIntentAsync_PassesMaxOutputTokensAndTemperature()
    {
        // Arrange
        var client = new FakeChatClient("distilled result");
        var options = new PromptDistillerOptions
        {
            MaxOutputTokens = 64,
            Temperature = 0.5f
        };

        // Act
        await PromptDistiller.DistillIntentAsync(client, "Some long verbose prompt", options);

        // Assert — verify chat options were passed correctly
        Assert.NotNull(client.LastOptions);
        Assert.Equal(64, client.LastOptions!.MaxOutputTokens);
        Assert.Equal(0.5f, client.LastOptions.Temperature);
    }

    #endregion

    #region Exception Handling and Fallback

    [Fact]
    public async Task DistillIntentAsync_WithThrowingClient_FallsBackToOriginalPrompt()
    {
        // Arrange — client throws a generic exception (simulates ONNX token overflow, network error, etc.)
        var originalPrompt = "Tell me about the weather in multiple cities";
        var client = new ThrowingChatClient(new InvalidOperationException("input_ids size exceeds max length"));

        // Act — should not throw; falls back gracefully
        var result = await PromptDistiller.DistillIntentAsync(client, originalPrompt);

        // Assert — returns the original prompt as fallback
        Assert.Equal(originalPrompt, result);
        Assert.Equal(1, client.CallCount);
    }

    [Fact]
    public async Task DistillIntentAsync_WithThrowingClient_FallbackPreservesTruncatedPrompt()
    {
        // Arrange — long prompt + MaxPromptLength=50 + throwing client
        var originalPrompt = new string('A', 200);
        var client = new ThrowingChatClient();
        var options = new PromptDistillerOptions { MaxPromptLength = 50 };

        // Act
        var result = await PromptDistiller.DistillIntentAsync(client, originalPrompt, options);

        // Assert — falls back to the TRUNCATED prompt (not the full original)
        Assert.Equal(50, result.Length);
        Assert.Equal(originalPrompt[..50], result);
    }

    [Fact]
    public async Task DistillIntentAsync_WithCancellation_PropagatesCancellation()
    {
        // Arrange — client throws OperationCanceledException
        var client = new ThrowingChatClient(new OperationCanceledException("Cancelled by user"));

        // Act & Assert — OperationCanceledException should NOT be caught; it should propagate
        await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await PromptDistiller.DistillIntentAsync(client, "test prompt"));
    }

    [Fact]
    public async Task DistillIntentAsync_WithTaskCanceledException_PropagatesCancellation()
    {
        // Arrange — TaskCanceledException is a subclass of OperationCanceledException
        var client = new ThrowingChatClient(new TaskCanceledException("Task was cancelled"));

        // Act & Assert — should propagate, not be swallowed
        await Assert.ThrowsAsync<TaskCanceledException>(
            async () => await PromptDistiller.DistillIntentAsync(client, "test prompt"));
    }

    [Fact]
    public async Task DistillIntentAsync_WithNullResponseText_FallsBackToOriginalPrompt()
    {
        // Arrange — a client that returns null text (edge case in response parsing)
        var originalPrompt = "Get the weather forecast";
        // FakeChatClient with null would cause issues, so we test empty which has the same effect
        var client = new FakeChatClient("");

        // Act
        var result = await PromptDistiller.DistillIntentAsync(client, originalPrompt);

        // Assert
        Assert.Equal(originalPrompt, result);
    }

    #endregion

    #region Prompt Truncation

    [Fact]
    public async Task DistillIntentAsync_WithMaxPromptLength_TruncatesLongPrompt()
    {
        // Arrange — 500-char prompt with MaxPromptLength=100
        var longPrompt = new string('x', 100) + " weather forecast " + new string('y', 382);
        Assert.Equal(500, longPrompt.Length);
        var client = new FakeChatClient("weather forecast");
        var options = new PromptDistillerOptions { MaxPromptLength = 100 };

        // Act
        await PromptDistiller.DistillIntentAsync(client, longPrompt, options);

        // Assert — the user message sent to LLM should be truncated to 100 chars
        Assert.NotNull(client.LastMessages);
        var userMessage = client.LastMessages!.FirstOrDefault(m => m.Role == ChatRole.User);
        Assert.NotNull(userMessage);
        Assert.Equal(100, userMessage.Text!.Length);
    }

    [Fact]
    public async Task DistillIntentAsync_WithPromptUnderMaxLength_DoesNotTruncate()
    {
        // Arrange — 50-char prompt with MaxPromptLength=200
        var shortPrompt = "What is the weather in Tokyo right now?";
        var client = new FakeChatClient("weather in Tokyo");
        var options = new PromptDistillerOptions { MaxPromptLength = 200 };

        // Act
        await PromptDistiller.DistillIntentAsync(client, shortPrompt, options);

        // Assert — the full prompt is sent to LLM
        Assert.NotNull(client.LastMessages);
        var userMessage = client.LastMessages!.FirstOrDefault(m => m.Role == ChatRole.User);
        Assert.NotNull(userMessage);
        Assert.Equal(shortPrompt, userMessage.Text);
    }

    [Fact]
    public async Task DistillIntentAsync_WithMaxPromptLengthZero_DoesNotTruncate()
    {
        // Arrange — MaxPromptLength=0 disables truncation
        var longPrompt = new string('x', 1000);
        var client = new FakeChatClient("distilled");
        var options = new PromptDistillerOptions { MaxPromptLength = 0 };

        // Act
        await PromptDistiller.DistillIntentAsync(client, longPrompt, options);

        // Assert — full prompt is sent to LLM
        Assert.NotNull(client.LastMessages);
        var userMessage = client.LastMessages!.FirstOrDefault(m => m.Role == ChatRole.User);
        Assert.NotNull(userMessage);
        Assert.Equal(1000, userMessage.Text!.Length);
    }

    [Fact]
    public async Task DistillIntentAsync_WithNegativeMaxPromptLength_DoesNotTruncate()
    {
        // Arrange — negative MaxPromptLength disables truncation
        var longPrompt = new string('x', 500);
        var client = new FakeChatClient("distilled");
        var options = new PromptDistillerOptions { MaxPromptLength = -1 };

        // Act
        await PromptDistiller.DistillIntentAsync(client, longPrompt, options);

        // Assert — full prompt is sent to LLM
        Assert.NotNull(client.LastMessages);
        var userMessage = client.LastMessages!.FirstOrDefault(m => m.Role == ChatRole.User);
        Assert.NotNull(userMessage);
        Assert.Equal(500, userMessage.Text!.Length);
    }

    #endregion

    #region Options Defaults

    [Fact]
    public void PromptDistillerOptions_DefaultMaxPromptLength_Is300()
    {
        var options = new PromptDistillerOptions();
        Assert.Equal(300, options.MaxPromptLength);
    }

    [Fact]
    public void PromptDistillerOptions_DefaultMaxOutputTokens_Is128()
    {
        var options = new PromptDistillerOptions();
        Assert.Equal(128, options.MaxOutputTokens);
    }

    [Fact]
    public void PromptDistillerOptions_DefaultTemperature_Is01()
    {
        var options = new PromptDistillerOptions();
        Assert.Equal(0.1f, options.Temperature);
    }

    [Fact]
    public void PromptDistillerOptions_DefaultSystemPrompt_IsNotEmpty()
    {
        var options = new PromptDistillerOptions();
        Assert.False(string.IsNullOrWhiteSpace(options.SystemPrompt));
        Assert.Contains("intent", options.SystemPrompt, StringComparison.OrdinalIgnoreCase);
    }

    #endregion
}
