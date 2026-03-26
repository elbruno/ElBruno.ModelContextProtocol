# ElBruno.ModelContextProtocol

[![CI Build](https://github.com/elbruno/ElBruno.ModelContextProtocol/actions/workflows/build.yml/badge.svg)](https://github.com/elbruno/ElBruno.ModelContextProtocol/actions/workflows/build.yml)
[![Publish to NuGet](https://github.com/elbruno/ElBruno.ModelContextProtocol/actions/workflows/publish.yml/badge.svg)](https://github.com/elbruno/ElBruno.ModelContextProtocol/actions/workflows/publish.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![GitHub stars](https://img.shields.io/github/stars/elbruno/ElBruno.ModelContextProtocol?style=flat)](https://github.com/elbruno/ElBruno.ModelContextProtocol)

## Semantic routing for MCP tools 🔀

ElBruno.ModelContextProtocol is a .NET library that makes it easy to find the right tools from Model Context Protocol (MCP) tool definitions. It uses semantic search powered by local embeddings to route prompts to the most relevant tools, enabling intelligent tool selection without external API calls.

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

### Quick Start

```csharp
using ElBruno.ModelContextProtocol.MCPToolRouter;
using ModelContextProtocol.Protocol;

// 1. Define your MCP tools
var tools = new[]
{
    new Tool { Name = "get_weather", Description = "Get weather for a location" },
    new Tool { Name = "send_email", Description = "Send an email message" },
    new Tool { Name = "search_files", Description = "Search files by name or content" },
    new Tool { Name = "calculate", Description = "Perform mathematical calculations" },
    new Tool { Name = "translate_text", Description = "Translate text between languages" }
};

// 2. Create the index (with optional configuration)
var options = new ToolIndexOptions { QueryCacheSize = 10 };
await using var index = await ToolIndex.CreateAsync(tools, options);
var results = await index.SearchAsync("What's the temperature outside?", topK: 3);

foreach (var result in results)
    Console.WriteLine($"{result.Tool.Name}: {result.Score:F3}");
```

### Using Filtered Tools with Azure OpenAI

```csharp
using Azure;
using Azure.AI.OpenAI;
using OpenAI.Chat;

// Create Azure OpenAI client
var client = new AzureOpenAIClient(
    new Uri("https://your-resource.openai.azure.com/"),
    new AzureKeyCredential("your-api-key"));
var chatClient = client.GetChatClient("gpt-5-mini");

// Route to relevant tools only (instead of sending ALL tools)
var relevantTools = await index.SearchAsync(userPrompt, topK: 3);

// Convert filtered tools to ChatTools
var options = new ChatCompletionOptions();
foreach (var result in relevantTools)
{
    options.Tools.Add(ChatTool.CreateFunctionTool(
        result.Tool.Name,
        result.Tool.Description ?? string.Empty));
}

// Call the LLM with only the relevant tools — saving tokens!
var response = await chatClient.CompleteChatAsync(
    [new UserChatMessage(userPrompt)],
    options);
```

## How It Works

MCPToolRouter uses semantic search to intelligently route prompts to the most relevant tools. Here's the process:

1. **Ingestion:** The library ingests MCP tool definitions (name, description, and input schema)
2. **Embedding:** Tool descriptions are embedded into a local vector store using ONNX embeddings (no external API calls needed)
3. **Query embedding:** When you search, the user prompt is embedded using the same model
4. **Similarity search:** The library finds the top-K tools via cosine similarity
5. **Tool selection:** Only the relevant tools are returned — saving tokens when forwarding to LLMs

This approach enables intelligent tool selection at LLM prompt time without external API calls or round-trips.

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

## Samples

Six sample applications showcase different use cases for MCPToolRouter:

| Sample | Description | Azure Required |
|--------|-------------|:-:|
| [BasicUsage](src/samples/BasicUsage/) | Getting started — index tools and search | ❌ |
| [TokenComparison](src/samples/TokenComparison/) | Compare token usage: all tools vs. routed | ✅ |
| [TokenComparisonMax](src/samples/TokenComparisonMax/) | Extreme 120+ tools scenario with rich Spectre.Console UX | ✅ |
| [FilteredFunctionCalling](src/samples/FilteredFunctionCalling/) | End-to-end function calling with filtered tools | ✅ |
| [AgentWithToolRouter](src/samples/AgentWithToolRouter/) | Microsoft Agent Framework with semantic tool routing | ✅ |
| [FunctionalToolsValidation](src/samples/FunctionalToolsValidation/) | 52 real tools with execution validation — standard vs. routed | ✅ |

### BasicUsage

The simplest way to get started. This sample creates a `ToolIndex` from MCP tool definitions, runs semantic search queries, and displays ranked results.

**No Azure dependency required** — perfect for exploring the library.

### TokenComparison

This is the marquee sample demonstrating the dramatic token savings when using routed tools instead of sending all tools to the LLM.

The sample compares two scenarios:
- **Standard Mode:** All 18 tools sent to the LLM (full context, high token cost)
- **Routed Mode:** Only top-3 relevant tools sent via MCPToolRouter (filtered context, minimal tokens)

**Example output:**
```
📊 Standard Mode (18 tools):  ~1,800 input tokens
📊 Routed Mode (3 tools):     ~500 input tokens
💰 Savings:                    ~72% fewer tokens!
```

**Azure OpenAI Setup:**

This sample requires Azure OpenAI. Use user secrets to configure credentials:

```bash
cd src/samples/TokenComparison
dotnet user-secrets init
dotnet user-secrets set "AzureOpenAI:Endpoint" "https://your-resource.openai.azure.com/"
dotnet user-secrets set "AzureOpenAI:ApiKey" "your-api-key"
dotnet user-secrets set "AzureOpenAI:DeploymentName" "gpt-5-mini"
```

Replace:
- `your-resource` with your Azure OpenAI resource name
- `your-api-key` with your API key
- Deployment name with your model (e.g., `gpt-5-mini`)

### TokenComparisonMax

An extreme-scale demo with **120+ tool definitions** across 12 categories, showcasing the dramatic token savings at scale. Uses [Spectre.Console](https://spectreconsole.net/) for a rich terminal experience with live-updating tables and a comprehensive summary.

**Features:**
- 120+ realistic MCP tool definitions
- Real-time token usage tracking with live tables
- Final summary table with per-scenario and aggregate savings
- Beautiful terminal UI via Spectre.Console

**Azure OpenAI Setup:**

```bash
cd src/samples/TokenComparisonMax
dotnet user-secrets init
dotnet user-secrets set "AzureOpenAI:Endpoint" "https://your-resource.openai.azure.com/"
dotnet user-secrets set "AzureOpenAI:ApiKey" "your-api-key"
dotnet user-secrets set "AzureOpenAI:DeploymentName" "gpt-5-mini"
```

Replace:
- `your-resource` with your Azure OpenAI resource name
- `your-api-key` with your API key
- Deployment name with your model (e.g., `gpt-5-mini`)

> **💰 Cost Estimation:** The TokenComparisonMax sample calculates estimated money saved based on Azure OpenAI pricing for GPT-5-mini (as of March 2026):
>
> | Token Type | Cost per 1M Tokens |
> |---|---|
> | Input | $0.25 |
> | Output | $2.00 |
> | Cached Input | $0.025 |
>
> Pricing source: [Azure OpenAI Service Pricing](https://azure.microsoft.com/en-us/pricing/details/azure-openai/)
> 
> *Prices may vary by region and deployment type. Check the pricing page for current rates.*

### FilteredFunctionCalling

An end-to-end example of the real-world pattern: route tools with MCPToolRouter, send only the filtered tools to Azure OpenAI, and handle tool call responses.

**Azure OpenAI Setup:**

```bash
cd src/samples/FilteredFunctionCalling
dotnet user-secrets init
dotnet user-secrets set "AzureOpenAI:Endpoint" "https://your-resource.openai.azure.com/"
dotnet user-secrets set "AzureOpenAI:ApiKey" "your-api-key"
dotnet user-secrets set "AzureOpenAI:DeploymentName" "gpt-5-mini"
```

### AgentWithToolRouter

Demonstrates MCPToolRouter integrated with the [Microsoft Agent Framework](https://github.com/microsoft/agent-framework) (`Microsoft.Agents.AI.OpenAI`). The sample defines 11 function tools, uses semantic routing to filter relevant tools per prompt, and creates an `AIAgent` with only the filtered tools — showing how tool routing works with the modern agent paradigm.

**Features:**
- 11 function tools across 6 domains (weather, email, calendar, files, math, translation)
- Semantic tool routing via MCPToolRouter before agent creation
- Multi-turn session demo with conversation memory
- Supports both API key and `DefaultAzureCredential` authentication

**Azure OpenAI Setup:**

```bash
cd src/samples/AgentWithToolRouter
dotnet user-secrets init
dotnet user-secrets set "AzureOpenAI:Endpoint" "https://your-resource.openai.azure.com/"
dotnet user-secrets set "AzureOpenAI:ApiKey" "your-api-key"
dotnet user-secrets set "AzureOpenAI:DeploymentName" "gpt-5-mini"
```

### FunctionalToolsValidation

A comprehensive validation sample with **52 real tool implementations** across 8 domains (math, strings, collections, dates, conversion, encoding, statistics, hashing). Each tool performs actual computation — no stubs. The sample runs 12 test scenarios comparing standard mode (all 52 tools) vs. routed mode (top-K filtered), validating that both produce correct results while measuring token savings.

**Features:**
- 52 fully functional tools with real C# implementations
- 12 test scenarios with expected-result validation
- Side-by-side comparison: standard vs. routed tool selection
- Full tool-call execution loop with Azure OpenAI

**Azure OpenAI Setup:**

```bash
cd src/samples/FunctionalToolsValidation
dotnet user-secrets init
dotnet user-secrets set "AzureOpenAI:Endpoint" "https://your-resource.openai.azure.com/"
dotnet user-secrets set "AzureOpenAI:ApiKey" "your-api-key"
dotnet user-secrets set "AzureOpenAI:DeploymentName" "gpt-5-mini"
```

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
- 📺 YouTube: https://youtube.com/@inthelabs
- 💼 LinkedIn: https://linkedin.com/in/inthelabs
- 🐦 Twitter: https://twitter.com/inthelabs
- 🎙️ Podcast: https://inthelabs.dev

## Acknowledgments

This library is built on top of:

- [ElBruno.LocalEmbeddings](https://github.com/elbruno/ElBruno.LocalEmbeddings) — Local embedding generation without external APIs
- [Model Context Protocol .NET SDK](https://github.com/modelcontextprotocol/sdk-dotnet) — Official MCP .NET support
