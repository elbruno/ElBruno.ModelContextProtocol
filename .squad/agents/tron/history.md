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

