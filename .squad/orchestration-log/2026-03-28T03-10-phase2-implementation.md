# Orchestration Log — Phase 2: Shared Singletons & Bounds Checking

**Timestamp:** 2026-03-28T03:10:00Z  
**Phase:** 2 / 3  
**Status:** ✅ Complete  
**Commit:** ac0ed8c

## Team Assignments

| Agent | Role | Assigned Task | Status |
|-------|------|---------------|--------|
| Tron | Core Dev | Implement shared ONNX + shared LLM client singleton; UseSharedResources option | ✅ Done |
| Sark | Security | Add LoadAsync bounds checking (toolCount, embeddingDim, vectorLength) | ✅ Done |
| Yori | Tester | Write 9 new tests for singletons + bounds validation | ✅ Done |

## Work Summary

### Tron — Shared Singletons for Static API Performance
- **Problem:** Every static call created/disposed fresh ONNX session (~300-700ms) + LocalChatClient (~1-3.5s)
- **Solution:** Process-level shared singletons (embedding generator, chat client) controlled by `ToolRouterOptions.UseSharedResources` (default: `true`)
- **Implementation:**
  - Double-checked locking with `SemaphoreSlim` for thread-safe lazy initialization
  - `ToolIndex.CreateDefaultGeneratorAsync` changed `private` → `internal` to avoid duplication
  - `ToolIndex.CreateAsync` accepts `ownsGenerator: false` so disposal never destroys shared session
  - Public `ResetSharedResourcesAsync()` for app shutdown / test cleanup
- **Performance impact:** 15-35× faster on repeated static calls (only ~10-20ms per call after init)
- **Opt-out:** `UseSharedResources = false` creates fresh resources per call for strict isolation

### Sark — LoadAsync Bounds Checking
- **Vulnerability:** Malicious / corrupted `.bin` files could specify huge `toolCount`, `embeddingDim`, `vectorLength` causing OOM DoS
- **Severity:** P1 (High)
- **Bounds added:**
  - `toolCount` ∈ [0, 100_000]
  - `embeddingDim` ∈ [0, 8192]
  - `vectorLength == embeddingDim` (consistency check)
- **Impact:** Eliminates OOM DoS; all 76 existing tests pass; no breaking changes

### Yori — Tester: Phase 2 Test Coverage
- **Tests added:** 9 new tests for singleton initialization, reuse, reset, and bounds validation
- **Result:** All tests pass; total test count 85 / 85

## Verification

- ✅ Build: `dotnet build ElBruno.ModelContextProtocol.slnx` clean
- ✅ Tests: 85 / 85 passing
- ✅ Performance: Measured 15-35× speedup on repeated static calls

## Decisions Formalized

1. **Shared Singletons** (Tron) — Double-checked locking, no disposal until ResetSharedResourcesAsync(), trades process-lifetime resource for 15-35× performance
2. **LoadAsync Bounds** (Sark) — Three validation checks (toolCount, embeddingDim, vectorLength consistency) prevent OOM attacks

## Next Phase

**Phase 3** targets supply-chain security hardening: NuGet lock files, SHA-pinned GitHub Actions, path traversal guards.
