# LLMDistillationMax

**Mode 2 (Hybrid Search) at Scale — 120+ Tools, 12 Long Prompts**

This sample demonstrates why **Mode 2 (hybrid search)** outperforms **Mode 1 (embeddings-only)** when user prompts are long, verbose, and conversational — the way humans actually write.

## The Problem

When a tool registry grows to 100+ tools and user prompts are paragraph-length (100–200+ words), raw embedding search gets **diluted by noise** — filler words, tangents, and stream-of-consciousness writing push the embedding vector away from the actual intent. Mode 2 solves this with a hybrid approach: it distills the prompt into comma-separated action phrases, searches each phrase individually, and merges those results with a baseline search on the original prompt. Baseline tools keep full scores; phrase-only matches get an 85% discount — so Mode 2 can only **improve** over Mode 1, never degrade.

## What This Sample Does

For each of 12 real-world, paragraph-length scenarios:

1. **Shows the original verbose prompt** — long, messy, multi-concern
2. **Distills it via a local LLM** — extracts comma-separated action phrases
3. **Runs Mode 1** — `ToolRouter.SearchAsync(prompt, tools)` — embeddings search on the raw prompt
4. **Runs Mode 2** — `ToolRouter.SearchUsingLLMAsync(prompt, tools)` — hybrid search (baseline + multi-query merge)
5. **Compares results** in a Spectre.Console table showing which tools each mode selected
6. **Scores each mode** against expected tools for the scenario

A final summary table shows win/loss/tie stats and overall accuracy comparison.

## Project Structure

The sample is split into focused files for readability:

| File | Purpose |
|------|---------|
| `Program.cs` | Main entry point — runs scenarios, builds Spectre.Console tables |
| `ToolDefinitions.cs` | 120+ MCP tool definitions across 12 domains |
| `Scenarios.cs` | 12 paragraph-length benchmark prompts with expected tools |
| `ScenarioResult.cs` | Record type capturing per-scenario Mode 1 vs Mode 2 results |

## API Used

```csharp
// Mode 1 — Embeddings only (no LLM needed)
var results = await ToolRouter.SearchAsync(prompt, tools);

// Mode 2 — Hybrid search (baseline + LLM distillation + multi-query merge)
var results = await ToolRouter.SearchUsingLLMAsync(prompt, tools);
```

## Requirements

- **.NET 8.0** or later
- **No Azure OpenAI** — runs 100% locally
- ~1.5 GB disk space for model downloads (first run only)
- *Optional:* For GPU acceleration, swap the ONNX runtime package — use `Microsoft.ML.OnnxRuntimeGenAI.DirectML` (Windows GPU) or `Microsoft.ML.OnnxRuntimeGenAI.Cuda` (NVIDIA). The default is CPU-only.

## Running

```bash
cd src/samples/LLMDistillationMax
dotnet run
```

> **First run** downloads the embedding model (~90 MB) and local LLM (~1.5 GB). Subsequent runs use the cached models.

## Sample Scenarios

| # | Scenario | Prompt Length | Domains Covered |
|---|----------|:------------:|-----------------|
| 1 | Infrastructure Chaos | ~130 words | DevOps, Database, Security |
| 2 | Marketing Multi-Hat Day | ~140 words | Web, Analytics, Email, Data |
| 3 | Developer Sprint Panic | ~150 words | CI/CD, Security, DevOps, Monitoring |
| 4 | Data Science Deep Dive | ~170 words | Database, ML, NLP, Data |
| 5 | Global Team Coordinator | ~160 words | Calendar, Translation, Messaging |
| 6 | Security Incident Response | ~180 words | Security, Monitoring, Auth |
| 7 | End-of-Quarter Reporting | ~170 words | Database, Math, Analytics, Web |
| 8 | ML Pipeline Debug Session | ~180 words | AI/ML, DevOps, Monitoring |
| 9 | New Developer Onboarding | ~170 words | Email, Calendar, DevOps, Security |
| 10 | Weather-Dependent Event | ~170 words | Weather, Email, Translation, Math |
| 11 | Content Pipeline Emergency | ~180 words | Language, Web, Translation |
| 12 | Late-Night Production Outage | ~190 words | Monitoring, DevOps, Messaging |

## Expected Output

```
╔══════════════════════════════════════════════╗
║  LLM Distill Max                             ║
╚══════════════════════════════════════════════╝

📦 Created 120 tool definitions across 12 domains
📋 12 scenarios with paragraph-length prompts

── Scenario 1/12: Infrastructure Chaos ──────────

📝 Original Prompt (130 words, 750 chars):
  So yesterday I was in a meeting with the VP...

🧠 LLM Distilled Phrases (1200ms):
  "check Kubernetes health, optimize database queries,
   rotate credentials, run vulnerability scan"

┌──────────────────────────────────────────────┐
│ #  Mode 1: Embeddings Only   Mode 2: Hybrid │
│ 1  ✓ check_service_health    ✓ query_database│
│ 2  ✗ get_metrics             ✓ kubectl_apply │
│ ...                                          │
└──────────────────────────────────────────────┘

  🏆 Mode 2 — Mode 1: 2/5 relevant | Mode 2: 4/5

... (12 scenarios) ...

┌──────────────────────────────────────────────┐
│          🏆 Overall Scoreboard               │
│ Metric              Mode 1      Mode 2       │
│ Scenarios Won         2           8           │
│ Total Hits          24/60       38/60         │
│ Hit Rate            40.0%       63.3%         │
└──────────────────────────────────────────────┘
```

## Comparison with Other Samples

| Sample | Tools | Prompt Style | Compares | Requires Azure |
|--------|:-----:|:------------:|----------|:--------------:|
| **LLMDistillationMax** (this) | 120+ | Long/verbose | Mode 1 vs Mode 2 (hybrid) accuracy | ❌ |
| LLMDistillationDemo | 30 | Long/verbose | Mode 1 vs Mode 2 accuracy | ❌ |
| TokenComparisonMax | 120+ | Short/direct | Token savings with Azure | ✅ |
| TokenComparison | 10 | Short/direct | Token savings with Azure | ✅ |

## Key Takeaway

> **Mode 1** works great for short, direct prompts.
> **Mode 2** uses hybrid search (baseline + multi-query phrase search) to guarantee it never does worse than Mode 1, while significantly improving results for long, noisy, multi-intent prompts.
