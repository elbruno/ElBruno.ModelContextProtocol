# Flynn — History

## Project Context

- **Project:** ElBruno.MCPToolRouter
- **User:** Bruno Capuano
- **Stack:** .NET (C#), NuGet library, xUnit, ElBruno.LocalEmbeddings
- **Description:** .NET library that ingests MCP tool definitions, embeds them into a local vector store, and returns top-K most relevant tools via cosine similarity.

## Learnings

### 2025-07-24 — Library Analysis & Improvement Proposals

- **CosineSimilarity hot path allocates heavily:** Every `SearchAsync` call does `.ToArray()` on `Embedding<float>.Vector` for every tool. Pre-extracting vectors at index creation and using `ReadOnlySpan<float>` eliminates all per-search allocations.
- **Samples re-create ToolIndex per iteration:** `TokenComparison` and `FilteredFunctionCalling` call `ToolIndex.CreateAsync` inside loops, re-downloading/loading the ONNX model each time. Index reuse or serialization would fix this.
- **No abstraction over embedding provider:** Hard-coupled to `ElBruno.LocalEmbeddings`. Accepting `IEmbeddingGenerator<string, Embedding<float>>` from `Microsoft.Extensions.AI` would open the library to cloud providers and remove the net10.0-only constraint for users willing to use cloud embeddings.
- **No DI integration:** Library requires manual lifecycle management. `IServiceCollection` extensions with singleton `ToolIndex` would be the expected pattern for ASP.NET Core users.
- **Options pattern needed:** As features grow (cache, logging, templates), a `ToolIndexOptions` class prevents parameter explosion on `CreateAsync`.
- **Wrote analysis to:** `.squad/decisions/inbox/flynn-improvements.md` — 15 improvement proposals (P0-P3) and 5 sample proposals.
