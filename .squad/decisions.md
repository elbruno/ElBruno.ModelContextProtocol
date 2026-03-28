# Squad Decisions

## Active Decisions

### 1. Single-Target Framework (net10.0 only)

**Date:** 2026-03-26  
**Agent:** Tron  
**Status:** Implemented

#### Context
ElBruno .NET conventions specify multi-targeting `net8.0;net10.0` for library projects to support both LTS and latest .NET versions.

#### Decision
MCPToolRouter library targets **net10.0 only** (single target).

#### Rationale
- **Dependency constraint:** ElBruno.LocalEmbeddings 1.1.5 (latest) only supports net10.0
- **No workaround available:** Cannot multi-target when critical dependency is single-platform
- **Trade-off accepted:** Narrower compatibility vs. using latest embedding library version

#### Impact
- Library requires .NET 10.0 SDK to build and run
- CI/CD pipelines must use .NET 10.0 SDK
- Consumers must target net10.0 or later
- May revisit when ElBruno.LocalEmbeddings adds net8.0 support

---

### 2. Shared Test Fixture for ToolIndex Tests

**Date:** 2026-03-26  
**Author:** Yori (Tester/QA)  
**Status:** Implemented

#### Context

The `ToolIndex.CreateAsync` method downloads a ~90MB ONNX embedding model on first use via `LocalEmbeddingGenerator`. Running 21 tests where each creates its own `ToolIndex` would result in:
- Repeated model downloads (or cache hits with delays)
- Longer test execution time
- Unnecessary resource consumption

#### Decision

Implement a shared test fixture using xUnit's `IClassFixture<T>` pattern:

```csharp
public class SharedToolIndexFixture : IAsyncLifetime
{
    public ToolIndex Index { get; private set; } = null!;
    public Tool[] Tools { get; } = new[] { /* 5 sample tools */ };
    
    public async Task InitializeAsync() => Index = await ToolIndex.CreateAsync(Tools);
    public async Task DisposeAsync() => await Index.DisposeAsync();
}
```

Tests that need a pre-built index use `IClassFixture<SharedToolIndexFixture>` and inject the fixture.

#### Consequences

**Positive:**
- Model downloads only once per test run
- Faster test execution (~8s vs potentially 60s+)
- Reduced resource consumption
- Tests still isolated (fixture provides read-only access)

**Negative:**
- Some tests that specifically test index creation cannot use the shared fixture
- Tests share the same 5 tools, requiring separate index creation for different tool sets

#### Implementation Notes

The following tests create their own index (cannot use shared fixture):
- `CreateAsync_WithNullTools_ThrowsArgumentNullException`
- `CreateAsync_WithEmptyTools_ThrowsArgumentException`
- `CreateAsync_WithSingleTool_ReturnsIndexWithCount1`
- `CreateAsync_WithMultipleTools_ReturnsCorrectCount`
- `CreateAsync_WithToolWithNoDescription_Succeeds`
- `SearchAsync_WithToolWithNoDescription_StillReturnsResults`
- `DisposeAsync_CanBeCalledMultipleTimes`

All other tests use the shared fixture for performance optimization.

---

### 3. Azure.AI.OpenAI SDK Usage

**Date:** 2026-03-26  
**Agent:** Tron  
**Status:** Implemented

#### Context

Sample applications (`TokenComparison` and `FilteredFunctionCalling`) needed to integrate with Azure OpenAI to demonstrate the MCPToolRouter library's token-saving capabilities.

#### Decision

Use `Azure.AI.OpenAI` 2.1.0 **directly** instead of the `Microsoft.Extensions.AI.OpenAI` abstraction layer.

#### Rationale

- **API Stability:** Azure.AI.OpenAI 2.1.0 has stable, well-documented APIs
- **Compatibility:** Microsoft.Extensions.AI.OpenAI had breaking API changes between versions (9.1.1 → 10.3.0)
- **Simplicity:** Direct SDK usage is more straightforward for sample code
- **Token Usage Access:** `ChatCompletion.Usage` properties directly accessible for measuring token savings
- **No Extra Dependencies:** Fewer packages to manage

#### API Pattern Used

```csharp
using Azure;
using Azure.AI.OpenAI;
using OpenAI.Chat;

var azureClient = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
var chatClient = azureClient.GetChatClient(deploymentName);

var options = new ChatCompletionOptions();
options.Tools.Add(ChatTool.CreateFunctionTool(name, description));

var response = await chatClient.CompleteChatAsync(
    [new UserChatMessage(userPrompt)],
    options);

var inputTokens = response.Value.Usage.InputTokenCount;
```

#### Impact

- Sample code is clearer and easier to understand
- Token usage measurements are straightforward
- No abstraction layer complexity for educational samples
- May need updates if Azure.AI.OpenAI 3.x introduces breaking changes (future)

#### Alternative Considered

Using `Microsoft.Extensions.AI.OpenAI` with `IChatClient` abstraction was attempted but:
- Breaking API changes between versions
- `AsBuilder()` and `UseFunctionInvocation()` methods not available
- Increased complexity for sample code
- Less direct access to token usage metrics

---

### 4. ToolRouter — LLM-Assisted Tool Routing with Fallback

**Date:** 2026-03-28  
**Agent:** Tron  
**Status:** Implemented

#### Context

The MCPToolRouter library offered only semantic search (embeddings-based). For complex scenarios where multiple tools have similar descriptions, semantic search alone can miss the intended tool. A secondary LLM-powered verification step was needed to improve routing accuracy.

#### Decision

Implement `ToolRouter` as a two-stage routing system:
1. **Stage 1 (Semantic):** Use embeddings-based search to find top-K candidates
2. **Stage 2 (LLM Verification):** Send candidates to LLM with distilled prompt for ranking/validation

The ToolRouter provides a `TryRouteAsync` method that combines both stages, with automatic fallback to semantic-only results if the LLM stage fails.

#### Implementation Details

- `PromptDistiller` class handles LLM-powered prompt analysis and fallback
- `ToolRouter` orchestrates semantic search + LLM verification flow
- `ToolRouterOptions` provides configuration (max results, retry behavior, LLM settings)
- `EmbeddingModelInfo` / `EmbeddingModelStatus` track model lifecycle
- FakeChatClient pattern used in tests for deterministic LLM behavior without network calls

#### Impact

- **New public API:** `IToolRouter`, `ToolRouter`, `PromptDistiller`, `ToolRouterOptions`
- **Integration:** ServiceCollectionExtensions added for DI support
- **Test Coverage:** 31 new tests (8 + 13 + 10) all passing
- **Backward Compatibility:** Existing ToolIndex API unchanged; ToolRouter is additive
- **Sample:** McpToolRouting demonstrates dual-mode usage

#### Rationale for Two-Mode Design

- **ToolIndex** alone: Fast, low-latency semantic filtering (good for high volume, latency-sensitive scenarios)
- **ToolRouter** (ToolIndex + LLM): Higher accuracy, good for ambiguous cases where semantic similarity alone is insufficient
- Users choose the appropriate mode based on their use case (accuracy vs. latency tradeoff)

---

### 5. Embedding Model Management

**Date:** 2026-03-28  
**Agent:** Tron  
**Status:** Implemented

#### Context

The library needed APIs to track and manage embedding model metadata (dimensions, status, provider) as the library expands to support multiple embedding generators (local, OpenAI, Azure OpenAI, Ollama).

#### Decision

Add `EmbeddingModelInfo` and `EmbeddingModelStatus` classes to encapsulate model metadata:

```csharp
public class EmbeddingModelInfo
{
    public string Provider { get; set; }
    public string ModelName { get; set; }
    public int EmbeddingDimension { get; set; }
    public EmbeddingModelStatus Status { get; set; }
}

public enum EmbeddingModelStatus
{
    NotInitialized = 0,
    Loading = 1,
    Ready = 2,
    Failed = 3
}
```

#### Impact

- Enables future `IToolIndex.GetModelInfoAsync()` API
- Prepares codebase for multi-model support
- Tracks model lifecycle (loading, ready, failed states)
- Documentation of model capabilities in API responses

---

### 6. FakeChatClient Test Pattern for LLM Tests

**Date:** 2026-03-28  
**Agent:** Yori  
**Status:** Implemented

#### Context

`PromptDistiller` and `ToolRouter` depend on `IChatClient` for LLM operations. Tests must avoid network calls, costs, and non-determinism.

#### Decision

Use private `FakeChatClient` class within test files that:
- Implements `IChatClient` with fixed responses
- Captures messages for assertion
- Provides deterministic behavior
- Requires no external mocking framework

```csharp
private class FakeChatClient : IChatClient
{
    private readonly string _response;
    public IList<ChatMessage>? LastMessages { get; private set; }
    
    public async Task<ChatCompletion> GetResponseAsync(IList<ChatMessage> messages, ...)
    {
        LastMessages = messages;
        return new ChatCompletion(_response);
    }
}
```

#### Impact

- All 8 PromptDistiller tests use FakeChatClient for fallback simulation
- All 13 ToolRouter tests use FakeChatClient for routing logic verification
- No external mocking dependencies (xUnit only)
- Pattern reusable for future IChatClient-dependent tests

---

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction
