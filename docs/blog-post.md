# Stop Wasting Tokens: Smart Tool Routing for LLMs with MCPToolRouter

Hey folks! Today I want to share something that's been bugging me for a while. You know when you're building an AI agent or working with LLMs, and you have dozens (or hundreds) of tools available? What do you do? Send ALL of them to the LLM every single time, right?

Yeah, me too. And it's expensive.

## The Token Problem Nobody Talks About

Here's the thing: every tool you send to an LLM costs tokens. Not just a few—we're talking about tool names, descriptions, and full JSON schemas. With 50+ tools, you can easily burn through 2,000+ tokens **before the LLM even thinks about your question**.

And that's just wasteful. If I'm asking "What's the weather in Seattle?", why am I sending 47 other tools about databases, emails, and file systems?

## Enter MCPToolRouter

So I built something to fix this: **ElBruno.ModelContextProtocol.MCPToolRouter**. It's a .NET library that uses semantic search to route your prompts to the most relevant tools. Think of it as a smart filter that sits between your user's question and your LLM.

Here's how it works:

1. **Index your tools once** (using local embeddings—no API calls)
2. **Search semantically** when a user asks something
3. **Get back only the relevant tools** (top 3, top 5, whatever you need)
4. **Send those to your LLM** instead of everything

The result? **70-80% token savings** in real-world scenarios. Not kidding.

## Show Me the Code

Let's get practical. Here's the simplest possible example:

```csharp
using ElBruno.ModelContextProtocol.MCPToolRouter;
using ModelContextProtocol.Protocol;

// Define your MCP tools (or pull them from an MCP server)
var tools = new[]
{
    new Tool { Name = "get_weather", Description = "Get weather for a location" },
    new Tool { Name = "send_email", Description = "Send an email message" },
    new Tool { Name = "search_files", Description = "Search files by name or content" },
    new Tool { Name = "calculate", Description = "Perform mathematical calculations" }
};

// Create the index (one-time cost)
await using var index = await ToolIndex.CreateAsync(tools);

// Find the most relevant tools for a prompt
var results = await index.SearchAsync("What's the temperature outside?", topK: 3);

foreach (var r in results)
    Console.WriteLine($"{r.Tool.Name}: {r.Score:F3}");
```

**Output:**
```
get_weather: 0.892
search_files: 0.234
calculate: 0.187
```

See that? It knows `get_weather` is the right tool. Now you send just that one (or top 3) to your LLM instead of all 50.

## Real-World Integration with Azure OpenAI

Here's where it gets practical. Let's say you're using Azure OpenAI and want to save tokens:

```csharp
// Create Azure OpenAI client
var chatClient = new AzureOpenAIClient(
    new Uri("https://your-resource.openai.azure.com/"),
    new AzureKeyCredential("your-api-key"))
    .GetChatClient("gpt-5-mini");

// Route to relevant tools only
var userPrompt = "What's the weather in Seattle?";
var relevant = await index.SearchAsync(userPrompt, topK: 3);

// Add only filtered tools to the chat call — saving tokens!
var chatOptions = new ChatCompletionOptions();
foreach (var r in relevant)
    chatOptions.Tools.Add(ChatTool.CreateFunctionTool(r.Tool.Name, r.Tool.Description ?? ""));

var response = await chatClient.CompleteChatAsync(
    [new UserChatMessage(userPrompt)],
    chatOptions);
```

Instead of sending 50 tools (2,000 tokens), you send 3 tools (~300 tokens). That's an 85% reduction. Multiply that by thousands of API calls, and you're saving real money.

## The Best Part: It's All Local

Here's what I love about this: **no external API calls for embeddings**. MCPToolRouter uses local ONNX models (via my [ElBruno.LocalEmbeddings](https://github.com/elbruno/ElBruno.LocalEmbeddings) library), so everything runs on your machine or server. Fast, private, and cost-effective.

First run downloads a small embedding model (~25 MB), and after that, it's instant.

## Advanced Features

Once you get the basics, there's more:

### Save/Load Indexes

Pre-build your index and load it instantly on startup:

```csharp
// Save
using var file = File.Create("tools.bin");
await index.SaveAsync(file);

// Load (instant warm-start)
using var stream = File.OpenRead("tools.bin");
await using var loaded = await ToolIndex.LoadAsync(stream);
```

### Dynamic Updates

Add or remove tools at runtime without rebuilding everything:

```csharp
await index.AddToolsAsync(new[] { new Tool { Name = "new_tool", Description = "..." } });
index.RemoveTools(new[] { "obsolete_tool" });
```

### Dependency Injection

Works great with ASP.NET Core:

```csharp
builder.Services.AddMcpToolRouter(tools, opts =>
{
    opts.QueryCacheSize = 20; // LRU cache for repeated queries
});

// Inject IToolIndex anywhere
app.MapGet("/search", async (IToolIndex index, string query)
    => await index.SearchAsync(query, topK: 3));
```

## Try It Yourself

The library is available on NuGet and fully open source:

```bash
dotnet add package ElBruno.ModelContextProtocol.MCPToolRouter
```

I've also included **6 sample applications** that show different use cases:

- **BasicUsage**: Getting started with tool indexing and search
- **TokenComparison**: Side-by-side comparison showing token savings
- **TokenComparisonMax**: Extreme scenario with 120+ tools
- **FilteredFunctionCalling**: End-to-end function calling with filtered tools
- **AgentWithToolRouter**: Integration with Microsoft Agent Framework
- **FunctionalToolsValidation**: 52 real tools with execution validation

Check out the full repo on GitHub:
👉 **https://github.com/elbruno/ElBruno.ModelContextProtocol**

## Why This Matters

Look, I'm not saying you should **always** use semantic routing. If you only have 5-10 tools, sending them all is fine. But once you cross into dozens or hundreds of tools, the cost and context window bloat become real problems.

MCPToolRouter solves this with a simple, pragmatic approach: **send only what matters**.

Plus, it's built on the Model Context Protocol (MCP), which is becoming the standard way to define and share tool definitions across AI systems. So you're not just saving tokens—you're building on a solid foundation.

## What's Next?

I'm actively working on this library, and I'd love your feedback. Try it out, break it, and let me know what you think. Open issues, send PRs, or just drop a comment.

And if you find it useful, star the repo and share it with your team. Let's make LLM tool routing smarter together.

**Download it now:**
```bash
dotnet add package ElBruno.ModelContextProtocol.MCPToolRouter
```

Happy coding!

**Bruno Capuano (ElBruno)**

- 💻 Blog: [https://elbruno.com](https://elbruno.com)
- 📺 YouTube: [https://youtube.com/@inthelabs](https://youtube.com/@inthelabs)
- 💼 LinkedIn: [https://linkedin.com/in/inthelabs](https://linkedin.com/in/inthelabs)
- 🐦 Twitter: [https://twitter.com/inthelabs](https://twitter.com/inthelabs)
- 🎙️ Podcast: [https://inthelabs.dev](https://inthelabs.dev)

---

*P.S. — The TokenComparisonMax sample has a beautiful Spectre.Console UI that shows live token savings. It's oddly satisfying to watch. 😄*
