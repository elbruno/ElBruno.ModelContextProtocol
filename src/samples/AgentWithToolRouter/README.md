# AgentWithToolRouter Sample

Demonstrates using **MCPToolRouter** with the [Microsoft Agent Framework](https://github.com/microsoft/Agents-for-net) (`Microsoft.Agents.AI.OpenAI`) to semantically filter function tools before passing them to an AI Agent — reducing token usage and improving response quality.

## What it shows

| Concept | Details |
|---------|---------|
| **Semantic tool routing** | Uses `ToolIndex` to find the most relevant tools for each prompt |
| **Microsoft Agent Framework** | Creates agents with `ChatClient.AsAIAgent()` |
| **Function tools** | 11 tools across 6 domains (weather, email, calendar, files, math, translation) |
| **Token savings** | Only 2–3 tools sent per prompt instead of all 11 |
| **Multi-turn sessions** | Agent remembers context across turns via `AgentSession` |
| **Flexible auth** | API key or `DefaultAzureCredential` (passwordless) |

## Prerequisites

- .NET 10.0 SDK
- Azure OpenAI resource with a deployed model (e.g., `gpt-4o`)
- Azure credentials (API key or Azure Identity)

## Setup

```bash
cd src/samples/AgentWithToolRouter

# Required
dotnet user-secrets set "AzureOpenAI:Endpoint" "https://your-resource.openai.azure.com/"
dotnet user-secrets set "AzureOpenAI:DeploymentName" "gpt-4o"

# Optional — omit to use DefaultAzureCredential (az login)
dotnet user-secrets set "AzureOpenAI:ApiKey" "your-api-key"
```

## Run

```bash
dotnet run
```

## How it works

1. **Register tools** — 11 static C# methods annotated with `[Description]` are wrapped as `AIFunction` instances. Matching MCP `Tool` definitions are created for semantic indexing.

2. **Build index** — `ToolIndex.CreateAsync()` generates local embeddings for all tool descriptions (one-time cost).

3. **Route per prompt** — For each user prompt, `SearchAsync()` returns the top-K most relevant tools by cosine similarity.

4. **Create agent** — A new `AIAgent` is created with **only** the filtered tools, dramatically reducing the token footprint.

5. **Run agent** — The agent processes the prompt, calls the relevant tool(s), and returns a natural-language answer.

6. **Multi-turn** — A session demo shows the agent retaining context across multiple conversation turns.
