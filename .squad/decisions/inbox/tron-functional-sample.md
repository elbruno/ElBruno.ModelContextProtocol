# Decision: FunctionalToolsValidation Sample Architecture

**Author:** Tron  
**Date:** 2025-07-18  
**Status:** Implemented

## Context

Bruno requested a sample that validates MCPToolRouter correctness with 50+ real tool implementations — tools that actually compute results, not stubs.

## Decision

Used a `Dictionary<string, Func<JsonElement, string>>` dispatch pattern for the tool registry, with inline `JsonSerializer.SerializeToElement(new { ... })` for schema definitions. This keeps all 53 tools + schemas in a single Program.cs without additional classes.

## Alternatives Considered

- **Reflection-based dispatch:** Would reduce boilerplate but adds complexity and obscures the tool implementations.
- **Separate tool classes per domain:** Cleaner separation but over-engineered for a sample.
- **Exact match validation:** Rejected in favor of fuzzy (case-insensitive contains) since LLMs wrap answers in natural language.

## Outcome

Single-file sample with 53 real tools, 12 validated scenarios, and a clear comparison table. Builds with 0 warnings, 0 errors on net8.0.
