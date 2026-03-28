# Decision: Revert ModelInfo Auto-Detection for Prompt Truncation

**Author:** Tron (Core Dev)
**Date:** 2025-07-22
**Status:** Implemented

## Context

In v0.6.1, we added auto-detection logic that read `ModelInfo.MaxSequenceLength` from `LocalChatClient` and used it to dynamically compute the safe prompt length for distillation. The intent was to eliminate manual tuning — the library would auto-adapt to whatever model was loaded.

## Problem

`genai_config.json` for Phi-3.5 reports `MaxSequenceLength = 131072` (theoretical context window), but the actual ONNX runtime compiled model has a much smaller limit (~128 tokens). The auto-detection trusted the config value, computed `(131072 - 70) * 4 = 524,008` safe characters, so prompts were never truncated. The LLM call then failed with token overflow, the try-catch caught the error, and fell back to the original prompt — making Mode 2 (LLM-distilled) identical to Mode 1 (embeddings-only). Distillation was silently broken.

## Decision

Revert all auto-detection logic. Restore the conservative 300-character default for `PromptDistillerOptions.MaxPromptLength`. Keep `ToolRouterOptions.MaxPromptLength` at 4096 (suitable for cloud LLMs). Model metadata display in samples is preserved for diagnostics.

## Rationale

- Model config files are unreliable for runtime limits — the compiled ONNX model may have different constraints than what the config advertises.
- A conservative hardcoded default (300 chars) is safer and predictable. Users can still override it.
- Auto-detection that silently fails is worse than no auto-detection — it creates a false sense of correctness.
- The try-catch safety net is good for resilience but should not be the primary truncation mechanism.

## Impact

- `PromptDistillerOptions.MaxPromptLength` default: 4096 → 300
- Removed: `ModelMaxSequenceLength`, `DetectedModelMaxSequenceLength`, `_sharedModelMaxSequenceLength`, auto-compute logic, LogMessage 202
- No breaking API changes (removed properties were internal or newly added)
- All 85 tests pass
