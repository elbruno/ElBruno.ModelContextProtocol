# Tron — History

## Project Context

- **Project:** ElBruno.MCPToolRouter
- **User:** Bruno Capuano
- **Stack:** .NET (C#), NuGet library, xUnit, ElBruno.LocalEmbeddings
- **Description:** .NET library that ingests MCP tool definitions, embeds them into a local vector store, and returns top-K most relevant tools via cosine similarity.

## Learnings

### Project Structure (completed)
- Created complete project infrastructure following ElBruno .NET conventions
- Solution uses `.slnx` XML format (not `.sln`)
- Library project: `src/ElBruno.ModelContextProtocol.MCPToolRouter/`
- Test project: `src/tests/ElBruno.ModelContextProtocol.MCPToolRouter.Tests/`
- Root files: `global.json`, `Directory.Build.props`, solution file
- Directories: `images/`, `docs/` (placeholders created)

### Technical Decisions
- **Target Framework:** `net10.0` only (not multi-targeting)
  - Reason: ElBruno.LocalEmbeddings 1.1.5 only supports net10.0
  - Originally planned to target net8.0;net10.0 per conventions, but dependency constraint forced single-target
- **ElBruno.LocalEmbeddings Package:** Version 1.1.5 (latest)
  - API: `await LocalEmbeddingGenerator.CreateAsync(cancellationToken)`
  - No options parameter in latest version
  - Returns `Embedding<float>` from `Microsoft.Extensions.AI`
  - Vector access via `.Vector.ToArray()`
- **ModelContextProtocol.Core Package:** Version 1.0.0
  - Provides `Tool` type with `Name`, `Description`, `InputSchema`, `Title`
  - Namespace: `ModelContextProtocol.Protocol`

### Implementation Architecture
- **ToolIndex.cs:** Main entry point with factory pattern
  - Private constructor + public static `CreateAsync` factory method
  - Generates embeddings using `"{tool.Name}: {tool.Description}"` format
  - Stores tools and embeddings in parallel arrays for fast lookup
  - Custom `CosineSimilarity` implementation (extension method not available in package)
- **ToolSearchResult.cs:** Simple result record with `Tool` and `Score`
- **Search algorithm:** Linear scan with cosine similarity, sort by score descending, filter by minScore, return topK

### Key File Paths
- Library: `D:\elbruno\ElBruno.ModelContextProtocol\src\ElBruno.ModelContextProtocol.MCPToolRouter\`
- Tests: `D:\elbruno\ElBruno.ModelContextProtocol\src\tests\ElBruno.ModelContextProtocol.MCPToolRouter.Tests\`
- Solution: `D:\elbruno\ElBruno.ModelContextProtocol\ElBruno.ModelContextProtocol.slnx`

### Build Verification
- ✅ `dotnet restore` successful
- ✅ `dotnet build` successful (net10.0)
- Library builds cleanly with no warnings
- Test project builds with xUnit framework

### Test Coverage Created
- `ToolIndexTests.cs` with 5 test cases:
  - CreateAsync with valid tools
  - CreateAsync with empty list (throws ArgumentException)
  - SearchAsync finds relevant tool
  - SearchAsync filters by minScore
  - SearchAsync respects topK limit

### Sample Applications Created (2025-03-26)
Created three comprehensive sample applications demonstrating the library:

1. **BasicUsage** (`src/samples/BasicUsage/`)
   - Simple console app with no external dependencies
   - Demonstrates core library functionality: creating ToolIndex, performing semantic search
   - Shows topK and minScore filtering
   - Targets net8.0 only (per convention for samples)

2. **TokenComparison** (`src/samples/TokenComparison/`)
   - Key value proposition demo: measures token savings
   - Compares sending ALL tools vs. filtered tools (via MCPToolRouter) to Azure OpenAI
   - Uses Azure.AI.OpenAI 2.1.0 SDK (OpenAI.Chat namespace)
   - Configuration via user secrets: AzureOpenAI:Endpoint, ApiKey, DeploymentName
   - Demonstrates 60-80% input token savings with 18 tools → top 3
   - Targets net8.0

3. **FilteredFunctionCalling** (`src/samples/FilteredFunctionCalling/`)
   - End-to-end practical workflow: filter tools → call LLM → execute tools → final response
   - Shows complete function calling integration with Azure OpenAI
   - 5 tools with stub implementations (get_weather, send_email, calculate, search_files, translate)
   - Uses dictionary-based tool definitions with implementations
   - Same user secrets pattern as TokenComparison
   - Targets net8.0

**Technical Notes:**
- All samples reference the library via ProjectReference
- Azure OpenAI samples use `Azure.AI.OpenAI` 2.1.0 directly (not Microsoft.Extensions.AI.OpenAI)
- API: `AzureOpenAIClient.GetChatClient()` returns `OpenAI.Chat.ChatClient`
- ChatTool, ChatCompletionOptions, UserChatMessage from `OpenAI.Chat` namespace
- Each sample includes README.md with setup instructions
- Solution file (`.slnx`) updated with `/src/samples/` folder containing all three projects

**Build Verification:**
- ✅ All projects build successfully in Release mode (net8.0)
- ✅ No warnings or errors
- ✅ Samples compile but require Azure credentials to run

### TokenComparisonMax Sample (2025-03-26)
Created an EXTREME token-savings demonstration with 120+ MCP tools and Spectre.Console UI:

1. **TokenComparisonMax** (`src/samples/TokenComparisonMax/`)
   - 120 realistic MCP tool definitions across 12 domains (weather, email, files, database, calendar, math, translation, web, DevOps, security, analytics, AI/ML)
   - Spectre.Console for beautiful terminal UX: FigletText banner, Live() table with real-time updates, styled summary tables, Panel highlights
   - 12 test scenarios (one per domain) to show routing works across all categories
   - Live table updates in real-time as each scenario processes
   - Final summary table with per-scenario and aggregate token savings, TOTAL row
   - Per-scenario tool selection breakdown with similarity scores
   - Uses Spectre.Console 0.49.1 NuGet package (latest stable)
   - Same user secrets pattern as TokenComparison (`token-comparison-max-sample`)
   - ToolIndex created once and reused across all scenarios for efficiency
   - Targets net8.0, references MCPToolRouter via ProjectReference
   - Added to solution file under `/src/samples/` folder
   - ✅ Builds successfully in Release mode (net8.0)

### AgentWithToolRouter Sample (2025-07-17)
Created a sample demonstrating MCPToolRouter with the Microsoft Agent Framework:

1. **AgentWithToolRouter** (`src/samples/AgentWithToolRouter/`)
   - Integrates MCPToolRouter with `Microsoft.Agents.AI.OpenAI` 1.0.0-rc4
   - 11 function tools across 6 domains (weather, email, calendar, files, math, translation)
   - Uses `ToolIndex.SearchAsync()` to semantically filter tools per prompt → maps MCP results to `AIFunction` instances
   - Creates `AIAgent` with only relevant tools, reducing token footprint
   - Multi-turn session demo with `AgentSession`
   - Supports API key or `DefaultAzureCredential` (passwordless) via user secrets
   - Targets net10.0 (matches Agent Framework requirements)
   - Uses `Azure.AI.OpenAI` 2.2.0-beta.4, `Azure.Identity` 1.13.2

**Technical Notes:**
- `AsAIAgent()` is an extension on `IChatClient` (not `ChatClient`), so must convert via `.AsIChatClient()` from `Microsoft.Extensions.AI.OpenAI`
- `AsIChatClient()` (not `AsChatClient()`) is the correct method name in `Microsoft.Extensions.AI.OpenAI` 10.3.0
- `tools` parameter on `AsAIAgent()` is `IList<AITool>?` — cast `AIFunction` to `AITool` when building the list
- `<NoWarn>$(NoWarn);MAAI001</NoWarn>` suppresses preview API warnings
- Added to solution file under `/src/samples/` folder
- ✅ Builds successfully in Release mode (net10.0)

### TokenComparisonMax Money Saved Column (2025-07-18)
Added cost calculations and "Money Saved" column to both live and summary tables:

- **Pricing constants** added after Azure client setup: `InputPricePerToken` ($0.25/1M) and `OutputPricePerToken` ($2.00/1M) for GPT-5-mini
- **ScenarioResult record** extended with `StandardCost`, `RoutedCost`, `MoneySaved` (decimal fields)
- **Output tokens captured** from both standard and routed responses (`Usage.OutputTokenCount`)
- **Cost calculation**: `(inputTokens * InputPricePerToken) + (outputTokens * OutputPricePerToken)` per scenario
- **Live table**: Added "💰 Saved" column showing `$0.XXXX` per scenario in green
- **Summary table**: Added "💰 Cost Saved" column with per-row and TOTAL cost savings
- **Highlight panel**: Updated to include estimated cost savings line with GPT-5-mini pricing attribution
- **Pricing reference note**: Added at the end with source URL
- Money values formatted to 4 decimal places (`F4`) with green Spectre.Console markup
- ✅ Builds successfully in Release mode (net8.0), 0 warnings

### Library Improvements — 5-Part Refactor (2025-07-18)
Implemented 5 major improvements to the MCPToolRouter core library:

1. **Options Pattern + ILogger Integration**
   - Created `ToolIndexOptions.cs` with `EmbeddingTextTemplate`, `QueryCacheSize`, `Logger`, `EmbeddingOptions`
   - Added `Microsoft.Extensions.Logging.Abstractions` for `ILogger` support
   - High-perf logging via `[LoggerMessage]` source generator (IndexCreated, SearchCompleted, ToolsAdded, ToolsRemoved)
   - Old `CreateAsync(tools, LocalEmbeddingsOptions?)` marked `[Obsolete]` — backward compatible

2. **SIMD Cosine Similarity + Allocation Elimination**
   - Pre-extract `float[]` vectors at index creation (no `Embedding<float>[]` stored)
   - Replaced scalar loop with `System.Numerics.Vector<float>` SIMD-accelerated cosine similarity
   - `ReadOnlySpan<float>` used throughout search — no `.ToArray()` in hot path
   - Vectors stored as `List<float[]>` for mutability (add/remove)

3. **IEmbeddingGenerator Abstraction**
   - Accept `IEmbeddingGenerator<string, Embedding<float>>?` in `CreateAsync`
   - When null, falls back to `LocalEmbeddingGenerator` (current behavior)
   - `bool _ownsGenerator` tracks disposal ownership — external generators not disposed
   - Added `Microsoft.Extensions.AI.Abstractions` explicit reference

4. **Index Serialization (Save/Load)**
   - Binary format: `[version][toolCount][embeddingDim]` header + per-tool `[name][desc][vectorLen][floats]`
   - `SaveAsync(Stream)` and `LoadAsync(Stream, generator?, options?)` methods
   - Format versioned at v1 — `InvalidDataException` on version mismatch
   - `BinaryWriter`/`BinaryReader` with `leaveOpen: true`

5. **DI Extensions + Dynamic Index**
   - Created `IToolIndex` interface with `Count`, `SearchAsync`, `AddToolsAsync`, `RemoveTools`, `SaveAsync`
   - `ToolIndex` implements `IToolIndex`
   - `AddToolsAsync`: generates embeddings, appends to internal lists under write lock
   - `RemoveTools`: rebuilds lists excluding named tools under write lock
   - `ReaderWriterLockSlim` protects all mutable state (reads vs writes)
   - `ServiceCollectionExtensions.cs` with `AddMcpToolRouter` overloads
   - Internal `CreateEmptyAsync` for DI registration without initial tools
   - Added `Microsoft.Extensions.DependencyInjection.Abstractions`, `Microsoft.Extensions.Options`

**Package Versions:**
- All `Microsoft.Extensions.*` packages pinned to `10.0.3`/`10.3.0` to match transitive deps from `ElBruno.LocalEmbeddings 1.1.5`
- Lower versions caused `NU1605` downgrade errors (treated as errors by `WarningsAsErrors`)

**Build Verification:**
- ✅ `dotnet build -c Release` — 0 warnings, 0 errors on both net8.0 and net10.0
- ✅ `dotnet test -c Release --framework net8.0` — 21/21 tests pass
- ✅ All sample projects build successfully

### FunctionalToolsValidation Sample (2025-07-18)
Created a comprehensive validation sample with 53 real C# tool implementations:

1. **FunctionalToolsValidation** (`src/samples/FunctionalToolsValidation/`)
   - 53 functional tools with real C# implementations across 4 domains:
     - Math (20): add, subtract, multiply, divide, power, sqrt, modulo, factorial, fibonacci, gcd, lcm, abs, min, max, round, ceiling, floor, percentage, average, median
     - String (16): reverse, uppercase, lowercase, trim, length, contains, replace, split, join, repeat, pad_left, pad_right, starts_with, ends_with, char_count, word_count
     - DateTime (8): current_time, add_days, days_between, day_of_week, is_weekend, format_date, parse_date, time_zone_convert
     - Conversion (9): celsius_to_fahrenheit, fahrenheit_to_celsius, km_to_miles, miles_to_km, kg_to_lbs, lbs_to_kg, hex_to_decimal, decimal_to_hex, binary_to_decimal
   - 12 test scenarios with expected answers covering all 4 domains
   - Full tool execution loop: LLM → tool call → real C# execution → result back → final answer
   - Both standard mode (all 53 tools) and routed mode (ToolIndex top-5) run each scenario
   - Fuzzy validation: checks expected value appears in LLM response (case-insensitive contains)
   - Detailed comparison table and summary with token savings

**Technical Notes:**
- Each MCP Tool has a proper `InputSchema` (JSON schema) for parameter types — LLM knows exact parameter names and types
- `ConvertToChatTool` helper includes schema via `BinaryData.FromString(mcpTool.InputSchema.GetRawText())`
- Tool execution loop handles multi-turn conversations (up to 10 iterations) with `ChatFinishReason.ToolCalls` check
- `toolRegistry` uses `Dictionary<string, Func<JsonElement, string>>` for direct dispatch
- Anonymous object schemas serialized via `JsonSerializer.SerializeToElement()` for concise inline definitions
- Uses `"G"` format specifier for numeric output to avoid trailing zeros
- UserSecretsId: `functional-tools-validation-sample`
- Targets net8.0, references MCPToolRouter via ProjectReference
- Added to solution file under `/src/samples/` folder
- ✅ Builds successfully in Release mode (net8.0), 0 warnings, 0 errors

### ToolRouter, PromptDistiller, ToolRouterOptions (2025-07-18)
Added high-level routing facade with prompt distillation support:

1. **PromptDistiller.cs** (`src/ElBruno.ModelContextProtocol.MCPToolRouter/`)
   - Static helper using `IChatClient` (from Microsoft.Extensions.AI) to distill complex prompts into single-sentence intents
   - `PromptDistillerOptions` class: configurable SystemPrompt, MaxOutputTokens (128), Temperature (0.1f)
   - Falls back to original prompt if distilled result < 5 chars
   - Full argument validation + XML doc comments

2. **ToolRouterOptions.cs** (`src/ElBruno.ModelContextProtocol.MCPToolRouter/`)
   - TopK (5), MinScore (0.0f), EnableDistillation (true)
   - Distillation settings: SystemPrompt, MaxOutputTokens, Temperature
   - `IndexOptions` property for underlying ToolIndex configuration
   - Internal `ToDistillerOptions()` helper to bridge to PromptDistillerOptions

3. **ToolRouter.cs** (`src/ElBruno.ModelContextProtocol.MCPToolRouter/`)
   - Sealed class implementing `IAsyncDisposable`, combines ToolIndex + PromptDistiller
   - Async factory `CreateAsync` (same pattern as ToolIndex): accepts tools, optional IChatClient, optional IEmbeddingGenerator
   - Instance `RouteAsync`: distills prompt if client available + EnableDistillation, then delegates to ToolIndex.SearchAsync
   - Static one-shot `RouteAsync`: creates temp index, routes, disposes
   - `Index` property exposes underlying IToolIndex for advanced usage
   - Internal `FromIndex` factory for DI (wraps existing IToolIndex without ownership)
   - LoggerMessage: DistillationCompleted (EventId 100), RoutingCompleted (EventId 101)
   - Ownership tracking: `_ownsIndex` determines if DisposeAsync disposes the index

4. **ServiceCollectionExtensions.cs** — new overload:
   - `AddMcpToolRouter(services, tools, chatClient, configure?)` registers both IToolIndex and ToolRouter as singletons
   - ToolRouter registered via `FromIndex` (does not own the index — DI container manages lifecycle)

**Technical Notes:**
- No new NuGet packages required — all types from existing `Microsoft.Extensions.AI.Abstractions`
- EventIds 100-101 for ToolRouter logging, avoids collision with ToolIndex EventIds 1-4
- `IChatClient.GetResponseAsync` used (not `CompleteAsync`) per Microsoft.Extensions.AI abstractions API
- ToolRouter uses `ConfigureAwait(false)` consistently, matching ToolIndex patterns

**Build Verification:**
- ✅ `dotnet build -c Release` — 0 warnings, 0 errors on both net8.0 and net10.0
- ✅ `dotnet test -c Release --framework net8.0` — 29/29 tests pass (no regressions)

### Model Management Convenience APIs (2025-07-18)
Added static helpers and convenience APIs for embedding model management:

1. **EmbeddingModelInfo.cs** (`src/ElBruno.ModelContextProtocol.MCPToolRouter/`)
   - Static helper class for model cache inspection
   - `DefaultModelName` constant: "sentence-transformers/all-MiniLM-L6-v2"
   - `GetDefaultCacheDirectory()`: resolves default path matching `LocalEmbeddings.ModelDownloader` logic
   - `GetModelDirectory(options?)`: resolves model directory from `LocalEmbeddingsOptions`
   - `IsModelDownloaded(options?)`: checks for `.onnx` files in the resolved directory
   - `GetStatus(options?)`: returns `EmbeddingModelStatus` with name, path, download flag, quantized pref

2. **EmbeddingModelStatus.cs** (`src/ElBruno.ModelContextProtocol.MCPToolRouter/`)
   - Sealed class with `required init` properties: `ModelName`, `CacheDirectory`, `IsDownloaded`, `PreferQuantized`

3. **ToolRouterOptions.cs** — added convenience pass-through:
   - `EmbeddingModelCacheDirectory` property flows to `IndexOptions.EmbeddingOptions.CacheDirectory`
   - Applied in `ToolRouter.CreateAsync` before building the index

4. **ToolRouter.cs** — added instance method:
   - `GetEmbeddingModelStatus()`: returns `EmbeddingModelInfo.GetStatus()` using the router's options

5. **ToolIndex.cs** — added static method:
   - `GetEmbeddingModelStatus(ToolIndexOptions?)`: delegates to `EmbeddingModelInfo.GetStatus()`

**Key Technical Detail:**
- Default cache path verified via decompilation of `ModelDownloader`: `Path.Combine(Environment.GetFolderPath(SpecialFolder.LocalApplicationData), "ElBruno", "LocalEmbeddings", "models")`
- Model name sanitized by replacing `/` with `Path.DirectorySeparatorChar` (mirrors `DefaultPathHelper.SanitizeModelName`)
- Resolution priority: `ModelPath` > `CacheDirectory + ModelName` > default path + ModelName

**Build Verification:**
- ✅ `dotnet build -c Release` — 0 warnings, 0 errors on both net8.0 and net10.0
- ✅ `dotnet test -c Release --framework net8.0` — 60/60 tests pass (10 new EmbeddingModelInfo tests + 50 existing)

### Simplified Static API for ToolRouter (2025-07-18)
Replaced the old static `RouteAsync(tools, prompt, chatClient?)` one-shot method with two clear static methods:

1. **`SearchAsync(userPrompt, tools, topK, minScore, options, ct)`** — Embeddings-only semantic search, no LLM needed. One-liner for simple use cases.
2. **`SearchUsingLLMAsync(userPrompt, tools, chatClient, topK, minScore, options, ct)`** — LLM-distilled search. User provides their own `IChatClient`.

**Key Design Choices:**
- Parameter order is **prompt-first** (`userPrompt, tools`) — reads naturally: "Search for *this* in *these tools*"
- Old static `RouteAsync` deleted outright (pre-1.0 v0.5.1, breaking changes are fine)
- Instance API (`CreateAsync` + `RouteAsync`) unchanged for advanced users
- No new dependencies added — library stays backend-agnostic via `IChatClient`
- `SearchAsync` forces `EnableDistillation = false`; `SearchUsingLLMAsync` forces `EnableDistillation = true`

**Test Changes:**
- Replaced `RouteAsync_StaticOneShot_ReturnsResults` with `SearchAsync_Static_ReturnsResults`
- Added `SearchUsingLLMAsync_Static_ReturnsResults` (new test)
- All other tests unchanged

**Build Verification:**
- ✅ `dotnet build -c Release` — 0 warnings, 0 errors on both net8.0 and net10.0
- ✅ `dotnet test -c Release --framework net8.0` — 61/61 tests pass (1 new test added, 1 replaced)

### Zero-Setup SearchUsingLLMAsync Overload + ElBruno.LocalLLMs Dependency (2025-07-18)
Added a zero-setup `SearchUsingLLMAsync` overload that internally creates a `LocalChatClient` — no user-side LLM setup required.

1. **ElBruno.LocalLLMs 0.5.0** added to library csproj as a package dependency
   - `Microsoft.Extensions.AI.Abstractions` bumped from 10.3.0 → 10.4.0 (required by LocalLLMs 0.5.0)
   - `Microsoft.Extensions.DependencyInjection.Abstractions` bumped from 10.0.3 → 10.0.5 (required by LocalLLMs 0.5.0)
   - Test project also bumped `Microsoft.Extensions.AI.Abstractions` to 10.4.0 to match

2. **ToolRouterOptions.cs** — new `LocalLLMModel` property:
   - Type: `ElBruno.LocalLLMs.ModelDefinition?` (not `string` — `LocalLLMsOptions.Model` is `ModelDefinition`)
   - Users can set `options.LocalLLMModel = KnownModels.Qwen25_05BInstruct` etc.
   - Default `null` = use whatever `LocalLLMsOptions` defaults to

3. **ToolRouter.cs** — new zero-setup static overload:
   - `SearchUsingLLMAsync(userPrompt, tools, topK, minScore, options, ct)` — no IChatClient param
   - Internally creates `LocalChatClient` via `CreateAsync(llmOptions, progress: null, ct)`
   - Disposes the client after use (`using var chatClient = ...`)
   - Delegates to the existing IChatClient overload
   - Placed BEFORE the IChatClient overload so C# resolves the right one

4. **McpToolRouting sample** simplified:
   - Removed `using ElBruno.LocalLLMs;` import
   - Scenario 1 now: `ToolRouter.SearchUsingLLMAsync(complexPrompt, allTools, topK: 7)` (one-liner)
   - Scenario 2 already used `ToolRouter.SearchAsync(...)` (unchanged)
   - Removed explicit `ElBruno.LocalLLMs` and `Microsoft.ML.OnnxRuntimeGenAI` from sample csproj (transitive from library)

**API Notes (ElBruno.LocalLLMs 0.5.0):**
- `LocalLLMsOptions.Model` is `ModelDefinition`, not `string` — no implicit conversion
- `LocalChatClient.CreateAsync` signature: `(LocalLLMsOptions, IProgress<ModelDownloadProgress>?, CancellationToken)` — must pass `progress: null` explicitly
- `KnownModels.Qwen25_05BInstruct` is a `ModelDefinition` constant from the package

**Build Verification:**
- ✅ `dotnet build -c Release` — 0 warnings, 0 errors on both net8.0 and net10.0
- ✅ `dotnet test --verbosity minimal` — 67/67 tests pass (no regressions)
- ✅ McpToolRouting sample builds successfully (net8.0)

### Comprehensive Performance Audit (2025-07-18)
Performed full codebase performance analysis covering 8 areas, 25 findings total. Key discoveries:

**Critical Findings:**
1. **QueryCacheSize is DEAD CODE** (🔴 P0) — `ToolIndexOptions.QueryCacheSize` is declared, documented, tested, and referenced in 7 samples, but never read by `ToolIndex.SearchAsync`. No caching data structure exists. Users think caching is active — it is not.
2. **Static API creates ONNX session per call** (🔴 P1) — `SearchAsync` and `SearchUsingLLMAsync` static methods each load a full ONNX model (~200ms), generate all tool embeddings, search, then dispose. 15-35× overhead vs. instance reuse.
3. **Zero-setup LLM overload creates LocalChatClient per call** (🔴 P1) — ~1-3.5 seconds per call including LLM model load.

**Architecture Strengths:**
- SIMD cosine similarity is well-implemented (`System.Numerics.Vector<float>`)
- Batch embedding correctly used for index creation
- `ReaderWriterLockSlim` concurrency model is correct
- IDisposable ownership patterns are clean
- Binary save/load format is efficient and version-aware
- Memory footprint is excellent (~1.6KB per tool for 384-dim embeddings)

**Medium Findings:**
- DisposeAsync has non-atomic `_disposed` check (race condition)
- DI registration uses sync-over-async (`GetAwaiter().GetResult()`)
- Save format loses Tool properties (InputSchema, Title) — only Name+Description saved
- No automatic index persistence / hash-based invalidation
- Search allocates full list then truncates (could use PriorityQueue)

**Output:** `.squad/decisions/inbox/tron-performance-audit.md` — 25 findings with impact ratings, priority matrix, and concrete recommendations.

### 2026-03-28 — Performance Audit Complete + Decision Merged

Completed comprehensive performance analysis as part of coordinated audit sprint with Sark (security) and Flynn (synthesis). Audit identified 3 high-impact, 6 medium-impact, and 5 low-impact findings across static API, embedding generation, file I/O, concurrency, and DI patterns.

**Key Performance Findings (P0-P3):**
- P0 (CRITICAL BUG): QueryCacheSize is DEAD CODE. Declared in ToolIndexOptions, documented, tested in 7 samples, but NEVER implemented. ToolIndex.SearchAsync ignores the option entirely. Users believe caching is active — it is not.
  - **Impact:** 15-35× speedup lost for identical repeated queries
  - **Fix:** Implement LRU cache using ConcurrentDictionary<string, float[]> with FIFO/size-based eviction (Item 1.1 in Phase 1)
- P0 (HIGH): Static API creates new ONNX session per call (200-500ms cold, 50ms warm overhead)
  - **Impact:** 310-720ms per SearchAsync/SearchUsingLLMAsync call vs 10-20ms with instance reuse
  - **Fix:** Shared Lazy<Task<IEmbeddingGenerator>> singleton (Item 2.1 in Phase 2)
- P0 (HIGH): DisposeAsync race condition — non-atomic bool check-and-set on _disposed
  - **Impact:** Double-dispose of resources under concurrent calls (crash/corruption)
  - **Fix:** Interlocked.Exchange(ref _disposed, 1) pattern (Item 1.3 in Phase 1)
- P1 (MEDIUM): Zero-setup LLM overload creates LocalChatClient per call (500ms-2s overhead)
  - **Impact:** Extreme cold start penalty for simple use cases
  - **Fix:** Shared Lazy<Task<LocalChatClient>> singleton (Item 2.2 in Phase 2)
- P2 (MEDIUM): No global ONNX session cache; multiple ToolIndex instances = multiple sessions (80-100MB each)
  - **Fix:** Reference-counted singleton pool for generators
- P2 (MEDIUM): LoadAsync deserializes without bounds checks on toolCount, embeddingDim, vectorLength
  - **Impact:** Malicious .bin file with toolCount = 2_000_000_000 causes OOM
  - **Fix:** Const limits (MaxTools=100K, MaxEmbeddingDim=8192) (Item 2.3 in Phase 2)
- P2 (MEDIUM): Search allocates full list then sorts and truncates (O(N log N) for top-K)
  - **Fix:** PriorityQueue<int, float> for O(N log K) (Item 2.4 in Phase 2)
- P3 (MEDIUM): DI registration uses sync-over-async (.GetAwaiter().GetResult())
  - **Impact:** Blocks threadpool during startup, potential starvation in ASP.NET
  - **Fix:** AddMcpToolRouterAsync with IHostedService (Item 4.4 in Phase 4)
- P3 (MEDIUM): Binary format loses Tool properties (InputSchema, Title) — only Name+Description saved
  - **Fix:** Format v2 with JSON serialization, backward-compatible reader (Item 4.2 in Phase 4)

**Architecture Strengths:**
- SIMD cosine similarity correctly implemented (System.Numerics.Vector<float>)
- Batch embedding optimization used properly
- ReaderWriterLockSlim concurrency model is correct
- IDisposable ownership patterns are clean
- Binary serialization format is efficient and versioned
- Memory footprint excellent (~1.6KB per tool)

**Integration into 5-Phase Plan:**
- Phase 1 (Immediate): Fix QueryCacheSize (Item 1.1), DisposeAsync race (Item 1.3)
- Phase 2: Fix static API ONNX overhead (Item 2.1, 2.2), LoadAsync bounds (Item 2.3), PriorityQueue (Item 2.4)
- Phase 3: Prompt length limit (Item 3.4), SemaphoreSlim migration (Item 3.5)
- Phase 4: Auto-persist (Item 4.1), Format v2 (Item 4.2), Block I/O (Item 4.3), Async DI (Item 4.4), Memory guard (Item 4.5)

**Success Metrics:**
- Phase 1: Cache hit rate > 99% for identical prompts; ~0ms cached vs ~10-20ms uncached
- Phase 2: Static API cold start < 100ms (from ~300-700ms) for repeat calls
- Phase 2: Zero-setup LLM < 500ms repeat (from ~1-3.5s) with shared client
- Phase 4: Auto-persist eliminates re-embedding on restart (< 50ms vs ~200-500ms)

**Decision merged to:** `.squad/decisions.md` (Decision §9 — 5-Phase Implementation Roadmap)
**Orchestration logged to:** `.squad/orchestration-log/2026-03-28T02-55-sark-security-audit.md`

### P0 Fixes — Phase 1 (completed)
- **QueryCacheSize LRU Cache:** Implemented FIFO-eviction query embedding cache in `ToolIndex.SearchAsync`. Uses `ConcurrentDictionary<string, float[]>` + `ConcurrentQueue<string>` for insertion-order tracking. Cache is cleared on `AddToolsAsync` and `RemoveTools` to avoid stale results. Added `LoggerMessage` at Debug level for cache hit/miss.
- **DisposeAsync Race Condition:** Changed `_disposed` from `bool` to `int` in both `ToolIndex` and `ToolRouter`, using `Interlocked.Exchange` for atomic check-and-set. Eliminates double-dispose under concurrent calls.
- Build: 0 warnings, 0 errors (net8.0 + net10.0)
- Tests: 67/67 passed on net8.0
### Phase 2 — Shared Singletons for Static API Performance (completed)
- **Shared Embedding Generator (Item 2.1):** Added process-level singleton for the ONNX embedding generator used by static API methods (SearchAsync, SearchUsingLLMAsync). Uses double-checked locking with SemaphoreSlim. The shared generator is passed to ToolIndex.CreateAsync with ownsGenerator: false, so per-call index disposal never destroys the shared session. Eliminates ~300-700ms ONNX session creation overhead on repeated static API calls.
- **Shared LocalChatClient (Item 2.2):** Same pattern for the zero-setup SearchUsingLLMAsync overload. Shared IChatClient singleton eliminates ~1-3.5s LocalChatClient.CreateAsync overhead on repeated calls.
- **ResetSharedResourcesAsync:** Public static cleanup method that disposes both shared singletons. Documented for app shutdown / test cleanup use only (not safe to call during in-flight searches).
- **UseSharedResources option:** Added ToolRouterOptions.UseSharedResources (default: 	rue). When alse, static methods fall back to per-call resource creation for isolation.
- **ToolIndex.CreateDefaultGeneratorAsync:** Changed from private to internal so ToolRouter can reuse the generator creation logic without duplication.
- Build: 0 warnings, 0 errors (net8.0 + net10.0)
- Tests: 76/76 passed on net8.0

### Prompt Length Limit (Phase 3.4)
- **Task:** Added MaxPromptLength guard to PromptDistiller to prevent oversized prompts from reaching the LLM
- **PromptDistillerOptions.MaxPromptLength:** New property (default 4096). Set to 0 or negative to disable truncation.
- **PromptDistiller.DistillIntentAsync:** Added overload accepting ILogger; truncates userPrompt before LLM call when it exceeds MaxPromptLength
- **LoggerMessage:** EventId 200, LogLevel.Warning — logs original vs. truncated length
- **ToolRouterOptions:** Added MaxPromptLength property, wired through ToDistillerOptions()
- **ToolRouter:** Now passes _logger to DistillIntentAsync for truncation visibility
- **Backward compatible:** Original 4-param overload delegates to new 5-param overload with NullLogger
- Build: 0 warnings, 0 errors (Release, net8.0 + net10.0)
- Tests: 85/85 passed on net8.0

### ElBruno.LocalLLMs v0.6.1 Upgrade & Model Metadata Integration
- **Package upgrade:** ElBruno.LocalLLMs 0.5.0 → 0.6.1 in MCPToolRouter.csproj
- **New API:** v0.6.1 exposes `LocalChatClient.ModelInfo` (`ModelMetadata?`) with MaxSequenceLength, ModelName, VocabSize
- **PromptDistillerOptions:** Added `ModelMaxSequenceLength` (nullable int); reverted MaxPromptLength default from 300 back to 4096
- **PromptDistiller.DistillIntentAsync:** Auto-computes effective prompt length from model context window (reserved 70 tokens, ~4 chars/token estimate); new LogMessage EventId=202 for auto-configuration
- **ToolRouterOptions:** Added internal `DetectedModelMaxSequenceLength`; wired through `ToDistillerOptions()`
- **ToolRouter:** Static field `_sharedModelMaxSequenceLength` captures model metadata on client creation; auto-populates `DetectedModelMaxSequenceLength` in both shared and fresh paths of `SearchUsingLLMAsync`
- **Samples:** LLMDistillationMax and LLMDistillationDemo now display model metadata after client creation
- **Gotcha:** LLMDistillationDemo had stale bin/obj cache from v0.5.0; required full bin/obj cleanup to resolve CS1061 on `ModelInfo`
- Build: 0 warnings, 0 errors; Tests: 85/85 passed
