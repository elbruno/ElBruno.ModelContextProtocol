# Yori — History

## Project Context

- **Project:** ElBruno.MCPToolRouter
- **User:** Bruno Capuano
- **Stack:** .NET (C#), NuGet library, xUnit, ElBruno.LocalEmbeddings
- **Description:** .NET library that ingests MCP tool definitions, embeds them into a local vector store, and returns top-K most relevant tools via cosine similarity.

## Learnings

### 2025-01-XX: Comprehensive Test Suite for ToolIndex

**Task:** Expand test suite for `ToolIndex` class from basic starter tests to comprehensive coverage (21 tests total).

**Approach:**
- Created `SharedToolIndexFixture` using xUnit's `IClassFixture` pattern to avoid repeated ONNX model downloads (~90MB)
- Organized tests into logical groups: input validation, core functionality, semantic relevance, disposal, and edge cases
- Used parallel test execution with fixture sharing to minimize total test run time

**Test Categories Implemented:**
1. **Input Validation (8 tests):** Null/empty/whitespace prompts, zero/negative topK, negative minScore, null/empty tool lists
2. **Core Functionality (7 tests):** Index creation, count verification, result sorting, topK limits, minScore filtering
3. **Semantic Relevance (3 tests):** Weather/email query ranking, high minScore filtering
4. **Disposal (1 test):** Multiple dispose calls
5. **Edge Cases (2 tests):** Tools with null descriptions

**Key Findings:**
- `ArgumentException.ThrowIfNullOrWhiteSpace` throws `ArgumentNullException` for null values (not base `ArgumentException`)
- Shared fixture pattern works well for expensive initialization (ONNX model download only happens once)
- All 21 tests pass successfully on .NET 8.0

**Outcomes:**
- Complete test coverage of public API surface
- Fast test execution (~8 seconds total) despite model initialization
- Clear test organization for future maintenance

### 2025-XX-XX: PromptDistiller and ToolRouter Test Suites

**Task:** Write unit tests for `PromptDistiller` (static class) and `ToolRouter` (facade class) — 21 new tests total.

**Approach:**
- Created `FakeChatClient` implementing `IChatClient` to mock LLM responses without network calls
- Used message-capturing variant in PromptDistiller tests to verify system prompt forwarding
- Created `SharedToolRouterFixture` (same IClassFixture pattern as ToolIndex tests) for ToolRouter integration tests
- Added `Microsoft.Extensions.AI.Abstractions` v10.3.0 to test project csproj

**Test Files Created:**
1. `PromptDistillerTests.cs` — 8 tests covering:
   - Valid response handling and trimming
   - Fallback to original prompt for empty/short responses (< 5 chars)
   - Input validation (null client, null/whitespace prompt)
   - Custom options with system prompt verification
2. `ToolRouterTests.cs` — 13 tests covering:
   - Factory method validation (null/empty tools)
   - Instance routing (simple prompt, topK limits, minScore filtering)
   - Static one-shot routing convenience method
   - Distillation integration (with/without chat client, disabled distillation)
   - Index property access, multiple dispose safety

**Key Findings:**
- `ToolRouter.RouteAsync` instance method does NOT accept runtime options — `EnableDistillation` is set at creation time via `ToolRouterOptions`
- `PromptDistillerOptions` is defined inside `PromptDistiller.cs`, not a separate file
- `ChatResponse.Text` returns the message text; combined with `.Trim()` for whitespace handling
- All 50 tests pass (29 existing + 21 new) in ~29 seconds on net8.0

**Outcomes:**
- Full test coverage for both new public API surfaces
- FakeChatClient pattern reusable for future LLM-dependent tests
- SharedToolRouterFixture follows established SharedToolIndexFixture pattern

### 2025-XX-XX: EmbeddingModelInfo and EmbeddingModelStatus Tests

**Task:** Write xUnit tests for `EmbeddingModelInfo` (static class) and `EmbeddingModelStatus` (data class) — 10 tests total, written TDD-style ahead of Tron's implementation.

**Approach:**
- Created `EmbeddingModelInfoTests.cs` with 10 test methods covering:
  1. `DefaultModelName` const value verification
  2. `GetDefaultCacheDirectory()` — non-empty return, contains model name segments
  3. `GetModelDirectory()` — null options returns default, custom CacheDirectory, custom ModelPath
  4. `IsModelDownloaded()` — false for non-existent path (no real model needed)
  5. `GetStatus()` — valid status object, custom options reflected, absolute cache path
- Used `ElBruno.LocalEmbeddings.Options.LocalEmbeddingsOptions` from existing NuGet dependency (v1.1.5)
- Tests designed to NOT require actual model download — they verify path resolution and status queries

**Key Findings:**
- `LocalEmbeddingsOptions` lives in `ElBruno.LocalEmbeddings.Options` namespace (confirmed via NuGet XML docs)
- Properties available: `ModelName`, `ModelPath`, `CacheDirectory`, `PreferQuantized`, `EnsureModelDownloaded`, etc.
- Tests compile cleanly except for the missing `EmbeddingModelInfo`/`EmbeddingModelStatus` types (awaiting Tron's impl)

**Outcomes:**
- 10 tests ready in `EmbeddingModelInfoTests.cs` — will compile and pass once Tron delivers the source files
- No existing test files modified
- Library project continues to build cleanly

### 2025-XX-XX: Tests for Simplified Static API (SearchAsync / SearchUsingLLMAsync)

**Task:** Write comprehensive tests for the new static `SearchAsync` and `SearchUsingLLMAsync` methods on `ToolRouter`, replacing the deleted `RouteAsync` static convenience method.

**Approach:**
- Replaced Tron's 2 stub tests in the "Simplified Static API Tests" region with 8 comprehensive tests in two new regions
- Used existing `FakeChatClient` pattern for LLM-distilled tests (Mode 2)
- Each static test creates its own tools array (no shared fixture needed — static methods create/dispose their own index)

**Test Categories Implemented:**
1. **SearchAsync — Mode 1: Embeddings-only (5 tests):**
   - `SearchAsync_ReturnsRelevantTools` — basic happy path, weather tool ranked first
   - `SearchAsync_RespectsTopK` — verify topK=2 limits results to exactly 2
   - `SearchAsync_WithOptions_UsesCustomSettings` — passes ToolRouterOptions with TopK=1
   - `SearchAsync_NullPrompt_Throws` — null prompt throws ArgumentNullException
   - `SearchAsync_NullTools_Throws` — null tools throws ArgumentNullException
2. **SearchUsingLLMAsync — Mode 2: LLM-distilled (3 tests):**
   - `SearchUsingLLMAsync_WithFakeChatClient_ReturnsResults` — FakeChatClient distills to weather-related
   - `SearchUsingLLMAsync_NullChatClient_Throws` — null chatClient throws ArgumentNullException
   - `SearchUsingLLMAsync_DistillsPrompt` — verifies distillation changes ranking (vague prompt → email tool via fake LLM)

**Key Findings:**
- Tron had already partially replaced the old `RouteAsync_StaticOneShot_ReturnsResults` with basic stubs before I started — coordinated by expanding those stubs
- New static API parameter order changed: `SearchAsync(userPrompt, tools, ...)` vs old `RouteAsync(tools, userPrompt, ...)`
- All 67 tests pass (29 ToolIndex + 8 PromptDistiller + 13 existing ToolRouter + 8 new static + 10 EmbeddingModelInfo - some overlap) in ~38s on net8.0

**Outcomes:**
- Full test coverage for both new static entry points
- Old `RouteAsync` static test removed — no references to deleted API
- Instance-based `RouteAsync` tests unchanged (that API still exists)
- All 67 tests pass, 0 failures

### 2025-XX-XX: Phase 1 Tests — QueryCache LRU and Concurrent Dispose

**Task:** Write 9 tests for Tron's Phase 1 changes: QueryCacheSize LRU cache (5 tests) and DisposeAsync race condition fix (4 tests).

**Approach:**
- Added 7 tests to `ToolIndexTests.cs`: 5 cache tests + 2 ToolIndex dispose tests (concurrent + double)
- Added 2 tests to `ToolRouterTests.cs`: concurrent dispose + double dispose for ToolRouter
- Tests written against current code — Tron's cache implementation already in ToolIndex.cs, ToolIndex._disposed already `int` with Interlocked.Exchange

**Tests Added to ToolIndexTests.cs (QueryCache LRU):**
1. `QueryCache_WhenEnabled_ReturnsConsistentResults` — cache=10, search same prompt twice, verify identical ordering+scores
2. `QueryCache_WhenDisabled_StillWorks` — cache=0, search works normally
3. `QueryCache_ClearedOnAddTools` — populate cache, add relevant tool, verify it appears in results
4. `QueryCache_ClearedOnRemoveTools` — populate cache, remove tool, verify stale data not served
5. `QueryCache_EvictsOldEntries_WhenFull` — cache=2, search 3 prompts, verify no crashes + correct results

**Tests Added (Concurrent Dispose):**
6. `ToolIndex_ConcurrentDispose_DoesNotThrow` — 10 concurrent DisposeAsync tasks on one ToolIndex
7. `ToolRouter_ConcurrentDispose_DoesNotThrow` — 10 concurrent DisposeAsync tasks on one ToolRouter
8. `ToolIndex_DoubleDispose_DoesNotThrow` — sequential double dispose with explicit Record.ExceptionAsync
9. `ToolRouter_DoubleDispose_DoesNotThrow` — same pattern for ToolRouter

**Key Findings:**
- ToolIndex._disposed is already `int` with `Interlocked.Exchange` — concurrent dispose tests pass NOW
- ToolRouter._disposed is still `bool` (Tron hasn't updated it yet) — but the concurrent dispose test passes currently due to single-threaded async execution in xUnit; may expose races under load after Tron changes it
- QueryCache already fully implemented in ToolIndex.SearchAsync with FIFO eviction — all 5 cache tests pass NOW
- `ClearQueryCache()` already called in both `AddToolsAsync` and `RemoveTools` — cache invalidation tests pass

**Outcomes:**
- All 76 tests pass (67 existing + 9 new), 0 failures, ~43s on net8.0
- All 9 new tests compile and pass against current code
- Tests will continue to validate behavior after Tron completes remaining changes (ToolRouter._disposed → int)

### 2025-XX-XX: Phase 2 Tests — Shared Singleton & LoadAsync Bounds Checking

**Task:** Write 9 tests for Phase 2 changes: shared singleton resources for static API (5 tests) and LoadAsync bounds checking (4 tests).

**Approach:**
- Added 5 tests to `ToolRouterTests.cs` for shared singleton (Items 2.1/2.2)
- Added 4 tests to `ToolIndexTests.cs` for LoadAsync bounds checking (Item 2.3)
- Created `FakeEmbeddingGenerator` in ToolIndexTests for LoadAsync tests — avoids downloading ONNX model since validation errors fire before any embedding generation
- Each shared singleton test uses try/finally with `ResetSharedResourcesAsync()` to prevent test pollution

**Tests Added to ToolRouterTests.cs (Shared Singleton):**
1. `StaticSearch_WithSharedResources_ReturnsResults` — proves shared embedding generator works end-to-end
2. `StaticSearch_CalledTwice_ProducesConsistentResults` — verifies singleton stability (same input → identical results)
3. `ResetSharedResources_DoesNotThrow` — reset on cold state is safe
4. `ResetSharedResources_ThenSearch_StillWorks` — reset + re-search recreates resources correctly
5. `StaticSearch_WithUseSharedResourcesFalse_StillWorks` — non-shared path (fresh generator per call)

**Tests Added to ToolIndexTests.cs (LoadAsync Bounds Checking):**
6. `LoadAsync_WithNegativeToolCount_ThrowsInvalidDataException` — toolCount=-1 rejected
7. `LoadAsync_WithExcessiveToolCount_ThrowsInvalidDataException` — toolCount=200,000 rejected (MaxToolCount=100,000)
8. `LoadAsync_WithExcessiveEmbeddingDim_ThrowsInvalidDataException` — embeddingDim=10,000 rejected (MaxEmbeddingDimension=8,192)
9. `LoadAsync_WithMismatchedVectorDim_ThrowsInvalidDataException` — vectorLength≠embeddingDim rejected

**Key Findings:**
- `FakeEmbeddingGenerator` pattern is efficient — LoadAsync bounds checks throw before generator is used, so the fake never needs to produce real embeddings
- `UseSharedResources=false` creates a fresh ONNX session per call (slower but isolated) — test confirms this path works
- Binary stream format: version(int32) → toolCount(int32) → embeddingDim(int32) → [name(string) → description(string) → vectorLength(int32) → floats[]]
- `ResetSharedResourcesAsync()` is safe to call even when no shared resources exist (cold state)

**Outcomes:**
- All 85 tests pass (76 existing + 9 new), 0 failures, ~47s on net8.0
- Shared singleton lifecycle fully covered: create → use → reset → recreate
- LoadAsync attack surface covered: negative values, excessive values, dimension mismatches

### 2025-XX-XX: MaxPromptLength Regression Tests

**Task:** Add regression tests to prevent recurrence of the MaxPromptLength mismatch bug where `ToolRouterOptions.MaxPromptLength` defaulted to 4096 while `PromptDistillerOptions.MaxPromptLength` defaulted to 300, causing Mode 2 (LLM-distilled) to produce identical results to Mode 1 (embeddings-only) due to silent ONNX model failures.

**Approach:**
- Read existing test files (ToolRouterTests.cs, PromptDistillerTests.cs) to understand patterns and conventions
- Added 5 regression tests across two files:
  1. **ToolRouterTests.cs** — 3 tests in new "MaxPromptLength Regression Tests" region:
     - `ToolRouterOptions_MaxPromptLength_DefaultAlignedWithDistillerOptions` — key regression test: verifies both options classes have aligned defaults
     - `ToDistillerOptions_MaxPromptLength_MappedCorrectly` — verifies mapping with default AND custom values
     - Updated existing `ToolRouterOptions_DefaultMaxPromptLength` test from 4096 to 300
  2. **PromptDistillerTests.cs** — 2 tests in new "MaxPromptLength Regression Tests" region:
     - `DistillIntentAsync_WithLongPrompt_TruncatesBeforeSendingToLLM` — verifies LLM receives truncated prompt when MaxPromptLength exceeded
     - `DistillIntentAsync_With300CharDefault_TruncatesCorrectly` — verifies 300-char default truncation on 500+ char prompt

**Context - The Bug:**
- `ToolRouterOptions.MaxPromptLength` defaulted to 4096
- `PromptDistillerOptions.MaxPromptLength` defaulted to 300
- This mismatch caused local ONNX models to fail silently on long prompts and fall back to the original prompt
- Result: Mode 2 produced identical results to Mode 1, making distillation ineffective

**Key Findings:**
- Tron had already fixed `ToolRouterOptions.MaxPromptLength` default from 4096 → 300 (not yet committed when I started)
- Used existing `FakeChatClient` pattern from PromptDistillerTests to capture messages sent to LLM
- Tests verify both default alignment AND runtime truncation behavior
- All 121 tests pass (85 existing + 36 distillation/options tests from prior work)

**Outcomes:**
- 5 new regression tests committed alongside Tron's default fix
- Tests ensure future changes can't reintroduce the mismatch
- Key test: `DefaultAlignedWithDistillerOptions` will fail if someone changes one default without the other
- All 121 tests pass, 0 failures, ~68s on net8.0
