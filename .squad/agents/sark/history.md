# Sark — History

## Context
- Project: ElBruno.MCPToolRouter — .NET library for semantic MCP tool routing
- Stack: .NET 8/10, C#, xUnit, ElBruno.LocalEmbeddings, ElBruno.LocalLLMs, ONNX
- Owner: Bruno Capuano

## Learnings

### 2025-07-24 — Full Security Audit

**Scope:** All .cs files, .csproj files, .yml workflows, .gitignore, Directory.Build.props

**Key findings (8 medium, 14 low/informational, 0 critical):**

1. **CI/CD:** publish.yml has a script injection vector via `github.event.inputs.version` — direct interpolation into bash. Fix: use env vars. All GitHub Actions pinned to tags, not SHAs.
2. **Supply chain:** No `packages.lock.json` / `RestorePackagesWithLockFile`. NuGet packages not signed (acceptable for v0.1.0).
3. **Code:** `ToolIndex.LoadAsync` deserializes binary data without bounds checks on `toolCount` and `vectorLength` — potential OOM DoS. `EmbeddingModelInfo.ResolveModelDirectory` does not validate model name against path traversal (low risk since developer-configured).
4. **Input validation:** Solid across all public APIs. `ArgumentNullException.ThrowIfNull` and `ArgumentException.ThrowIfNullOrWhiteSpace` used consistently.
5. **Prompt injection:** `PromptDistiller` passes raw user input to LLM — inherent LLM prompt injection risk. Impact limited to tool *selection*, not execution. Document as known limitation.
6. **Secrets:** Clean — no hardcoded credentials. Samples properly use `dotnet user-secrets`.
7. **Model downloads:** Delegated to upstream `ElBruno.LocalEmbeddings` — integrity verification cannot be confirmed from this repo. Recommend separate audit.
8. **Thread safety:** `ReaderWriterLockSlim` is thread-affine. Current code is safe (no awaits inside lock), but fragile for future refactoring.

**Report:** `.squad/decisions/inbox/sark-security-audit.md`

### 2026-03-28 — Security Audit Complete + Decision Merged

Completed comprehensive security audit as part of coordinated audit sprint with Tron (performance) and Flynn (synthesis). Audit identified 0 critical, 8 medium, and 14 low/informational security findings across library code, dependencies, CI/CD workflows, and supply chain.

**Key Security Findings (P0-P3):**
- P0 (Fix immediately): Script injection in publish.yml via unescaped `github.event.inputs.version` (line 35-36). Solution: use environment variables instead of direct interpolation.
- P1 (High): No NuGet lock files (no `packages.lock.json`, `RestorePackagesWithLockFile` not set). Enables dependency confusion attacks.
- P1 (High): GitHub Actions not SHA-pinned. All actions use mutable tags (@v4, @v7) instead of commit SHAs. Mutation risk.
- P1 (High): LoadAsync deserializes untrusted binary data without bounds checking. Malicious `.bin` file with `toolCount = 2_000_000_000` causes OOM.
- P2 (Medium): Model name path traversal in ResolveModelDirectory. Uses modelName directly with Replace without validating against ".." or absolute paths.
- P2 (Medium): PromptDistiller inherent prompt injection risk. LLM can be manipulated to return malicious tool selections.
- P3 (Low-impact): ReaderWriterLockSlim is thread-affine. Current code is safe (no awaits inside lock), but fragile for future refactoring.
- ✅ Input validation: Solid across all public APIs (ArgumentNullException.ThrowIfNull, ArgumentException.ThrowIfNullOrWhiteSpace)
- ✅ Secrets: Clean — no hardcoded credentials, samples use dotnet user-secrets correctly
- ✅ Permissions: Workflow permissions appropriately scoped (read for PR, OIDC write for publish)

**Integration into 5-Phase Plan:**
- Phase 1 (Immediate): Fix script injection in publish.yml (Item 1.2)
- Phase 2: Add LoadAsync bounds checking (Item 2.3)
- Phase 3: Enable NuGet lock files (Item 3.1), SHA-pin GitHub Actions (Item 3.2), model name path validation (Item 3.3)
- Phase 5: Add `dotnet nuget audit` to CI (Item 5.4)

**Decision merged to:** `.squad/decisions.md` (Decision §9 — 5-Phase Implementation Roadmap)
**Orchestration logged to:** `.squad/orchestration-log/2026-03-28T02-55-sark-security-audit.md`
