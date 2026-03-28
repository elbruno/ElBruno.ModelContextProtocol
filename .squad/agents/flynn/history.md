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

### 2025-07-24 — Security & Performance Phased Implementation Plan

- **Synthesized Sark's security audit (8 medium, 14 low) and Tron's performance audit (3 high, 6 medium, 5 low)** into a 5-phase, ~20-item implementation plan ordered by severity and dependency.
- **P0 items identified:** (1) QueryCacheSize is dead code — declared, tested, sampled, but never implemented in ToolIndex.SearchAsync. This is the single most impactful bug. (2) Script injection in publish.yml via unescaped `github.event.inputs.version`. (3) DisposeAsync race condition (non-atomic bool check).
- **Key architectural decisions in plan:** Shared ONNX singleton for static API path (process-level `Lazy<Task<IEmbeddingGenerator>>`), shared LocalChatClient for zero-setup LLM overload, reference-counted lifecycle with `ResetSharedResources()` escape hatch.
- **Phase ordering rationale:** Phase 1 = broken things (cache, injection, race). Phase 2 = high-impact perf (15-35× static API speedup). Phase 3 = supply chain hardening (lock files, SHA pins). Phase 4 = advanced opt (auto-persist, format v2, async DI). Phase 5 = docs and monitoring.
- **Assignment strategy:** Tron owns core perf (12 items), Sark owns security hardening (6 items), Yori owns test coverage for all items, Ram owns documentation (4 items). Flynn reviews architecture-critical PRs (shared singleton, async DI).
- **Wrote decision to:** `.squad/decisions/inbox/flynn-phased-plan.md`

### 2026-03-28 — Security & Performance Audit Synthesis + 5-Phase Roadmap

Completed synthesis of parallel audits from Sark (security, 8 medium + 14 low findings) and Tron (performance, 3 high + 6 medium + 5 low findings). Created comprehensive 5-phase, ~20-item implementation plan ordered by criticality, dependency, and team assignments.

**Key Architectural Decisions in Phased Plan:**

1. **Shared ONNX Singleton Pattern (Item 2.1):**
   - Use `static Lazy<Task<IEmbeddingGenerator>>` for process-level ONNX session reuse
   - Thread-safe lazy initialization; thread-safe access by default
   - `ToolRouter.ResetSharedGenerator()` escape hatch for testing
   - Reference counting NOT required (single-model scenario)
   - 15-35× speedup for static API repeat calls

2. **Shared LocalChatClient Pattern (Item 2.2):**
   - Apply same `Lazy<Task<>>` pattern as Item 2.1
   - Document lifecycle: "Lives for process lifetime. Call ToolRouter.ResetSharedResources() to release."
   - Eliminates 500ms-2s model load penalty per zero-setup call
   - Depends on Item 2.1 (implement together)

3. **LRU Query Cache Implementation (Item 1.1):**
   - ConcurrentDictionary<string, float[]> backing store
   - FIFO eviction when size exceeds QueryCacheSize
   - Clear cache on AddToolsAsync, RemoveTools, DisposeAsync
   - Cache key: user prompt (raw string)
   - Add cache hit/miss debug logging
   - 99%+ cache hit rate for identical repeated queries

4. **SemaphoreSlim Migration from ReaderWriterLockSlim (Item 3.5):**
   - Replace thread-affine ReaderWriterLockSlim with SemaphoreSlim(1, 1)
   - Read concurrency sacrifice acceptable (< 10K tools = < 1ms search anyway)
   - Eliminates async-safety fragility; current code works but is brittle
   - Depends on Item 1.1 (same code path needs updating)

5. **Format v2 Binary Save/Load with Backward Compatibility (Item 4.2):**
   - Write v2 (includes JSON Tool metadata), read both v1 and v2
   - V1 reader persists only Name/Description
   - V2 reader fully restores Tool (InputSchema, Title, etc.)
   - Log warning when loading v1 (data loss)
   - Depends on Item 2.3 (bounds checks must be in place first)

6. **DisposeAsync Race Condition Fix (Item 1.3):**
   - Change `bool _disposed` to `int _disposed`
   - Use `Interlocked.Exchange(ref _disposed, 1)` for atomic check-and-set
   - Return early if already disposed
   - Apply to both ToolIndex and ToolRouter
   - P0 fix — immediate impact on concurrent usage

7. **Script Injection Fix in publish.yml (Item 1.2):**
   - Replace direct `${{ github.event.inputs.version }}` interpolation with env var
   - Use environment variables for all workflow inputs
   - Prevents bash injection of arbitrary code (e.g., credential theft)
   - Quick fix, high security impact

8. **NuGet Lock Files (Item 3.1):**
   - Add `<RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>` to Directory.Build.props
   - Run `dotnet restore` to generate `packages.lock.json` files
   - Commit lock files to git
   - Update CI: `dotnet restore --locked-mode`
   - Eliminates dependency confusion attacks

9. **GitHub Actions SHA-Pinning (Item 3.2):**
   - Pin all action references to full commit SHA with tag comment
   - Example: `actions/checkout@b4ffde65f46336ab88eb53be808477a3936bae11 # v4.1.1`
   - Apply to both build.yml and publish.yml
   - Add Dependabot configuration for GitHub Actions ecosystem updates

**Phase Ordering Rationale:**
- **Phase 1 (Immediate):** P0 fixes — broken/dangerous things. Unblock Phase 2. (3 items)
- **Phase 2 (Sprint 2):** High-impact perf — 15-35× speedup for repeated calls. Core value. (4 items)
- **Phase 3 (Sprint 3):** Security hardening + CI — supply chain, thread safety. (5 items)
- **Phase 4 (Sprint 4):** Advanced opt — nice-to-haves, can defer. (5 items)
- **Phase 5 (Ongoing):** Documentation — users understand tradeoffs, get guidance. (5 items)

**Team Assignment:**
| Agent | Role | Items | Notes |
|-------|------|-------|-------|
| **Tron** | Core Dev | 1.1, 1.3, 2.1, 2.2, 2.4, 3.4, 3.5, 4.1-4.5 (12 total) | All core perf/concurrency fixes |
| **Sark** | Security | 1.2, 2.3, 3.1-3.3, 5.4 (6 total) | CI/CD hardening, input validation, supply chain |
| **Yori** | QA | Tests for all items | Especially cache tests, race condition tests, bounds tests |
| **Ram** | Docs | 5.1-5.3, 5.5 (4 total) | Performance guide, security limitations, model security |
| **Flynn** | Architect | Review 2.1, 2.2, 4.4 | Sign-off on shared singleton pattern, async DI |

**Risk Mitigation:**
- Shared ONNX singleton memory leak → WeakReference or lifecycle documentation
- LRU cache invalidation on tool changes → Clear cache in mutation methods, add tests
- Lock file CI failures → Document dotnet restore --force-evaluate, enable Dependabot
- SHA-pinned actions fall behind → Add Dependabot github-actions config
- Format v2 breaks v1 files → v1→v2 migration in reader, warn on v1 load

**Success Criteria:**
- Phase 1: QueryCacheSize works (cache hit > 99%), script injection fixed, no race conditions
- Phase 2: Static API cold start < 100ms, zero-setup LLM < 500ms, LoadAsync bounds checked
- Phase 3: packages.lock.json committed, all actions SHA-pinned, transitive deps audited
- Phase 4: Auto-persist skips embedding on restart, format v2 handles all Tool metadata
- Phase 5: README has performance guide, users understand static vs instance tradeoffs

**Decision merged to:** `.squad/decisions.md` (Decision §9 — 5-Phase Implementation Roadmap)
**Orchestration logged to:** `.squad/orchestration-log/2026-03-28T02-55-sark-security-audit.md`
