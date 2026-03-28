# McpToolRouting Sample

Demonstrates intelligent tool routing using **local LLM distillation** and **semantic search** to filter MCP tools based on complex user prompts.

## What It Demonstrates

This sample showcases the complete MCPToolRouter pipeline:

1. **Complex Prompt → Distilled Intent** — Parses verbose, multi-step user prompts and distills them into core intent using a local LLM (Qwen 2.5 0.5B)
2. **Semantic Tool Routing** — Routes the distilled intent to the most relevant tools using vector similarity search
3. **Token Savings** — Shows how filtering tools from a large set (28 tools) down to top-K reduces context window usage

## Three Key Scenarios

1. **Complex Multi-Step Prompt:** A verbose 300+ character business trip planning request that requires weather, calendar, email, time zone, data analysis, and translation tools. The router intelligently filters down to the 7 most relevant tools.

2. **Simple One-Shot Query:** A straightforward "What's the weather in Tokyo?" prompt that routes directly to weather and location tools without distillation overhead.

3. **Token Savings Visualization:** Comparative analysis showing ~70-85% token reduction when sending only top-K routed tools vs. all 28 tools to an LLM.

## Prerequisites

- **.NET 8.0 or higher**
- **~1 GB disk space** — First run downloads the Qwen 2.5 0.5B ONNX model (~500 MB)
- Local LLM inference via Microsoft ONNX Runtime (no cloud dependencies)

## Running the Sample

```bash
cd src/samples/McpToolRouting
dotnet run
```

**Expected output:**

```
🚀 MCP Tool Router with Local LLM Distillation
================================================

📌 SCENARIO 1: Complex Prompt → Distilled Intent
-------------------------------------------------
📝 Original prompt length: 412 characters
✨ Distilled top-7 most relevant tools:

  • get_weather_forecast      0.892
  • create_event              0.856
  • send_email                0.834
  • get_time_zone             0.821
  • analyze_data              0.798
  • translate_text            0.784
  • search_files              0.715

...
```

## Key APIs Used

### Creating a Router with Distillation
```csharp
using var chatClient = await LocalChatClient.CreateAsync(llmOptions);
await using var router = await ToolRouter.CreateAsync(allTools, chatClient);
var results = await router.RouteAsync(complexPrompt, topK: 7);
```

### Creating a Router Without Distillation (Semantic Search Only)
```csharp
await using var router = await ToolRouter.CreateAsync(allTools);
var results = await router.RouteAsync("What's the weather?", topK: 3);
```

### Advanced Pattern — One-Shot Static Method
```csharp
var results = await ToolRouter.RouteAsync(tools, prompt, chatClient, topK: 5);
```

## Tool Set

The sample includes 28 realistic MCP tool definitions across 7 domains:
- **Weather & Location:** get_weather, get_weather_forecast, get_time_zone, find_location
- **Email & Communication:** send_email, check_email, create_meeting_invite, send_slack_message
- **Calendar & Tasks:** create_event, list_calendar_events, create_todo, complete_todo
- **Files & Documents:** search_files, read_file, write_file, delete_file
- **Web & Information:** web_search, fetch_webpage, get_stock_price, translate_text
- **Math & Data:** calculate, analyze_data, generate_report
- **Code & Development:** run_code, check_syntax, explain_code

## Notes

- **First run is slower** — The sample downloads and initializes the ONNX model (~10-15 seconds).
- **Subsequent runs are fast** — The model is cached locally.
- **No API keys required** — Uses local inference; no external LLM calls.
- **Multi-step prompts shine** — The distillation feature is most valuable for complex, verbose user requests.

## Learn More

- [README.md](../../README.md) — Main library documentation
- [TokenComparison](../TokenComparison/) — Azure OpenAI integration example
- [AgentWithToolRouter](../AgentWithToolRouter/) — Microsoft Agent Framework integration
