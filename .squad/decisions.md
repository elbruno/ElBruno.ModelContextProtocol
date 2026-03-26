# Squad Decisions

## Active Decisions

### 1. Single-Target Framework (net10.0 only)

**Date:** 2026-03-26  
**Agent:** Tron  
**Status:** Implemented

#### Context
ElBruno .NET conventions specify multi-targeting `net8.0;net10.0` for library projects to support both LTS and latest .NET versions.

#### Decision
MCPToolRouter library targets **net10.0 only** (single target).

#### Rationale
- **Dependency constraint:** ElBruno.LocalEmbeddings 1.1.5 (latest) only supports net10.0
- **No workaround available:** Cannot multi-target when critical dependency is single-platform
- **Trade-off accepted:** Narrower compatibility vs. using latest embedding library version

#### Impact
- Library requires .NET 10.0 SDK to build and run
- CI/CD pipelines must use .NET 10.0 SDK
- Consumers must target net10.0 or later
- May revisit when ElBruno.LocalEmbeddings adds net8.0 support

---

### 2. Shared Test Fixture for ToolIndex Tests

**Date:** 2026-03-26  
**Author:** Yori (Tester/QA)  
**Status:** Implemented

#### Context

The `ToolIndex.CreateAsync` method downloads a ~90MB ONNX embedding model on first use via `LocalEmbeddingGenerator`. Running 21 tests where each creates its own `ToolIndex` would result in:
- Repeated model downloads (or cache hits with delays)
- Longer test execution time
- Unnecessary resource consumption

#### Decision

Implement a shared test fixture using xUnit's `IClassFixture<T>` pattern:

```csharp
public class SharedToolIndexFixture : IAsyncLifetime
{
    public ToolIndex Index { get; private set; } = null!;
    public Tool[] Tools { get; } = new[] { /* 5 sample tools */ };
    
    public async Task InitializeAsync() => Index = await ToolIndex.CreateAsync(Tools);
    public async Task DisposeAsync() => await Index.DisposeAsync();
}
```

Tests that need a pre-built index use `IClassFixture<SharedToolIndexFixture>` and inject the fixture.

#### Consequences

**Positive:**
- Model downloads only once per test run
- Faster test execution (~8s vs potentially 60s+)
- Reduced resource consumption
- Tests still isolated (fixture provides read-only access)

**Negative:**
- Some tests that specifically test index creation cannot use the shared fixture
- Tests share the same 5 tools, requiring separate index creation for different tool sets

#### Implementation Notes

The following tests create their own index (cannot use shared fixture):
- `CreateAsync_WithNullTools_ThrowsArgumentNullException`
- `CreateAsync_WithEmptyTools_ThrowsArgumentException`
- `CreateAsync_WithSingleTool_ReturnsIndexWithCount1`
- `CreateAsync_WithMultipleTools_ReturnsCorrectCount`
- `CreateAsync_WithToolWithNoDescription_Succeeds`
- `SearchAsync_WithToolWithNoDescription_StillReturnsResults`
- `DisposeAsync_CanBeCalledMultipleTimes`

All other tests use the shared fixture for performance optimization.

---

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction
