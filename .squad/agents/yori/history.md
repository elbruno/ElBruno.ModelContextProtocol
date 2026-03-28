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
