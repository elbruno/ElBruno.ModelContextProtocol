# ElBruno.ModelContextProtocol

[![CI Build](https://github.com/elbruno/ElBruno.ModelContextProtocol/actions/workflows/build.yml/badge.svg)](https://github.com/elbruno/ElBruno.ModelContextProtocol/actions/workflows/build.yml)
[![Publish to NuGet](https://github.com/elbruno/ElBruno.ModelContextProtocol/actions/workflows/publish.yml/badge.svg)](https://github.com/elbruno/ElBruno.ModelContextProtocol/actions/workflows/publish.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![GitHub stars](https://img.shields.io/github/stars/elbruno/ElBruno.ModelContextProtocol?style=flat)](https://github.com/elbruno/ElBruno.ModelContextProtocol)

## Semantic routing for MCP tools 🔀

ElBruno.ModelContextProtocol is a .NET library that makes it easy to find the right tools from Model Context Protocol (MCP) tool definitions. It uses semantic search powered by local embeddings to route prompts to the most relevant tools, enabling intelligent tool selection without external API calls. **By routing prompts to only relevant tools before sending to an LLM, you can reduce token costs by 70–85%.**

## Packages

| Package | NuGet | Downloads | Description |
|---------|-------|-----------|-------------|
| ElBruno.ModelContextProtocol.MCPToolRouter | [![NuGet](https://img.shields.io/nuget/v/ElBruno.ModelContextProtocol.MCPToolRouter.svg)](https://www.nuget.org/packages/ElBruno.ModelContextProtocol.MCPToolRouter) | [![Downloads](https://img.shields.io/nuget/dt/ElBruno.ModelContextProtocol.MCPToolRouter.svg)](https://www.nuget.org/packages/ElBruno.ModelContextProtocol.MCPToolRouter) | Semantic tool routing for MCP |

## MCPToolRouter

A high-performance semantic search engine for Model Context Protocol tools. MCPToolRouter indexes your MCP tool definitions and returns the most relevant tools for any prompt using vector similarity search.

### Installation

```bash
dotnet add package ElBruno.ModelContextProtocol.MCPToolRouter
```

## TL;DR

```csharp
// Mode 1: Embeddings only — fast, no LLM needed
var results = await ToolRouter.SearchAsync(prompt, tools, topK: 3);

// Mode 2: LLM-assisted — best for verbose/complex prompts
var results = await ToolRouter.SearchUsingLLMAsync(prompt, tools, topK: 5);
```

---

## How It Works — Two Modes

MCPToolRouter supports **two distinct modes** for finding the right tools. Choose based on your prompt complexity and speed requirements:

### The Pipeline

```
┌─────────────────────────────────────────────────────────────────┐
│  Mode 1: Embeddings Filter (Fast, Simple)                       │
│                                                                  │
│  User Prompt ──► Embed ──► Cosine Similarity ──► Top-K Tools    │
│                                                                  │
│  "What's the weather?" → [0.89 get_weather, 0.45 send_email]   │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│  Mode 2: LLM-Assisted Routing (Precise, Complex)                │
│                                                                  │
│  User Prompt ──► LLM Distill ──► Embed ──► Cosine ──► Top-K    │
│                                                                  │
│  "Hey, I was thinking about my trip and need to know if it's    │
│   going to rain in Tokyo..." → "Check weather in Tokyo"         │
│   → [0.91 get_weather, 0.52 get_forecast]                      │
└─────────────────────────────────────────────────────────────────┘
```

---

### Mode 1: Embeddings Filter (No LLM Needed) — One-Liner

**Use when:** Your prompt is clear and single-intent.

**What it does:** Embeds your prompt locally using ONNX and finds the top matching tools via cosine similarity.

**Speed:** ~1–5ms per query  
**Dependencies:** Local embeddings only (~90MB, auto-downloaded)

**Static API (Recommended):**

```csharp
using ElBruno.ModelContextProtocol.MCPToolRouter;
using ModelContextProtocol.Protocol;

var tools = new[]
{
    new Tool { Name = "get_weather", Description = "Get current weather for a location" },
    new Tool { Name = "send_email", Description = "Send an email message to a recipient" },
    new Tool { Name = "search_files", Description = "Search for files by name or content" },
    new Tool { Name = "calculate", Description = "Perform mathematical calculations" },
    new Tool { Name = "translate_text", Description = "Translate text between languages" }
};

// One-liner: route and get results immediately
var results = await ToolRouter.SearchAsync("What's the temperature outside?", tools, topK: 3);

foreach (var r in results)
    Console.WriteLine($"  {r.Tool.Name}: {r.Score:F3}");
// Output:
//   get_weather: 0.847
//   calculate: 0.312
//   translate_text: 0.201
```

**Advanced: Reusable Instance (for performance-critical scenarios)**

For servers, agents, or batch operations, build and reuse an index to avoid re-embedding:

```csharp
// Build once, reuse many times (e.g., in a web API or agent loop)
await using var index = await ToolIndex.CreateAsync(tools);

var results = await index.SearchAsync("What's the temperature outside?", topK: 3);
```

---

### Mode 2: LLM-Assisted Routing (Local Distillation) — One-Liner

**Use when:** Your prompt is verbose, multi-part, or conversational.

**What it does:** Uses a small local LLM (e.g., Qwen 2.5 0.5B) to distill the prompt to a single sentence, then runs the same embedding search. The LLM extracts the core intent, improving accuracy.

**Speed:** ~50–200ms (LLM inference + embedding)  
**Dependencies:** Local embeddings (~90MB) + local LLM (~1GB, auto-downloaded)

**Static API — Zero Setup (Recommended):**

```csharp
using ElBruno.ModelContextProtocol.MCPToolRouter;
using ModelContextProtocol.Protocol;

var tools = new Tool[] 
{
    new Tool { Name = "get_weather", Description = "Get current weather for a location" },
    new Tool { Name = "send_email", Description = "Send an email message to a recipient" },
    new Tool { Name = "search_files", Description = "Search for files by name or content" },
    new Tool { Name = "calculate", Description = "Perform mathematical calculations" },
    new Tool { Name = "translate_text", Description = "Translate text between languages" }
};

// One-liner: the local LLM is downloaded and managed automatically
var results = await ToolRouter.SearchUsingLLMAsync(
    "Hey, I was thinking about my trip next week and I need to know " +
    "if it's going to rain in Tokyo. Also remind me to call the dentist.",
    tools, topK: 5);

// The LLM distills this to something like: "Check weather in Tokyo, set reminder"
// Then embeddings find the best matching tools
foreach (var r in results)
    Console.WriteLine($"  {r.Tool.Name}: {r.Score:F3}");
// Output:
//   get_weather: 0.912
//   calculate: 0.287
//   translate_text: 0.156
```

**Advanced: Bring Your Own IChatClient**

Use any `IChatClient` (Azure OpenAI, Ollama, etc.) instead of the built-in local LLM:

```csharp
// Pass your own IChatClient for prompt distillation
var results = await ToolRouter.SearchUsingLLMAsync(
    "Complex prompt here...",
    tools, chatClient, topK: 5);
```

**Advanced: Reusable Instance (for performance-critical scenarios)**

For servers or agents running multiple queries, build and reuse a router instance:

```csharp
await using var router = await ToolRouter.CreateAsync(tools, chatClient);
var results = await router.RouteAsync("Complex prompt here...", topK: 5);
```

---

### When to Use Which Mode

| | **Mode 1: Embeddings** | **Mode 2: LLM-Assisted** |
|---|---|---|
| **Best for** | Clear, single-intent prompts | Verbose, multi-part, conversational prompts |
| **Speed** | ~1–5ms per query | ~50–200ms (includes LLM inference) |
| **Dependencies** | Local embeddings only (~90MB) | Local embeddings + local LLM (~1GB) |
| **Static API** | `ToolRouter.SearchAsync()` | `ToolRouter.SearchUsingLLMAsync()` |
| **Instance API** | `ToolIndex.CreateAsync()` + `SearchAsync()` | `ToolRouter.CreateAsync()` + `RouteAsync()` |
| **Example prompt** | "Send an email" | "I need to email Alice about the deadline and check the weather" |
| **Accuracy** | High for clear intents | Higher for ambiguous/complex intents |

---

### Using Filtered Tools with Azure OpenAI

Both modes work seamlessly with Azure OpenAI — route first, then send only the filtered tools to reduce token costs.

```csharp
using Azure;
using Azure.AI.OpenAI;
using OpenAI.Chat;

// Create Azure OpenAI ChatClient
var chatClient = new AzureOpenAIClient(
    new Uri("https://your-resource.openai.azure.com/"),
    new AzureKeyCredential("your-api-key"))
    .GetChatClient("gpt-5-mini");

// Route to relevant tools only (using Mode 1 as example)
var relevant = await ToolRouter.SearchAsync(userPrompt, allTools, topK: 3);

// Add only filtered tools to the chat call — saving tokens!
var chatOptions = new ChatCompletionOptions();
foreach (var r in relevant)
    chatOptions.Tools.Add(ChatTool.CreateFunctionTool(r.Tool.Name, r.Tool.Description ?? ""));

var response = await chatClient.CompleteChatAsync([new UserChatMessage(userPrompt)], chatOptions);
```

## How It Works — Technical Details

**Core mechanism:** MCPToolRouter embeds tool descriptions and user prompts into a vector space, then finds the top-K tools via cosine similarity — all using local embeddings (ONNX, no external APIs).

**Two-stage pipeline for Mode 2:** When using LLM distillation, the local LLM first condenses verbose prompts into a single intent sentence, then embeddings search finds matching tools — this hybrid approach maximizes accuracy for complex scenarios.

**Token savings:** By routing prompts to only the relevant tools before sending to external LLMs, you can reduce token usage by 70–85% while maintaining accuracy.

## Advanced Features

### ToolIndexOptions

Configure the index behavior with `ToolIndexOptions`:

```csharp
var options = new ToolIndexOptions
{
    QueryCacheSize = 20,                          // LRU cache for repeated queries (0 = disabled)
    EmbeddingTextTemplate = "{Name}: {Description}" // Customize how tools are embedded
};

await using var index = await ToolIndex.CreateAsync(tools, options);
```

### Save / Load Index

Persist a pre-built index to avoid re-embedding on startup:

```csharp
// Save
using var file = File.Create("tools.bin");
await index.SaveAsync(file);

// Load (instant warm-start — no re-embedding)
using var stream = File.OpenRead("tools.bin");
await using var loaded = await ToolIndex.LoadAsync(stream);
```

### Dynamic Index (Add / Remove Tools)

Modify the index at runtime without rebuilding:

```csharp
await index.AddToolsAsync(new[] { new Tool { Name = "new_tool", Description = "..." } });
index.RemoveTools(new[] { "obsolete_tool" });
```

### Dependency Injection

Register `IToolIndex` as a singleton in ASP.NET Core or any DI container:

```csharp
builder.Services.AddMcpToolRouter(tools, opts =>
{
    opts.QueryCacheSize = 20;
});

// Inject IToolIndex anywhere
app.MapGet("/search", async (IToolIndex index, string query)
    => await index.SearchAsync(query, topK: 3));
```

### Custom Embedding Generator

Bring your own `IEmbeddingGenerator<string, Embedding<float>>` from the Microsoft.Extensions.AI ecosystem:

```csharp
IEmbeddingGenerator<string, Embedding<float>> myGenerator = /* your provider */;
await using var index = await ToolIndex.CreateAsync(tools, myGenerator, options);
```

---

## ⚡ Performance Guide

MCPToolRouter is designed for both one-off queries and high-throughput scenarios. Understand the trade-offs to choose the right API for your use case.

### Static vs Instance API

The **static API** (`SearchAsync`, `SearchUsingLLMAsync`) is ideal for simple scripts and one-off calls — just pass your prompt and tools, get results immediately. Under the hood, static methods now reuse shared singleton ONNX sessions by default (`UseSharedResources = true`), so repeated static calls are **much faster** than before.

The **instance API** (`ToolIndex.CreateAsync` + `SearchAsync`, or `ToolRouter.CreateAsync` + `RouteAsync`) is optimized for servers, agents, and batch operations where you make many queries. By reusing the same index, you avoid re-embedding tools and re-initializing the LLM — this provides **15–35x speedup** on subsequent queries compared to creating a new index each time.

**Pro tip:** For cleanup, call `await ToolRouter.ResetSharedResourcesAsync()` to release singleton resources when your application shuts down.

### Query Cache

Set `QueryCacheSize > 0` in `ToolIndexOptions` to cache query embeddings. Identical prompts skip embedding generation entirely (~0ms vs ~10–20ms per query):

```csharp
var options = new ToolIndexOptions { QueryCacheSize = 20 };
await using var index = await ToolIndex.CreateAsync(tools, options);

var result1 = await index.SearchAsync("weather in Tokyo");     // ~10ms: embedding generated
var result2 = await index.SearchAsync("weather in Tokyo");     // ~0ms: cache hit
```

The cache is automatically cleared when tools are added or removed.

### Quick Recommendation

| Scenario | Recommended API | Why |
|----------|----------------|-----|
| CLI tool, one-off query | `ToolRouter.SearchAsync()` | Simplest, shared resources handle perf |
| Server/agent, many queries | `ToolRouter.CreateAsync()` + `RouteAsync()` | Full control, best performance |
| Verbose or complex prompts | `ToolRouter.SearchUsingLLMAsync()` | LLM distills intent first |

---

## 🔒 Security Considerations

MCPToolRouter runs entirely locally — models and searches never leave your machine. However, a few security practices are worth considering:

### Prompt Injection

Mode 2 uses a local LLM for prompt distillation. While the LLM cannot execute tools directly, adversarial prompts could theoretically influence which tools are selected. Validate tool selection downstream in your application before executing any tool calls. Example: if a tool requires authentication or permission checks, enforce those before execution.

### Model Downloads

Embedding and LLM models are downloaded automatically on first use. Use the `EmbeddingModelCacheDirectory` option to control where models are stored:

```csharp
var options = new ToolIndexOptions { EmbeddingModelCacheDirectory = "/secure/models" };
await using var index = await ToolIndex.CreateAsync(tools, options);
```

Models are downloaded over HTTPS and verified. Pin specific model versions if reproducibility is critical.

### Input Validation

`ToolIndex.LoadAsync()` validates all numeric bounds. The default `MaxPromptLength` is 4,096 characters, which limits the size of input sent to the LLM. Queries exceeding this limit are truncated automatically.

### Supply Chain

Build reproducibility is ensured via **NuGet lock files** (`.csproj` with `RestorePackagesWithLockFile=true`). Lock files pin exact package versions, preventing unexpected updates that could affect behavior or introduce vulnerabilities.

---

## Samples

Eight sample applications showcase different use cases for MCPToolRouter:

| Sample | Description | Azure Required |
|--------|-------------|:-:|
| [BasicUsage](src/samples/BasicUsage/) | Getting started — index tools and search | ❌ |
| [McpToolRouting](src/samples/McpToolRouting/) | Local LLM distillation for complex prompt routing | ❌ |
| [LLMDistillationDemo](src/samples/LLMDistillationDemo/) | Mode 1 vs Mode 2 comparison with verbose prompts | ❌ |
| [TokenComparison](src/samples/TokenComparison/) | Compare token usage: all tools vs. routed | ✅ |
| [TokenComparisonMax](src/samples/TokenComparisonMax/) | Extreme 120+ tools scenario with rich Spectre.Console UX | ✅ |
| [FilteredFunctionCalling](src/samples/FilteredFunctionCalling/) | End-to-end function calling with filtered tools | ✅ |
| [AgentWithToolRouter](src/samples/AgentWithToolRouter/) | Microsoft Agent Framework with semantic tool routing | ✅ |
| [FunctionalToolsValidation](src/samples/FunctionalToolsValidation/) | 52 real tools with execution validation — standard vs. routed | ✅ |

### BasicUsage

Getting started — index tools and search with semantic similarity. No Azure required.

### McpToolRouting

Demonstrates local LLM-powered tool routing with prompt distillation. 28 realistic MCP tools, complex multi-part prompt handling, and token savings analysis. No Azure required.

### LLMDistillationDemo

Head-to-head comparison of Mode 1 (embeddings-only) vs Mode 2 (LLM-distilled) across 7 scenarios with long, verbose, conversational prompts. Shows why Mode 2 exists — when users ramble, LLM distillation extracts core intent for better tool selection. No Azure required.

### TokenComparison

Marquee sample showing token savings: all 18 tools (standard mode, ~1,800 tokens) vs. top-3 routed tools (~500 tokens, ~72% savings).

### TokenComparisonMax

Extreme-scale demo with 120+ tool definitions across 12 categories, live token tracking, and Spectre.Console rich terminal UI.

### FilteredFunctionCalling

End-to-end example: route tools with MCPToolRouter, send only filtered tools to Azure OpenAI, and handle tool call responses.

### AgentWithToolRouter

Integrates MCPToolRouter with the Microsoft Agent Framework. 11 function tools, semantic routing, and multi-turn conversation demo.

### FunctionalToolsValidation

Comprehensive validation with 52 real tool implementations across 8 domains (math, strings, collections, dates, conversion, encoding, statistics, hashing). Validates routed vs. standard mode with 12 test scenarios.

---

## Azure OpenAI Setup

For samples requiring Azure OpenAI (TokenComparison, TokenComparisonMax, FilteredFunctionCalling, AgentWithToolRouter, FunctionalToolsValidation), configure credentials using user secrets:

```bash
cd src/samples/{SampleName}
dotnet user-secrets init
dotnet user-secrets set "AzureOpenAI:Endpoint" "https://your-resource.openai.azure.com/"
dotnet user-secrets set "AzureOpenAI:ApiKey" "your-api-key"
dotnet user-secrets set "AzureOpenAI:DeploymentName" "gpt-5-mini"
```

Replace:
- `your-resource` with your Azure OpenAI resource name
- `your-api-key` with your API key  
- Deployment name with your model (e.g., `gpt-5-mini`)

See each sample's folder for additional setup details.

---

## Building from Source

Clone the repository and build with the .NET CLI:

```bash
dotnet restore ElBruno.ModelContextProtocol.slnx
dotnet build ElBruno.ModelContextProtocol.slnx
dotnet test ElBruno.ModelContextProtocol.slnx
```

## Documentation

More detailed documentation and examples are available in the [docs/](docs/) folder.

## License

This project is licensed under the MIT License — see [LICENSE](LICENSE) for details.

## Author

**Bruno Capuano** (ElBruno)

- 💻 Blog: https://elbruno.com
- 📺 YouTube: https://youtube.com/elbruno
- 💼 LinkedIn: https://linkedin.com/in/elbruno
- 🐦 Twitter: https://twitter.com/elbruno
- 🎙️ Podcast: https://notienenombre.com
- 🎙️ Podcast: https://inthelabs.dev

## Acknowledgments

This library is built on top of:

- [ElBruno.LocalEmbeddings](https://github.com/elbruno/ElBruno.LocalEmbeddings) — Local embedding generation without external APIs
- [Model Context Protocol .NET SDK](https://github.com/modelcontextprotocol/sdk-dotnet) — Official MCP .NET support
