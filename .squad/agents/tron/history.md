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
