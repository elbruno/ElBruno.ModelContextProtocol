# TokenComparisonMax Sample

> **EXTREME** token-savings demo with **120+ MCP tool definitions** across 12 domains, powered by [Spectre.Console](https://spectreconsole.net/) for a beautiful terminal experience.

## What It Does

This sample showcases how **MCPToolRouter** scales to real-world scenarios where dozens (or hundreds) of tools are registered. It:

1. Defines **120 realistic MCP tools** across 12 domains (weather, email, file system, database, calendar, math, translation, web, DevOps, security, analytics, AI/ML).
2. Runs **12 test prompts** (one per domain) against Azure OpenAI—once with all tools, once with only the top-5 routed tools.
3. Displays a **live-updating table** in the terminal as each scenario processes.
4. Prints a **final summary table** with per-scenario and total token savings.

## Prerequisites

- .NET 8.0 SDK or later
- An Azure OpenAI deployment (e.g., `gpt-5-mini`)

## Setup

```bash
cd src/samples/TokenComparisonMax

dotnet user-secrets set "AzureOpenAI:Endpoint" "https://your-resource.openai.azure.com/"
dotnet user-secrets set "AzureOpenAI:ApiKey" "your-api-key"
dotnet user-secrets set "AzureOpenAI:DeploymentName" "gpt-5-mini"
```

## Run

```bash
dotnet run --project src/samples/TokenComparisonMax
```

## Expected Output

- A Figlet banner followed by a live-updating table showing each scenario's progress.
- Per-scenario token counts: standard mode (all 120 tools) vs. routed mode (top 5 tools).
- A summary table with aggregate savings (typically **90%+** input token reduction).
- A panel highlighting the bottom-line savings.
