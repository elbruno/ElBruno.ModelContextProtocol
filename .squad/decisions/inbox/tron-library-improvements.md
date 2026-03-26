# Decision: Library Improvements — IToolIndex, IEmbeddingGenerator, Serialization

**Date:** 2025-07-18  
**Agent:** Tron  
**Status:** Implemented

## Context

The MCPToolRouter library needed 5 improvements to make it production-ready: options pattern, SIMD performance, embedding abstraction, serialization, and DI support.

## Key Decisions

### 1. IEmbeddingGenerator ownership model
When a custom `IEmbeddingGenerator<string, Embedding<float>>` is provided externally, the `ToolIndex` does NOT own it and will not dispose it. Only internally-created generators are disposed. This follows the standard .NET ownership pattern.

### 2. Package versions pinned to 10.x
All `Microsoft.Extensions.*` packages use version `10.0.3`/`10.3.0` to match transitive dependencies from `ElBruno.LocalEmbeddings 1.1.5`. Lower versions trigger `NU1605` downgrade errors treated as build failures.

### 3. Obsolete overload disambiguation
The old `CreateAsync(tools, LocalEmbeddingsOptions?)` had its second parameter changed from optional to required to avoid overload ambiguity with the new `CreateAsync(tools, ToolIndexOptions?)`. Marked `[Obsolete]`.

### 4. ReaderWriterLockSlim for thread safety
Dynamic index mutations (AddToolsAsync, RemoveTools) use `ReaderWriterLockSlim` to allow concurrent reads during search while serializing writes. The lock is disposed in `DisposeAsync`.

## Impact
- New public API surface: `IToolIndex`, `ToolIndexOptions`, `ServiceCollectionExtensions`
- All 21 existing tests pass without modification
- Samples and dependents build cleanly
