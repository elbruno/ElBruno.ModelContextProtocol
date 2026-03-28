# Decision: README Restructure for Developer Experience

**Date:** 2026  
**Owner:** Ram (DevRel)  
**Status:** Implemented

## Problem

README.md had three DX friction points:
1. "Quick Start" section duplicated Mode 1 content — developers didn't immediately see the API shape (two methods)
2. Value proposition ("70–85% token savings") buried deep in "How It Works — Technical Details"
3. Sample section bloated with 5 repeated Azure setup blocks + long per-sample descriptions (200+ words each)

## Solution

Three targeted changes:

### Change 1: TL;DR Hero Block
Replaced 20-line "Quick Start" with compact 4-line TL;DR showing both `SearchAsync` and `SearchUsingLLMAsync` side by side.

**Why:** Developers scanning README instantly understand: "Oh, two static methods, one for embeddings, one for LLM-assisted. Got it."

### Change 2: Early Value Proposition
Moved "reduce token costs by 70–85%" from line 232 to line 10 (opening description).

**Why:** Token savings is the key selling point — frontload it so new visitors understand the value within 10 seconds.

### Change 3: Consolidated Azure Setup + Trimmed Sample Descriptions
- Reduced per-sample descriptions from 200+ words to 1–2 sentences
- Consolidated 5 repeated Azure setup blocks into single unified "Azure OpenAI Setup" section
- Each sample now: 1–2 lines of description + table link to folder for details

**Why:** Scannability + DRY principle. Developers get sample overview instantly, then drill into their chosen sample folder for full setup steps.

## Metrics

- **File size:** 497 lines → 385 lines (−22% shorter)
- **Deletions:** 137 lines
- **Insertions:** 25 lines
- **Readability:** Structure now: badges → tagline + value prop → packages → TL;DR → how it works → modes → advanced → samples (tidy list) → unified setup → rest

## Preserved (No Changes)

- All badges and opening description
- Mode 1 and Mode 2 detailed code examples
- Pipeline diagrams
- Comparison table
- Advanced Features section
- Building from Source
- License, Author, Acknowledgments

## Outcome

README is more scannable, value prop is clearer, and Azure-sample friction is eliminated. Developers can now:
1. Read tagline + value prop in <10s
2. See API shape in TL;DR (1 line per method)
3. Pick a sample from table
4. Follow unified setup once, then follow sample-specific docs in folder
