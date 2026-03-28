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

### 7. Simplified Static API for ToolRouter

**Date:** 2026-03-28  
**Author:** Flynn (Lead / Architect)  
**Status:** Implemented

#### Context

ToolRouter offered only the instance API (`CreateAsync` → `RouteAsync` → `DisposeAsync`). For simple use cases, this lifecycle is verbose. Users requested explicit static one-liners to reduce boilerplate while maintaining backend-agnostic design.

#### Decision

Add two static convenience methods alongside the existing instance API:

1. **`SearchAsync(userPrompt, tools, topK?, minScore?, options?, ct?)`** — Embeddings-only search (no LLM)
2. **`SearchUsingLLMAsync(userPrompt, tools, chatClient, topK?, minScore?, options?, ct?)`** — LLM-distilled search (requires user-provided `IChatClient`)

Parameter order: Prompt-first (reads naturally: "Search for *this* in *these tools*").

Delete the old ambiguous static `RouteAsync(tools, prompt, chatClient?)` method entirely (pre-1.0 allows breaking changes).

#### Rationale

- **Explicit over implicit:** Two methods with clear names eliminate guessing about distillation behavior
- **Backend-agnostic:** No hard dependency on `ElBruno.LocalLLMs`; users bring their own `IChatClient` (Azure OpenAI, Ollama, LocalLLMs, etc.)
- **Lean NuGet package:** 6/7 existing samples don't use LocalLLMs; proportionality favors user choice over bundled 500MB LLM models
- **Pre-release versioning:** v0.5.1 allows breaking changes per semver
- **Natural .NET pattern:** Matches `HttpClient.GetStringAsync()` (static one-liner) vs `new HttpClient()` (reused), `Regex.IsMatch()` vs `new Regex(pattern)`

#### Impact

- **New public APIs:** `SearchAsync`, `SearchUsingLLMAsync` (both static)
- **Breaking change:** Old static `RouteAsync` deleted
- **Instance API unchanged:** `CreateAsync` + instance `RouteAsync` continue to work identically
- **No new dependencies:** Library remains backend-agnostic
- **Test Coverage:** 8 new tests (5 SearchAsync + 3 SearchUsingLLMAsync); all 67 tests passing
- **Sample:** McpToolRouting demonstrates both modes

#### Dependency Analysis

| Factor | Assessment |
|--------|------------|
| **Add LocalLLMs hard dependency?** | **NO.** Pre-release risk (v0.5.0), disproportionate bloat for Mode 1 users, unnecessary coupling. |
| **Backend agnosticity** | ✅ Preserved via `IChatClient` abstraction. Users can choose LocalLLMs, Azure OpenAI, Ollama, Anthropic, etc. |
| **Zero-setup LocalLLMs pattern** | ✅ Documented but not bundled. Future `MCPToolRouter.LocalLLM` extension package option (defer to post-LocalLLMs v1.0). |

#### Usage Examples

```csharp
// Mode 1: Pure embeddings
var results = await ToolRouter.SearchAsync("weather", tools, topK: 3);

// Mode 2: LLM-distilled (any IChatClient)
var results = await ToolRouter.SearchUsingLLMAsync("weather", tools, chatClient, topK: 5);

// Mode 2b: LocalLLMs pattern (documented, not bundled)
using var localLLM = await LocalChatClient.CreateAsync(new LocalLLMsOptions { Model = ... });
var results = await ToolRouter.SearchUsingLLMAsync("prompt", tools, localLLM);

// Advanced: Reusable instance API (unchanged)
await using var router = await ToolRouter.CreateAsync(tools, chatClient, options);
var r1 = await router.RouteAsync("query 1");
var r2 = await router.RouteAsync("query 2");  // reuses embedding index
```

---

### 8. User Directive: v0.5.1 Allows Breaking Changes

**Date:** 2026-03-28T01:23:52Z  
**Author:** Bruno Capuano  
**Status:** Captured

#### Directive

We are still in v0.5.1 — breaking signature changes and major architectural changes are acceptable and unblock the simplified API redesign without backward-compatibility constraints.

---

### 9. Security & Performance Audit: 5-Phase Implementation Roadmap

**Date:** 2026-03-28  
**Authors:** Sark (Security), Tron (Performance), Flynn (Synthesis)  
**Status:** Audit Complete — Awaiting Team Consensus

#### Context

Comprehensive parallel audit of library identified security vulnerabilities (0 critical, 8 medium, 14 low) and performance bottlenecks (3 high-impact, 6 medium, 5 low). Three major findings require immediate P0 fixes: (1) QueryCacheSize is dead code despite being declared and tested, (2) Script injection vector in publish.yml CI workflow, (3) Race condition in concurrent DisposeAsync calls.

#### Audit Findings Summary

**Security (Sark):**
- Most Urgent: Script injection in `publish.yml` line 35-36 via unescaped `github.event.inputs.version` → Fix: use env vars
- Supply Chain: No `packages.lock.json` files → Fix: enable `RestorePackagesWithLockFile`
- Deserialization: `LoadAsync` has no bounds checks on `toolCount`, `embeddingDim`, `vectorLength` (OOM DoS risk) → Fix: add const limits
- Validation: Input validation solid across all public APIs; no SQL/shell injection vectors
- Secrets: Clean — no hardcoded credentials; samples use `dotnet user-secrets` correctly
- LLM: PromptDistiller inherent prompt injection risk (limited to tool *selection*, not execution) → Fix: document as known limitation, add 4096-char length limit
- CI/CD: GitHub Actions not SHA-pinned (mutation risk) → Fix: pin to full commit SHAs with tag comments

**Performance (Tron):**
- Most Critical: QueryCacheSize is dead code — declared in `ToolIndexOptions`, tested in 7 samples, but never implemented in `ToolIndex.SearchAsync` → Fix: implement LRU cache with `ConcurrentDictionary<string, float[]>`
- Bottleneck: Static API creates new ONNX session per call (200-500ms overhead) → Fix: shared `Lazy<Task<IEmbeddingGenerator>>` singleton
- Race Condition: `DisposeAsync` uses non-atomic `_disposed` bool check-and-set → Fix: `Interlocked.Exchange(ref _disposed, 1)`
- Architecture: No global ONNX session cache; multiple `ToolIndex` = multiple sessions (80-100MB each) → Fix: reference-counted singleton pool
- Efficiency: `SearchAsync` does full sort for top-K (O(N log N)) → Fix: use `PriorityQueue<int, float>` (O(N log K))
- Zero-Setup: `SearchUsingLLMAsync` creates `LocalChatClient` per call (500ms-2s overhead) → Fix: shared `Lazy<Task<LocalChatClient>>`
- DI: `AddMcpToolRouter` uses `.GetAwaiter().GetResult()` (blocks threadpool) → Fix: `AddMcpToolRouterAsync` with `IHostedService`
- Persistence: No auto-caching of computed indices → Future: `AutoPersistPath` option with SHA256 validation
- Format: Binary save/load loses `InputSchema` and other `Tool` properties → Future: Format v2 with JSON serialization

#### Decision: 5-Phase Implementation Plan

**Phase 1 — Critical Fixes (P0, Immediate)**
- Item 1.1: Implement QueryCacheSize LRU cache (Tron)
- Item 1.2: Fix script injection in publish.yml (Sark) — use env vars
- Item 1.3: Fix DisposeAsync race (Tron) — Interlocked.Exchange
- **Impact:** Unblocks Phase 2, eliminates documented-but-broken feature

**Phase 2 — High-Impact Performance (Sprint 2)**
- Item 2.1: Shared embedding generator for static API (Tron) — Lazy<Task<IEmbeddingGenerator>>
- Item 2.2: Shared LocalChatClient for zero-setup LLM (Tron) — same pattern
- Item 2.3: LoadAsync bounds checking (Sark) — const limits on toolCount, embeddingDim
- Item 2.4: PriorityQueue for top-K search (Tron) — O(N log K) instead of O(N log N)
- **Impact:** 15-35× speedup for static API; DoS mitigation

**Phase 3 — Security Hardening + CI (Sprint 3)**
- Item 3.1: Enable NuGet lock files (Sark) — add to Directory.Build.props
- Item 3.2: SHA-pin GitHub Actions (Sark) — replace @v tags with full SHA
- Item 3.3: Model name path traversal guard (Sark) — validate against ".." and absolute paths
- Item 3.4: Prompt length limit (Tron) — add MaxInputLength to PromptDistillerOptions (default 4096)
- Item 3.5: Migrate ReaderWriterLockSlim to SemaphoreSlim (Tron) — async-safe alternative
- **Impact:** Supply chain hardening; eliminates thread-safety fragility

**Phase 4 — Advanced Optimizations (Sprint 4, can defer)**
- Item 4.1: AutoPersistPath for index caching (Tron) — skip re-embedding on restart
- Item 4.2: Save format v2 with InputSchema (Tron) — JSON serialization with v1 fallback
- Item 4.3: Block I/O for save/load (Tron) — MemoryMarshal.AsBytes for 2-5× speedup
- Item 4.4: AddMcpToolRouterAsync with IHostedService (Tron) — non-blocking DI registration
- Item 4.5: Memory spike guard for concurrent static calls (Tron) — SemaphoreSlim limiter if no Item 2.1
- **Impact:** ~50-200ms savings per restart; nice-to-haves

**Phase 5 — Documentation & Monitoring (Ongoing)**
- Item 5.1: Performance guide (Ram) — static vs instance tradeoffs
- Item 5.2: Prompt injection limitation (Ram) — security considerations section
- Item 5.3: Sync-over-async DI limitation (Ram) — xmldoc warning + README note
- Item 5.4: Add `dotnet nuget audit` to CI (Sark) — transitive dependency scanning
- Item 5.5: Model download security guide (Ram) — recommended verification steps
- **Impact:** Users understand performance characteristics; proactive security guidance

#### Assignment

| Agent | Phase 1 | Phase 2 | Phase 3 | Phase 4 | Phase 5 |
|-------|---------|---------|---------|---------|---------|
| **Tron** (Core) | 1.1, 1.3 | 2.1, 2.2, 2.4 | 3.4, 3.5 | 4.1-4.5 | — |
| **Sark** (Security) | 1.2 | 2.3 | 3.1-3.3 | — | 5.4 |
| **Yori** (QA) | Tests for all items | Tests for all items | Tests for all items | Tests for all items | — |
| **Ram** (Docs) | — | — | — | — | 5.1-5.3, 5.5 |
| **Flynn** (Lead) | — | Review 2.1, 2.2 | — | Review 4.4 | — |

#### Risk Assessment

| Risk | Mitigation |
|------|-----------|
| Shared ONNX singleton memory leak | WeakReference or reference counting; document lifecycle clearly |
| LRU cache invalidation on tool modifications | Clear cache in AddToolsAsync/RemoveTools; add regression tests |
| NuGet lock files cause CI failures | Document `dotnet restore --force-evaluate` workflow; enable Dependabot |
| SHA-pinned actions fall behind security patches | Add Dependabot `github-actions` ecosystem config |
| Format v2 breaks existing .bin files | Implement v1→v2 migration in reader; log warning on v1 load |

#### Success Metrics

| Phase | Metric | Target |
|-------|--------|--------|
| **Phase 1** | QueryCacheSize cache hit rate | > 99% for identical prompts; ~0ms cached vs ~10-20ms uncached |
| **Phase 1** | publish.yml GitHub Security Advisory scan | 0 script injection findings |
| **Phase 2** | Static API cold start (with cache) | < 100ms for repeat calls |
| **Phase 2** | Zero-setup LLM repeat calls | < 500ms (from ~1-3.5s) |
| **Phase 3** | Reproducible builds | `packages.lock.json` committed; CI uses `--locked-mode` |
| **Phase 3** | Action pinning | `grep '@v' .github/workflows/*.yml` returns 0 |
| **Phase 4** | Auto-persist performance | App restart skips embedding generation |
| **Phase 5** | Documentation completeness | Users stop creating "SearchAsync is slow" issues |

#### Architectural Decisions

1. **Shared ONNX Singleton:** Use `static Lazy<Task<IEmbeddingGenerator>>` (thread-safe initialization) with `ToolRouter.ResetSharedGenerator()` escape hatch for testing. Reference counting not required (single-model scenario).

2. **Shared LocalChatClient:** Apply same `Lazy<Task<>>` pattern as Item 2.1. Document lifecycle: "Lives for process duration. Call `ToolRouter.ResetSharedResources()` to release."

3. **Cache Invalidation:** LRU cache backed by `ConcurrentDictionary<string, float[]>` with FIFO eviction when size exceeds `QueryCacheSize`. Clear cache in `AddToolsAsync`, `RemoveTools`, and `DisposeAsync`.

4. **SemaphoreSlim Migration:** Replace `ReaderWriterLockSlim` with `SemaphoreSlim(1, 1)` for write-heavy workload. Read concurrency sacrifice acceptable for < 10,000 tools where search is < 1ms.

5. **Format v2 Backward Compatibility:** Write v2 (with JSON Tool metadata), read both v1 and v2. V1 reader persists only Name/Description; v2 reader fully restores Tool. Log warning when loading v1.

#### Open Questions for Team

- Should Phase 4 (advanced opt) be deferred to v0.6.0 / v1.0.0 planning, or run in parallel with Phase 3?
- For Item 4.4 (AddMcpToolRouterAsync), is the `IHostedService` pattern acceptable for early-stage consumers?
- Should we add Dependabot configuration now, or as part of Phase 3.2?

---

### 10. DirectML GPU Runtime for Sample Projects

**Date:** 2026-03-28  
**Author:** Tron (Core Developer)  
**Status:** ✅ Implemented

#### Context

Two sample projects (`LLMDistillationMax` and `LLMDistillationDemo`) were using the CPU-only ONNX GenAI runtime package (`Microsoft.ML.OnnxRuntimeGenAI`). The ElBruno.LocalLLMs library (v0.7.1) already defaults to `ExecutionProvider.Auto`, which attempts GPU acceleration and falls back to CPU, but the samples with direct ONNX dependencies weren't benefiting from GPU acceleration.

#### Decision

**Migrated sample projects from CPU to DirectML GPU runtime:**
- Replaced `Microsoft.ML.OnnxRuntimeGenAI` v0.12.2 with `Microsoft.ML.OnnxRuntimeGenAI.DirectML` v0.12.2
- Changed projects:
  - `src/samples/LLMDistillationMax/LLMDistillationMax.csproj`
  - `src/samples/LLMDistillationDemo/LLMDistillationDemo.csproj`

#### Verification

- Scanned all 9 sample projects — only 2 had direct ONNX references
- Other samples use ElBruno.LocalLLMs indirectly (already GPU-capable)
- No code changes needed (library handles ExecutionProvider.Auto)

#### Alternatives Considered

1. **Add both CPU and DirectML packages** — Rejected (package conflict)
2. **Only use CPU package** — Rejected (poor performance on GPU-capable Windows machines)
3. **Add platform-specific conditionals** — Rejected (DirectML already handles GPU detection and fallback)

#### Impact

- ✅ **Performance:** Samples now leverage GPU acceleration on Windows (AMD/Intel/NVIDIA)
- ✅ **Compatibility:** DirectML auto-detects GPU capability and falls back to CPU if needed
- ✅ **Build:** Solution builds cleanly (net8.0 + net10.0)
- ✅ **Tests:** All 115 unit tests pass

#### Trade-offs

- **Pros:**
  - Significant performance improvement for distillation workflows
  - No code changes required (transparent upgrade)
  - Aligns with library's ExecutionProvider.Auto strategy
- **Cons:**
  - DirectML is Windows-only (Linux/macOS users would need CPU package)
  - Slightly larger package download (~DirectML includes GPU shaders)

---

### 11. Revert ModelInfo Auto-Detection for Prompt Truncation

**Author:** Tron (Core Dev)  
**Date:** 2026-03-28  
**Status:** ✅ Implemented

#### Context

In v0.6.1, auto-detection logic read `ModelInfo.MaxSequenceLength` from `LocalChatClient` and used it to dynamically compute the safe prompt length for distillation. The intent was to eliminate manual tuning — the library would auto-adapt to whatever model was loaded.

#### Problem

`genai_config.json` for Phi-3.5 reports `MaxSequenceLength = 131072` (theoretical context window), but the actual ONNX runtime compiled model has a much smaller limit (~128 tokens). The auto-detection trusted the config value, computed `(131072 - 70) * 4 = 524,008` safe characters, so prompts were never truncated. The LLM call then failed with token overflow, the try-catch caught the error, and fell back to the original prompt — making Mode 2 (LLM-distilled) identical to Mode 1 (embeddings-only). Distillation was silently broken.

#### Decision

Revert all auto-detection logic. Restore the conservative 300-character default for `PromptDistillerOptions.MaxPromptLength`. Keep `ToolRouterOptions.MaxPromptLength` at 4096 (suitable for cloud LLMs). Model metadata display in samples is preserved for diagnostics.

#### Rationale

- Model config files are unreliable for runtime limits — the compiled ONNX model may have different constraints than what the config advertises.
- A conservative hardcoded default (300 chars) is safer and predictable. Users can still override it.
- Auto-detection that silently fails is worse than no auto-detection — it creates a false sense of correctness.
- The try-catch safety net is good for resilience but should not be the primary truncation mechanism.

#### Impact

- `PromptDistillerOptions.MaxPromptLength` default: 4096 → 300
- Removed: `ModelMaxSequenceLength`, `DetectedModelMaxSequenceLength`, `_sharedModelMaxSequenceLength`, auto-compute logic, LogMessage 202
- No breaking API changes (removed properties were internal or newly added)
- All 85 tests pass

---

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction
