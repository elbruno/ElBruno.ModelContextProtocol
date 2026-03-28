# Decision: Upgrade ElBruno.LocalLLMs to v0.6.1 and integrate model metadata

**Author:** Tron (Core Dev)
**Date:** 2025-07-28
**Status:** Implemented

## Context
ElBruno.LocalLLMs v0.6.1 added `ModelInfo` property exposing `ModelMetadata` (MaxSequenceLength, ModelName, VocabSize). This was the feature we requested in ElBruno.LocalLLMs issue #3.

## Decision
- Upgraded from v0.5.0 to v0.6.1
- Added auto-detection of model context window to compute safe prompt truncation limits
- Reverted `PromptDistillerOptions.MaxPromptLength` default from 300 (band-aid) back to 4096 (proper default for cloud LLMs)
- Model metadata auto-populates via `DetectedModelMaxSequenceLength` (internal) on `ToolRouterOptions`, flowing through `ToDistillerOptions()`
- Formula: `safeChars = (MaxSequenceLength - 70 reserved) * 4 chars/token`
- The auto-computed value only takes precedence when it's *smaller* than MaxPromptLength

## Impact
- Zero-setup `SearchUsingLLMAsync` automatically adapts to model context window
- Users passing their own `IChatClient` can set `ModelMaxSequenceLength` on `PromptDistillerOptions` manually
- Fully backward-compatible: existing code sees 4096 default (was 300 band-aid), no breaking changes
