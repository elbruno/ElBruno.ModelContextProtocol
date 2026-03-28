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
