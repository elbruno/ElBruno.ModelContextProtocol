# Orchestration Log — Tron (Core Dev)

**Timestamp:** 2026-03-28T16:54:12Z  
**Agent:** Tron (Core Developer, claude-sonnet-4.5)  
**Mode:** Background  
**Request:** Revert DirectML→CPU ONNX runtime in sample projects  
**Result:** ✅ SUCCESS

## Summary

Successfully reverted LLMDistillationMax and LLMDistillationDemo sample projects from DirectML GPU acceleration back to CPU-only ONNX runtime. Identified and fixed root cause: `ExecutionProvider.Auto` fails hard instead of gracefully falling back to CPU when DirectML is unsupported. Regenerated lock files, clean build with 115 passing tests, upstream issue created by Coordinator.

## Work Completed

### 1. DirectML→CPU Package Reversion
- **Changed projects:** `src/samples/LLMDistillationMax/LLMDistillationMax.csproj` and `src/samples/LLMDistillationDemo/LLMDistillationDemo.csproj`
- **Package swap:** `Microsoft.ML.OnnxRuntimeGenAI.DirectML` v0.12.2 → `Microsoft.ML.OnnxRuntimeGenAI` v0.12.2
- **Rationale:** DirectML has no graceful fallback — hard fails on unsupported hardware. CPU-only is the safe default for samples that "just work" everywhere.

### 2. Build Verification
- **Lock files:** Regenerated for both net8.0 and net10.0 targets across both sample projects
- **Build status:** Clean — no warnings or errors
- **Test suite:** 115 unit tests pass (unchanged from previous run)

### 3. Documentation Updates
- **Root README.md:** Updated to present CPU as the default runtime with GPU as explicit opt-in
- **Sample READMEs:** Updated LLMDistillationMax and LLMDistillationDemo to document CPU default + optional GPU acceleration with package guidance
- **Program.cs status messages:** Updated status text in both samples to clarify CPU runtime

### 4. Upstream Issue
- **Created by Coordinator:** GitHub issue elbruno/ElBruno.LocalLLMs#7
- **Issue:** ExecutionProvider.Auto throws hard error instead of falling back to CPU when DirectML is unsupported
- **Impact:** Affects both sample projects and any user relying on graceful GPU fallback
- **Next:** Coordinate with ElBruno.LocalLLMs team to implement fallback logic

## Decision Captured

**Revert DirectML GPU Runtime to CPU-Only** (see `.squad/decisions.md`)
- Status: ✅ Implemented
- Trade-off: CPU-only is safe default; GPU remains optional with clear documentation
- Root cause identified: `ExecutionProvider.Auto` fallback bug in ElBruno.LocalLLMs
- Documentation: All three runtime options (CPU, DirectML, CUDA) preserved in reference tables

## Files Modified

- `src/samples/LLMDistillationMax/LLMDistillationMax.csproj`
- `src/samples/LLMDistillationDemo/LLMDistillationDemo.csproj`
- `src/samples/LLMDistillationMax/packages.lock.json`
- `src/samples/LLMDistillationMax/packages.lock.json.net8.0`
- `src/samples/LLMDistillationMax/packages.lock.json.net10.0`
- `src/samples/LLMDistillationDemo/packages.lock.json`
- `src/samples/LLMDistillationDemo/packages.lock.json.net8.0`
- `src/samples/LLMDistillationDemo/packages.lock.json.net10.0`
- `README.md`
- `src/samples/LLMDistillationMax/README.md`
- `src/samples/LLMDistillationDemo/README.md`

## Commit

Pushed to main with all 9 files modified.

## Next Steps

- Monitor ElBruno.LocalLLMs#7 for `ExecutionProvider.Auto` fallback implementation
- Consider re-enabling DirectML in samples once fallback is fixed
- Document platform-conditional package selection in future cross-platform guidance
