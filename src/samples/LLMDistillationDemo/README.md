# LLM Distillation Demo

Demonstrates **why Mode 2 (LLM-assisted routing) exists** by comparing it against Mode 1 (embeddings-only) across 7 scenarios with long, verbose, realistic user prompts.

## The Problem

Real users don't write `"get weather Tokyo"`. They write:

> *"Hey, so I was talking to my colleague Sarah and she mentioned that Tokyo has amazing cherry blossoms in spring. I'm actually thinking of going there next month for a conference... anyway, I need to figure out what the weather will be like because I need to pack appropriate clothes. Also, while I'm at it, I should probably translate some of my presentation slides to Japanese..."*

Mode 1 (embeddings-only) searches using the **entire verbose prompt** as the query vector — noise words, tangents, and all. Mode 2 first distills the prompt into a focused intent using a local LLM, then searches with that clean query.

## What It Shows

For each scenario, the demo outputs:

1. **The original long prompt** — realistic conversational text
2. **The LLM-distilled intent** — what the local model extracted as the core request
3. **Mode 1 results** — top-5 tools from embeddings search on the raw prompt
4. **Mode 2 results** — top-5 tools from embeddings search on the distilled intent
5. **Verdict** — which mode selected more relevant tools, with a running tally

## 7 Scenarios

| # | Scenario | Prompt Style | Key Tools |
|---|----------|-------------|-----------|
| 1 | Trip Planning Ramble | Conversational, multi-topic | weather, translate, location |
| 2 | Kitchen-Sink Email | Meandering with tangents | email, analytics, reports |
| 3 | Developer Stream of Consciousness | Technical ramble | database, DevOps, security |
| 4 | Vague Meeting Request | Indirect coordination | calendar, meetings, timezones |
| 5 | Research Ramble | Inspiration-to-action | sentiment, spreadsheets, data |
| 6 | Multi-Domain Chaos | Rapid context switching | Slack, email, calendar, translate |
| 7 | Procrastinator's Todo List | Everything at once | calendar, email, todos, security |

## Prerequisites

- **.NET 8.0 or higher**
- **~1.5 GB disk space** — first run downloads the embedding model (~90 MB) and local LLM (~500 MB)
- No API keys required — everything runs locally
- *Optional:* For GPU acceleration on Windows, also add `Microsoft.ML.OnnxRuntimeGenAI.DirectML` package via `dotnet add package Microsoft.ML.OnnxRuntimeGenAI.DirectML` for 2–5x faster inference

## Running the Sample

```bash
cd src/samples/LLMDistillationDemo
dotnet run
```

## How It Works Under the Hood

```
Mode 1:  verbose prompt ──────────────────────────> embeddings ──> cosine search ──> tools
Mode 2:  verbose prompt ──> local LLM ──> intent ──> embeddings ──> cosine search ──> tools
```

The only difference is **what goes into the embedding search**. Mode 2 adds one step (local LLM distillation) that strips away conversational noise and extracts actionable intent.

## Key APIs

```csharp
// Mode 1 — Embeddings only (no LLM)
var results = await ToolRouter.SearchAsync(prompt, tools, topK: 5);

// Mode 2 — LLM distillation (zero-setup, downloads local model automatically)
var results = await ToolRouter.SearchUsingLLMAsync(prompt, tools, topK: 5);
```

## When to Use Each Mode

| | Mode 1 | Mode 2 |
|---|--------|--------|
| **Best for** | Short, clear prompts | Long, verbose, conversational prompts |
| **Latency** | Fast (embeddings only) | Slower (LLM + embeddings) |
| **Dependencies** | Embedding model only | Embedding model + local LLM |
| **Example** | "Send email to team" | "OK so I've been meaning to do this all week..." |

## Tool Set

30 tools across 8 domains: Weather & Location, Email & Communication, Calendar & Tasks, Files & Documents, Web & Information, Math & Data, Code & Development, DevOps & Infrastructure.

## Notes

- **First run is slower** — downloads and initializes ONNX models (~10-15 seconds)
- **Subsequent runs are fast** — models are cached locally
- **No API keys required** — uses local inference only
- The sample reuses a single `ToolIndex` and `LocalChatClient` across all scenarios for performance

## Learn More

- [Main README](../../../README.md) — library documentation
- [McpToolRouting](../McpToolRouting/) — 3-scenario demo with static API
- [TokenComparisonMax](../TokenComparisonMax/) — token savings visualization with Spectre.Console
