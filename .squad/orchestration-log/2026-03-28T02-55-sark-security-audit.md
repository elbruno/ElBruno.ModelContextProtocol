# Orchestration Log — Security & Performance Audit Sprint

**Timestamp:** 2026-03-28T02:55:00Z  
**Session:** Security & Performance Audit + Phased Implementation Plan  
**Status:** Completed

## Spawn Manifest

### Sark (Security Engineer) — Background Task
**Status:** ✅ Completed  
**Model:** claude-sonnet-4.5

**Deliverables:**
- ✅ Comprehensive security audit of full repository
- ✅ Coverage: Library code, dependencies, CI/CD workflows, supply chain, secrets, file I/O
- ✅ Findings: 0 critical, 8 medium, 14 low/informational
- ✅ Key findings identified:
  - Script injection in publish.yml via unescaped `github.event.inputs.version`
  - Missing NuGet lock files (RestorePackagesWithLockFile)
  - LoadAsync unbounded allocation vulnerability (DoS via toolCount, vectorLength)
  - Model name path traversal in ResolveModelDirectory
  - LLM prompt injection risk in PromptDistiller
  - GitHub Actions not SHA-pinned
- ✅ Priority remediation order provided (7 phases)
- ✅ Report: `.squad/decisions/inbox/sark-security-audit.md`

## Technical Findings Summary

| Category | Severity | Finding | Recommendation |
|----------|----------|---------|-----------------|
| CI/CD | 🟡 Medium | Script injection in publish.yml line 35-36 | Use environment variables instead of direct interpolation |
| Supply Chain | 🟡 Medium | No NuGet lock files | Enable `RestorePackagesWithLockFile` in Directory.Build.props |
| Deserialization | 🟡 Medium | LoadAsync no bounds checks | Validate toolCount < 100,000, embeddingDim < 8192 |
| File I/O | 🟡 Medium | Model path traversal (developer-set) | Validate against ".." and absolute paths |
| LLM Security | 🟡 Medium | PromptDistiller prompt injection risk | Document as known limitation, add length limit (4096 chars) |
| CI/CD | 🟡 Medium | Actions not SHA-pinned | Pin to full commit SHA with tag comments |
| Thread Safety | 🟡 Medium | ReaderWriterLockSlim in async context | Document no-await invariant, consider SemaphoreSlim |
| Dependencies | 🟢 Low | Package versions current | Periodic audit with `dotnet list package --vulnerable` |
| Validation | 🟢 Low | Input validation solid | All public APIs use ArgumentNullException.ThrowIfNull |
| Secrets | 🟢 Low | No hardcoded credentials | Samples correctly use dotnet user-secrets |
| Permissions | 🟢 Low | Workflow permissions scoped | read/write isolation appropriate |
| OIDC | 🟢 Low | Trusted publishing | Current pattern secure for NuGet |

**Complexity Assessment:** Medium — Multiple independent findings, each addressable with low effort

---

### Tron (Core Dev) — Background Task
**Status:** ✅ Completed  
**Model:** claude-sonnet-4.5

**Deliverables:**
- ✅ Comprehensive performance analysis of entire codebase
- ✅ Coverage: Static API, embedding generation, file I/O, static DI, concurrency
- ✅ Findings: 3 high-impact, 6 medium-impact, 5 low-impact
- ✅ Key findings identified:
  - **P0:** QueryCacheSize is dead code — declared but never implemented (15-35× static API slowdown)
  - **P0:** Static API creates ONNX session per call (200-500ms cold, 50ms warm)
  - **P0:** DisposeAsync race condition (non-atomic bool check-and-set)
  - Shared ONNX session not cached (80-100MB per instance)
  - LoadAsync deserializes without bounds checks (potential OOM)
  - PriorityQueue inefficiency for top-K search
  - Zero-setup LLM overload creates LocalChatClient per call (500ms-2s)
  - Sync-over-async in ServiceCollectionExtensions (blocks thread)
  - No auto-persistence of tool indices
  - Binary format lacks InputSchema preservation
- ✅ Performance metrics provided (benchmarks in findings)
- ✅ Report: `.squad/decisions/inbox/tron-performance-audit.md`

## Performance Findings Summary

| Issue | Severity | Impact | Cost | Mitigation |
|-------|----------|--------|------|-----------|
| Unimplemented QueryCacheSize | 🔴 HIGH | Cached queries ignored, re-embedded every call | ~50-200ms per cached query | Implement LRU cache in ToolIndex |
| Static API ONNX recreation | 🔴 HIGH | 15-35× slower than instance API | 310-720ms per static call | Shared Lazy<IEmbeddingGenerator> singleton |
| DisposeAsync race condition | 🔴 HIGH | Double-dispose of resources under concurrency | Crash/corruption potential | Interlocked.Exchange instead of bool |
| No global ONNX session cache | 🟡 MEDIUM | Multiple ToolIndex = multiple ONNX sessions | 80-100MB per instance | Reference-counted singleton pool |
| LoadAsync no bounds checking | 🟡 MEDIUM | Malicious .bin file causes OOM | DoS vector | Add const limits (toolCount, embeddingDim) |
| PriorityQueue inefficiency | 🟡 MEDIUM | Full sort for top-K wasteful | O(N log N) vs O(N log K) | Use PriorityQueue<int, float> |
| Zero-setup LLM cold start | 🟡 MEDIUM | Model load + inference per call | 500ms-2s overhead | Shared Lazy<Task<LocalChatClient>> |
| Sync-over-async in DI | 🟡 MEDIUM | Blocks threadpool during registration | Potential starvation | AddMcpToolRouterAsync with IHostedService |
| No auto-persistence | 🟡 MEDIUM | Re-embed identical tools on restart | 200-500ms overhead | AutoPersistPath option with cache validation |
| Format v1 lacks InputSchema | 🟡 MEDIUM | Save/load loses function-calling metadata | Incomplete round-trip | Format v2 with JSON Tool serialization |

**Complexity Assessment:** High — 3 P0 items require immediate attention, multiple architectural improvements needed

---

### Flynn (Lead/Architect) — Background Task
**Status:** ✅ Completed  
**Model:** claude-opus-4.6

**Deliverables:**
- ✅ Synthesized Sark's security audit + Tron's performance audit
- ✅ Created comprehensive 5-phase, ~20-item implementation plan
- ✅ Risk assessment across all proposals
- ✅ Success metrics and KPIs for each phase
- ✅ Assignment matrix for Tron, Sark, Yori, Ram, Flynn
- ✅ Dependency mapping between items
- ✅ Key architectural decisions documented:
  - Shared ONNX singleton pattern with reference counting
  - Shared LocalChatClient for zero-setup LLM path
  - SemaphoreSlim migration from ReaderWriterLockSlim
  - LRU cache implementation for QueryCacheSize
  - Format v2 with backward compatibility for binary save/load
- ✅ Phasing rationale: P0 fixes → high-impact perf → supply chain hardening → advanced opt → docs
- ✅ Report: `.squad/decisions/inbox/flynn-phased-plan.md`

## Phased Plan Summary

| Phase | Goal | Items | Timeline | Owners |
|-------|------|-------|----------|--------|
| **Phase 1** | Critical fixes (broken/dangerous) | 1.1 (QueryCache), 1.2 (Publish.yml injection), 1.3 (DisposeAsync race) | Immediate (sprint 1) | Tron, Sark |
| **Phase 2** | High-impact perf (15-35× speedup) | 2.1 (Shared generator), 2.2 (Shared LLM), 2.3 (LoadAsync bounds), 2.4 (PriorityQueue) | Sprint 2 | Tron |
| **Phase 3** | Security hardening + CI | 3.1 (Lock files), 3.2 (SHA pins), 3.3 (Path validation), 3.4 (Prompt length), 3.5 (SemaphoreSlim) | Sprint 3 | Sark, Tron |
| **Phase 4** | Advanced optimization | 4.1 (AutoPersist), 4.2 (Format v2), 4.3 (Block I/O), 4.4 (Async DI), 4.5 (Memory spike guard) | Sprint 4 | Tron |
| **Phase 5** | Documentation | 5.1 (Perf guide), 5.2 (Prompt injection docs), 5.3 (DI limitation docs), 5.4 (nuget audit), 5.5 (Model security) | Ongoing | Ram, Sark |

## Risk Assessment

- **Shared singleton memory leak:** Implement with `WeakReference` or reference counting; document lifecycle
- **LRU cache invalidation:** Clear cache in `AddToolsAsync`/`RemoveTools`; add regression tests
- **NuGet lock file CI failures:** Document `dotnet restore --force-evaluate` workflow; enable Dependabot
- **SHA-pinned actions fall behind:** Add Dependabot `github-actions` ecosystem config
- **Format v2 breaks v1 files:** Implement v1→v2 migration in reader; log warning on v1 load

---

## Quality Metrics

| Metric | Target |
|--------|--------|
| Security Audit Coverage | 100% of .cs, .csproj, .yml, .gitignore, Directory.Build.props |
| Performance Audit Coverage | 100% of codebase |
| Findings Severity | 0 Critical, 8 Medium (Security), 3 High + 6 Medium (Perf) |
| Remediation Priority Order | 7-phase dependency chain established |
| Architecture Sign-off | Flynn review on Items 2.1, 2.2, 4.4 (shared singletons, async DI) |

---

**Orchestrated by:** Scribe  
**Team:** Sark, Tron, Flynn  
**Outcome:** Comprehensive security & performance analysis complete. Phased implementation plan ready for team consensus.
