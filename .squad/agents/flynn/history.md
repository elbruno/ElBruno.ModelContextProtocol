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

### 2026-07-24 — Simplified Static API Architecture Evaluation

- **Bruno wants two static one-liners:** `ToolRouter.SearchAsync` (embeddings only) and `ToolRouter.SearchUsingLLMAsync` (LLM distillation + embeddings) to replace the verbose `CreateAsync` → `RouteAsync` → `DisposeAsync` lifecycle for simple use cases.
- **Recommended against hard dependency on ElBruno.LocalLLMs:** Only 1 of 7 samples uses it, LocalLLMs is pre-release (v0.5.0), and the IChatClient abstraction already supports any LLM backend. Forcing ONNX LLM inference on all consumers for a convenience API is disproportionate.
- **Recommended keeping both static + instance APIs:** Static one-liners for scripts/demos/one-off queries. Instance API (CreateAsync + RouteAsync) for servers, agents, and multi-turn scenarios where embedding index reuse avoids ~50-200ms re-embedding cost per call.
- **Static API re-creates the embedding index per call:** Acceptable for one-off use but catastrophic for high-throughput. Documented this trade-off. Deferred internal caching to v0.2.0+ if user feedback demands it.
- **Parameter order prompt-first:** `SearchAsync(prompt, tools, topK?)` reads more naturally than `SearchAsync(tools, prompt, topK?)`. Matches Bruno's proposed signature.
- **Breaking change strategy:** Mark existing static `RouteAsync` as `[Obsolete]` rather than delete. Gives consumers migration window. Remove in v0.2.0 or v1.0.0.
- **No new files, no new dependencies:** Both static methods fit cleanly in existing ToolRouter.cs. Implementation delegates to CreateAsync internally.
- **Open question for Bruno:** Is IChatClient-based Mode 2 acceptable (3 lines: create client, search, dispose)? If zero-setup is essential, recommend a future `MCPToolRouter.LocalLLM` extension package after LocalLLMs reaches v1.0.
- **Wrote decision to:** `.squad/decisions/inbox/flynn-simplified-api-architecture.md`
