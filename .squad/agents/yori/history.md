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
