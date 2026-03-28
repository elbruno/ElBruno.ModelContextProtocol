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

        public FakeChatClient(string response) => _response = response;

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken ct = default)
        {
            LastMessages = messages.ToList();
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, _response)));
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

    #endregion
}
