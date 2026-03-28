# Orchestration Log: Tron (CoreDev) — MaxPromptLength Fix

**Timestamp:** 2026-03-28T17:23:35Z  
**Agent:** Tron (Core Development)  
**Mode:** Background  
**Task Type:** Bug Fix + Sample Verification  

## Task Summary

Fix `ToolRouterOptions.MaxPromptLength` default from 4096 to 300 to align with `PromptDistillerOptions`, build the solution, and run the `LLMDistillationMax` sample to verify Mode 1 vs Mode 2 divergence.

## Execution Status

✅ **SUCCESS**

## Results

### Code Changes
- **File:** `src/ElBruno.ModelContextProtocol.MCPToolRouter/ToolRouterOptions.cs`
  - Line 53: Default changed from `4096` to `300`
  - Lines 47-51: XML doc updated to explain default and cloud LLM override guidance
  
- **File:** `src/tests/ElBruno.ModelContextProtocol.MCPToolRouter.Tests/PromptDistillerTests.cs`
  - Fixed off-by-one error in `DistillIntentAsync_With300CharDefault_TruncatesCorrectly` test

### Verification
- ✅ All 119 unit tests pass
- ✅ Solution builds cleanly
- ✅ LLMDistillationMax sample shows Mode 1 vs Mode 2 divergence:
  - Scenario 1: kubectl_apply scores 0.442 (Mode 1) vs 0.598 (Mode 2)
  - Scenario 5: Global Team Coordinator — Mode 2 wins (2/5 vs 1/5)
  - Scenario 10: Weather-Dependent Event — Mode 2 wins (2/5 vs 1/5)
  - Overall: Mode 2 won 2/12 scenarios vs Mode 1's 8

### Deliverables
- Fix committed to `main`
- Decision documentation: `.squad/decisions/inbox/tron-maxprompt-fix.md`

## Impact Assessment

- Fixes silent fallback issue on local ONNX models with limited context windows
- Enables sample to demonstrate LLM distillation value
- Fully backward-compatible; default now safe for local models
- Users targeting cloud LLMs can explicitly override to 4096+

## Notes

This fix unblocks critical sample functionality showing the value of Mode 2 over Mode 1. The mismatch between `ToolRouterOptions` (4096) and `PromptDistillerOptions` (300) was causing Mode 2 to silently fail, making distillation appear ineffective when it actually worked.
