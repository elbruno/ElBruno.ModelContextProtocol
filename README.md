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

var tools = new[]
{
    new Tool { Name = "get_weather", Description = "Get weather for a location" },
    new Tool { Name = "send_email", Description = "Send an email" },
    new Tool { Name = "search_files", Description = "Search files by name" }
};

await using var index = await ToolIndex.CreateAsync(tools);
var results = await index.SearchAsync("What's the temperature outside?", topK: 2);

foreach (var result in results)
    Console.WriteLine($"{result.Tool.Name}: {result.Score:F3}");
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
