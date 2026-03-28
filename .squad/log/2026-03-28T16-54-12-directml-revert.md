# Session Log — DirectML Revert + Upstream Issue

**Timestamp:** 2026-03-28T16:54:12Z  
**Team:** Tron (Core Dev) + Coordinator  
**Commit:** (pushed to main with 9 files)

## Overview

Reverted DirectML GPU acceleration in sample projects back to CPU-only runtime after hardware incompatibility on Bruno's machine. Root cause analysis revealed `ExecutionProvider.Auto` fails hard instead of gracefully falling back to CPU when DirectML is unavailable. Upstream issue created in ElBruno.LocalLLMs to fix this fallback behavior.

## Key Results

✅ **Package Reversion:** LLMDistillationMax + LLMDistillationDemo reverted to CPU-only `Microsoft.ML.OnnxRuntimeGenAI`  
✅ **Build Status:** Clean build, 115 unit tests passing  
✅ **Documentation:** README + sample READMEs updated with CPU as default, GPU as optional  
✅ **Upstream Issue:** Created issue #7 in elbruno/ElBruno.LocalLLMs to fix `ExecutionProvider.Auto` fallback  
✅ **Decision Artifact:** Revert decision merged to `.squad/decisions.md`

## Timeline

1. **Problem:** DirectML runtime fails on unsupported hardware with "Specified provider is not supported" error
2. **Analysis:** DirectML has no graceful fallback — auto-detection in ElBruno.LocalLLMs throws instead of falling back to CPU
3. **Decision:** Revert samples to CPU (safe default) and document GPU as optional
4. **Implementation:** 9 files updated (2 csproj, 2 lock files per project, 3 READMEs)
5. **Validation:** Clean build, 115 tests pass
6. **Upstream:** Coordinator filed issue in ElBruno.LocalLLMs to fix root cause

## Decision Artifact

**DirectML GPU Runtime for Sample Projects — Revert**  
See `.squad/decisions.md` § "Revert DirectML GPU Runtime to CPU-Only"  
Status: Implemented | Trade-off: CPU default + optional GPU | Root cause: ExecutionProvider.Auto fallback bug
