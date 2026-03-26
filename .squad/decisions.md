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

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction
