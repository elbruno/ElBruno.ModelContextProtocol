# 🏗️ MCPToolRouter — Improvement Analysis & Sample Proposals

**Author:** Flynn (Lead/Architect)  
**Date:** 2025-07-24  
**Status:** Proposal  

---

## Executive Summary

MCPToolRouter has a clean, focused design: ingest MCP tool definitions, embed locally, return top-K by cosine similarity. The current implementation is correct and well-tested (21 xUnit tests with shared fixture). Below I identify **high-impact improvements** grouped by category, and propose **5 new samples** that would drive adoption in the .NET AI community.

---

## Part 1: Library Improvement Suggestions

### 🔴 P0 — Performance (Ship-blocking for scale)

#### 1.1 Eliminate Per-Search Allocations in CosineSimilarity

**Current:** `SearchAsync` calls `.ToArray()` on every `Embedding<float>.Vector` for every tool on every search — allocating `O(n × dim)` floats per query.

**Proposed:** Pre-extract `float[]` vectors at index creation time and store them. Use `ReadOnlySpan<float>` in the similarity calculation to avoid all per-search heap allocation.

```csharp
// At creation time, store pre-extracted vectors
private readonly float[][] _vectors; // extracted once in CreateAsync

private static float CosineSimilarity(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
{
    // SIMD-friendly, zero-allocation
}
```

**Impact:** Eliminates thousands of temporary arrays per search. For 100 tools × 384-dim embeddings, that's ~150KB of garbage per query eliminated.  
**Complexity:** Simple  
**Risk:** None — internal change, public API unchanged

#### 1.2 SIMD-Accelerated Cosine Similarity

**Current:** Scalar loop for dot product / magnitude.

**Proposed:** Use `System.Numerics.Vector<float>` or `System.Runtime.Intrinsics` for SIMD-accelerated cosine similarity. On .NET 10 with AVX-512, this can process 16 floats per instruction.

```csharp
using System.Numerics;

private static float CosineSimilarity(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
{
    // Process Vector<float>.Count elements at a time
    int vectorSize = Vector<float>.Count;
    // ... SIMD loop with Vector.Dot, then scalar remainder
}
```

**Impact:** 4-16x speedup on cosine similarity computation.  
**Complexity:** Medium  
**Risk:** Low — well-understood optimization, extensive SIMD support in .NET 10

#### 1.3 Batch Query Embedding

**Current:** Each `SearchAsync` call generates one embedding independently.

**Proposed:** Add `SearchBatchAsync(IEnumerable<string> prompts, ...)` that batches multiple queries into a single embedding generation call. The ONNX runtime is much more efficient with batched inference.

```csharp
public async Task<IReadOnlyList<IReadOnlyList<ToolSearchResult>>> SearchBatchAsync(
    IEnumerable<string> prompts,
    int topK = 5,
    float minScore = 0.0f,
    CancellationToken cancellationToken = default)
```

**Impact:** Significant latency reduction for multi-query scenarios (e.g., routing multiple user turns).  
**Complexity:** Medium  
**Risk:** Low

---

### 🟡 P1 — Caching & Persistence

#### 1.4 Index Serialization (Save/Load)

**Current:** Index must be rebuilt from scratch every time. Samples like `TokenComparison` and `FilteredFunctionCalling` call `ToolIndex.CreateAsync` inside every loop iteration — downloading/loading the ONNX model and re-embedding all tools each time.

**Proposed:** Add `SaveAsync` / `LoadAsync` for persisting the embedded index to disk. This enables warm-start scenarios and avoids redundant embedding generation.

```csharp
// Save the index (embeddings + tool metadata) to a stream
public async Task SaveAsync(Stream stream, CancellationToken cancellationToken = default);

// Load a previously saved index
public static async Task<ToolIndex> LoadAsync(
    Stream stream,
    LocalEmbeddingsOptions? embeddingOptions = null,
    CancellationToken cancellationToken = default);
```

**Impact:** Dramatically faster startup for applications with stable tool sets. First-class scenario for MCP servers that don't change frequently.  
**Complexity:** Medium  
**Risk:** Low — serialization format needs versioning

#### 1.5 Query Embedding Cache (LRU)

**Current:** Same query re-embeds every time.

**Proposed:** Optional in-memory LRU cache for query embeddings. Repeated or similar queries (e.g., in chat loops) skip embedding generation entirely.

```csharp
public static async Task<ToolIndex> CreateAsync(
    IEnumerable<Tool> tools,
    ToolIndexOptions? options = null,  // includes CacheSize, etc.
    CancellationToken cancellationToken = default)
```

**Impact:** Sub-millisecond search for cached queries.  
**Complexity:** Simple  
**Risk:** Low — opt-in behavior

---

### 🟢 P2 — API Enhancements

#### 1.6 Builder Pattern / Options Object

**Current:** `CreateAsync` has a flat parameter list. As we add features (cache, persistence, logging), the parameter list will grow.

**Proposed:** Introduce `ToolIndexOptions` to group configuration:

```csharp
public sealed class ToolIndexOptions
{
    public LocalEmbeddingsOptions? EmbeddingOptions { get; set; }
    public int QueryCacheSize { get; set; } = 0; // 0 = disabled
    public ILogger? Logger { get; set; }
    public string? EmbeddingTextTemplate { get; set; } = "{Name}: {Description}";
}
```

**Impact:** Clean, extensible API surface. Follows .NET Options pattern.  
**Complexity:** Simple  
**Risk:** None — additive, backward compatible via default values

#### 1.7 AddTools / RemoveTools (Dynamic Index)

**Current:** Index is immutable after creation.

**Proposed:** Allow incremental updates — adding/removing tools without rebuilding the entire index. Critical for long-running applications where MCP servers connect/disconnect.

```csharp
public async Task AddToolsAsync(IEnumerable<Tool> tools, CancellationToken cancellationToken = default);
public void RemoveTools(IEnumerable<string> toolNames);
```

**Impact:** Enables dynamic tool registration in servers and long-running apps.  
**Complexity:** Medium  
**Risk:** Medium — concurrency needs careful handling (consider `ReaderWriterLockSlim`)

#### 1.8 IAsyncEnumerable Streaming Results

**Current:** Returns `IReadOnlyList<ToolSearchResult>` — must compute all results before returning.

**Proposed:** Add streaming overload for lazy evaluation:

```csharp
public async IAsyncEnumerable<ToolSearchResult> SearchStreamAsync(
    string prompt,
    int topK = 5,
    float minScore = 0.0f,
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
```

**Impact:** Enables early termination and progressive rendering. Follows modern .NET async patterns.  
**Complexity:** Simple  
**Risk:** Low

#### 1.9 Tool Tags / Categories

**Current:** Search is purely semantic on `Name: Description`.

**Proposed:** Allow tools to carry metadata (tags, categories) that can be used as pre-filters before semantic search. This improves precision when tools span different domains.

```csharp
var results = await index.SearchAsync("send notification", 
    topK: 5, 
    filter: t => t.Annotations?.ContainsKey("category") == true 
                 && t.Annotations["category"] == "communication");
```

**Impact:** Hybrid search (metadata + semantic) is a proven pattern from vector DB ecosystems.  
**Complexity:** Medium  
**Risk:** Low

---

### 🔵 P3 — Integration & Ecosystem

#### 1.10 ASP.NET Core DI / IServiceCollection Extensions

**Current:** No DI integration. Users must manage `ToolIndex` lifecycle manually.

**Proposed:** Ship a companion package `ElBruno.ModelContextProtocol.MCPToolRouter.Extensions.DependencyInjection` (or add extension methods to the main package):

```csharp
services.AddMcpToolRouter(options =>
{
    options.EmbeddingOptions = new LocalEmbeddingsOptions { ... };
    options.QueryCacheSize = 100;
});

// Inject IToolIndex in controllers/services
public class ChatController(IToolIndex toolIndex) { ... }
```

**Impact:** First-class citizen in ASP.NET Core apps. Enables singleton lifecycle management.  
**Complexity:** Medium  
**Risk:** Low

#### 1.11 Direct MCP Server Integration

**Current:** Users manually extract `Tool[]` from MCP servers and pass them in.

**Proposed:** Add extension methods that accept `IMcpClient` directly:

```csharp
// Pull tools directly from an MCP client
var client = await McpClientFactory.CreateAsync(transport);
await using var index = await ToolIndex.CreateFromClientAsync(client);
```

**Impact:** Reduces boilerplate. The "happy path" for anyone connecting to MCP servers.  
**Complexity:** Simple  
**Risk:** Low — depends on `ModelContextProtocol` SDK stable API

#### 1.12 IEmbeddingGenerator<string, Embedding<float>> Abstraction

**Current:** Hard-coupled to `ElBruno.LocalEmbeddings`.

**Proposed:** Accept the `Microsoft.Extensions.AI.IEmbeddingGenerator<string, Embedding<float>>` interface. This allows users to plug in OpenAI embeddings, Azure OpenAI, Ollama, or any provider.

```csharp
public static async Task<ToolIndex> CreateAsync(
    IEnumerable<Tool> tools,
    IEmbeddingGenerator<string, Embedding<float>>? embeddingGenerator = null,
    ToolIndexOptions? options = null,
    CancellationToken cancellationToken = default)
```

**Impact:** Massive — opens the library to the entire Microsoft.Extensions.AI ecosystem. Users not on .NET 10 could use cloud embeddings.  
**Complexity:** Medium  
**Risk:** Low — `IEmbeddingGenerator` is a stable abstraction in Microsoft.Extensions.AI

---

### 🟣 P4 — Observability

#### 1.13 ILogger Integration

**Current:** No logging.

**Proposed:** Accept `ILogger<ToolIndex>` via options. Log key events:
- Index creation (tool count, embedding dimension, time taken)
- Search queries (prompt length, results count, top score, time taken)
- Cache hits/misses

**Impact:** Essential for production debugging and monitoring.  
**Complexity:** Simple  
**Risk:** None

#### 1.14 Metrics / OpenTelemetry

**Current:** No metrics.

**Proposed:** Emit `System.Diagnostics.Metrics` counters:
- `mcptoolrouter.search.count` — total searches
- `mcptoolrouter.search.duration` — search latency histogram
- `mcptoolrouter.search.top_score` — histogram of top result scores
- `mcptoolrouter.index.tool_count` — gauge of indexed tools
- `mcptoolrouter.cache.hit_rate` — cache hit ratio

**Impact:** Enables dashboards and alerting in production.  
**Complexity:** Medium  
**Risk:** Low

---

### 🧪 P5 — Testing & Benchmarks

#### 1.15 BenchmarkDotNet Performance Tests

**Current:** Only correctness tests (xUnit).

**Proposed:** Add a `benchmarks/` project using BenchmarkDotNet:
- Search latency vs. tool count (10, 100, 1000, 10000 tools)
- Memory allocation per search
- Index creation time vs. tool count
- SIMD vs. scalar cosine similarity

**Impact:** Establishes performance baseline. Catches regressions. Great for README badges.  
**Complexity:** Medium  
**Risk:** None

---

## Part 2: Sample Proposals

### Sample 1: LiveMcpServer

**Name:** `LiveMcpServer`  
**Description:** Connects to a real MCP server (e.g., the official `filesystem` or `sqlite` server) via stdio transport, pulls tools dynamically, indexes them, and routes user queries to the most relevant tools — a complete end-to-end demo.  
**Why it helps:** Current samples use hardcoded `Tool` objects. This shows the *real* MCP workflow — connect, discover, route — which is what production users will actually do.  
**Complexity:** Medium  
**Dependencies:** `ModelContextProtocol` SDK, an MCP server binary (e.g., `@modelcontextprotocol/server-filesystem` via npx, or a local .NET MCP server)  

---

### Sample 2: AspNetCoreApi

**Name:** `AspNetCoreApi`  
**Description:** A minimal ASP.NET Core Web API that exposes a `/route` endpoint. Accepts a user prompt, uses MCPToolRouter to find relevant tools, and returns the filtered tool list as JSON. Demonstrates singleton `ToolIndex` lifecycle in DI.  
**Why it helps:** Most .NET developers build web APIs. Showing DI integration, singleton lifecycle, and HTTP endpoint usage makes adoption trivial for web teams. Also a great template for building "tool routing as a service."  
**Complexity:** Simple  
**Dependencies:** `Microsoft.AspNetCore.App` (built-in), MCPToolRouter  

---

### Sample 3: MultiServerAggregator

**Name:** `MultiServerAggregator`  
**Description:** Connects to 2-3 different MCP servers simultaneously (e.g., filesystem + database + weather API), aggregates all their tools into a single unified index, and demonstrates cross-server routing — "find the best tool regardless of which server provides it."  
**Why it helps:** Real-world MCP deployments involve multiple servers. This is the "killer demo" for conferences: show 50+ tools from 3 servers, and MCPToolRouter instantly finding the right one. Demonstrates the scaling/token-saving value proposition clearly.  
**Complexity:** Complex  
**Dependencies:** `ModelContextProtocol` SDK, 2-3 MCP server binaries, MCPToolRouter  

---

### Sample 4: SemanticKernelPlugin

**Name:** `SemanticKernelPlugin`  
**Description:** Wraps MCPToolRouter as a Semantic Kernel plugin/filter that intercepts function-calling and pre-filters the `KernelFunction` set before sending to the LLM. Shows how to drop MCPToolRouter into existing SK pipelines.  
**Why it helps:** Semantic Kernel is the most popular .NET AI orchestration library. Showing seamless integration lowers the adoption barrier for the largest segment of .NET AI developers. This is a high-signal demo for the Microsoft AI ecosystem.  
**Complexity:** Medium  
**Dependencies:** `Microsoft.SemanticKernel` (1.x), `Azure.AI.OpenAI`, MCPToolRouter  

---

### Sample 5: InteractiveBenchmark

**Name:** `InteractiveBenchmark`  
**Description:** A console app that generates N synthetic tools (configurable: 10, 50, 100, 500, 1000), indexes them, runs a suite of queries, and prints a performance report: index creation time, avg/p50/p95/p99 search latency, memory usage, and token savings estimate vs. sending all tools.  
**Why it helps:** Developers evaluating the library want to know: "how does it scale?" This gives them a one-command answer. Also doubles as a regression test for performance. Great for README screenshots showing scale characteristics.  
**Complexity:** Simple  
**Dependencies:** MCPToolRouter only (no external services needed — runs fully offline)  

---

## Priority Ranking

| Priority | Item | Effort | Impact |
|----------|------|--------|--------|
| 🔴 P0 | 1.1 Eliminate allocations | Small | High |
| 🔴 P0 | 1.2 SIMD cosine similarity | Medium | High |
| 🟡 P1 | 1.12 IEmbeddingGenerator abstraction | Medium | Very High |
| 🟡 P1 | 1.4 Index serialization | Medium | High |
| 🟡 P1 | 1.6 Options pattern | Small | Medium |
| 🟡 P1 | 1.11 MCP server integration | Small | High |
| 🟡 P1 | 1.13 ILogger | Small | Medium |
| 🟢 P2 | 1.10 DI extensions | Medium | High |
| 🟢 P2 | 1.7 Dynamic index | Medium | Medium |
| 🟢 P2 | 1.5 Query cache | Small | Medium |
| 🟢 P2 | 1.8 IAsyncEnumerable | Small | Low |
| 🟢 P2 | 1.3 Batch search | Medium | Medium |
| 🔵 P3 | 1.14 Metrics/OTel | Medium | Medium |
| 🔵 P3 | 1.9 Tags/categories | Medium | Medium |
| 🔵 P3 | 1.15 Benchmarks | Medium | Medium |

### Recommended First Sprint

1. **1.1 + 1.2** — Performance foundations (allocations + SIMD)
2. **1.6** — Options pattern (unlocks clean addition of all other features)
3. **1.12** — `IEmbeddingGenerator` abstraction (biggest community impact)
4. **Sample: InteractiveBenchmark** — Proves the performance story
5. **Sample: LiveMcpServer** — Proves the MCP integration story

---

*— Flynn 🏗️*
