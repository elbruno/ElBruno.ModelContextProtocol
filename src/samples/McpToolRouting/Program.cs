using ElBruno.ModelContextProtocol.MCPToolRouter;
using ModelContextProtocol.Protocol;

// Define a comprehensive set of 28 realistic MCP tools across various domains
var allTools = new Tool[]
{
    // Weather & Location (4 tools)
    new Tool { Name = "get_weather", Description = "Get current weather for a specific location including temperature, humidity, and forecast" },
    new Tool { Name = "get_weather_forecast", Description = "Get a 7-day weather forecast for a location with hourly details" },
    new Tool { Name = "get_time_zone", Description = "Get the time zone and current time for a specified location" },
    new Tool { Name = "find_location", Description = "Find geographic coordinates and details for a city or address" },

    // Email & Communication (4 tools)
    new Tool { Name = "send_email", Description = "Send an email message with attachments to one or more recipients" },
    new Tool { Name = "check_email", Description = "Retrieve and read recent emails from your inbox with sender and subject info" },
    new Tool { Name = "create_meeting_invite", Description = "Create and send a calendar meeting invitation to attendees" },
    new Tool { Name = "send_slack_message", Description = "Send a message to a Slack channel or direct message to a user" },

    // Calendar & Task Management (4 tools)
    new Tool { Name = "create_event", Description = "Create a calendar event with date, time, attendees, and reminder settings" },
    new Tool { Name = "list_calendar_events", Description = "List all upcoming calendar events for the next N days" },
    new Tool { Name = "create_todo", Description = "Create a to-do item with due date, priority, and description" },
    new Tool { Name = "complete_todo", Description = "Mark a to-do item as completed and update your task list" },

    // File & Document Management (4 tools)
    new Tool { Name = "search_files", Description = "Search for files by name, extension, or content using full-text search" },
    new Tool { Name = "read_file", Description = "Read the contents of a text, JSON, CSV, or markdown file" },
    new Tool { Name = "write_file", Description = "Create or update a file with specified text content" },
    new Tool { Name = "delete_file", Description = "Safely delete a file with confirmation and backup options" },

    // Web & Information (4 tools)
    new Tool { Name = "web_search", Description = "Search the web for information, news, and resources on any topic" },
    new Tool { Name = "fetch_webpage", Description = "Fetch and parse the contents of a webpage for text extraction" },
    new Tool { Name = "get_stock_price", Description = "Get current stock price, historical data, and market trends for a ticker symbol" },
    new Tool { Name = "translate_text", Description = "Translate text between 50+ languages with context awareness" },

    // Math & Data Analysis (3 tools)
    new Tool { Name = "calculate", Description = "Perform mathematical calculations including advanced functions and statistics" },
    new Tool { Name = "analyze_data", Description = "Analyze datasets with descriptive statistics, correlations, and visualizations" },
    new Tool { Name = "generate_report", Description = "Generate formatted reports from data with charts and summaries" },

    // Code & Development (3 tools)
    new Tool { Name = "run_code", Description = "Execute Python, JavaScript, or C# code snippets and return results" },
    new Tool { Name = "check_syntax", Description = "Validate code syntax for multiple programming languages" },
    new Tool { Name = "explain_code", Description = "Provide detailed explanations of code logic and functionality" }
};

Console.WriteLine("🚀 MCP Tool Router with Local LLM Distillation");
Console.WriteLine("================================================\n");

// Scenario 1: Complex verbose prompt → distilled intent → filtered tools
Console.WriteLine("📌 SCENARIO 1: Complex Prompt → LLM Distillation");
Console.WriteLine("-------------------------------------------------");

var complexPrompt = @"I need to handle multiple tasks today. First, I'm planning a business trip to Tokyo and I need to:
1. Check the weather forecast there for the next week
2. Find out what time zone they're in and the current time
3. Once I know the schedule, I need to create calendar events for my meetings
4. Send emails to my team about the trip schedule
5. Also, I have some budget spreadsheets that need analysis for the quarterly report

Additionally, I should search for the latest technology news and translate some documents that are in Japanese.";

Console.WriteLine($"📝 Original prompt length: {complexPrompt.Length} characters");

// One-liner: Route using local LLM distillation (zero-setup)
var routedResults = await ToolRouter.SearchUsingLLMAsync(complexPrompt, allTools, topK: 7);

Console.WriteLine($"✨ Distilled top-7 most relevant tools:\n");
foreach (var r in routedResults)
{
    Console.WriteLine($"  • {r.Tool.Name,-25} {r.Score:F3}");
}

Console.WriteLine();

// Scenario 2: Simple prompt → direct routing (embeddings-only, no LLM)
Console.WriteLine("📌 SCENARIO 2: Simple Prompt → Embeddings Search");
Console.WriteLine("-------------------------------------------");

var simplePrompt = "What's the weather like in Tokyo?";
Console.WriteLine($"📝 Simple prompt: \"{simplePrompt}\"\n");

// One-liner: Route using embeddings only (no LLM needed)
var simpleResults = await ToolRouter.SearchAsync(simplePrompt, allTools, topK: 3);

Console.WriteLine("🎯 Top-3 most relevant tools:\n");
foreach (var r in simpleResults)
{
    Console.WriteLine($"  • {r.Tool.Name,-25} {r.Score:F3}");
}

Console.WriteLine();

// Scenario 3: Token savings analysis
Console.WriteLine("📌 SCENARIO 3: Token Savings Comparison");
Console.WriteLine("--------------------------------------");

// Estimate tokens for all tools
// Rough estimate: ~10 tokens per tool (name + brief description)
int tokensPerTool = 10;
int allToolsTokens = allTools.Length * tokensPerTool;

// Estimate tokens for top-K routed tools  
int routed3TokensEstimate = 3 * tokensPerTool;
int routed5TokensEstimate = 5 * tokensPerTool;
int routed7TokensEstimate = 7 * tokensPerTool;

Console.WriteLine($"📊 Token Usage Estimate (rough count):");
Console.WriteLine($"  All {allTools.Length} tools:          ~{allToolsTokens} tokens");
Console.WriteLine($"  Top-3 routed:      ~{routed3TokensEstimate} tokens ({(1 - routed3TokensEstimate / (double)allToolsTokens) * 100:F1}% savings)");
Console.WriteLine($"  Top-5 routed:      ~{routed5TokensEstimate} tokens ({(1 - routed5TokensEstimate / (double)allToolsTokens) * 100:F1}% savings)");
Console.WriteLine($"  Top-7 routed:      ~{routed7TokensEstimate} tokens ({(1 - routed7TokensEstimate / (double)allToolsTokens) * 100:F1}% savings)");

Console.WriteLine($"\n💰 Benefits of tool routing:");
Console.WriteLine($"  ✓ Reduces context window usage");
Console.WriteLine($"  ✓ Faster LLM response times");
Console.WriteLine($"  ✓ Lower API costs");
Console.WriteLine($"  ✓ Improved tool selection quality");

Console.WriteLine();

// Show advanced usage patterns
Console.WriteLine("📌 ADVANCED USAGE PATTERNS");
Console.WriteLine("--------------------------");

Console.WriteLine("Pattern 1 - Mode 1 (Embeddings-only, one-liner):");
Console.WriteLine(@"
var results = await ToolRouter.SearchAsync(prompt, tools, topK: 3);
");

Console.WriteLine("Pattern 2 - Mode 2 (LLM-distilled, one-liner):");
Console.WriteLine(@"
var results = await ToolRouter.SearchUsingLLMAsync(complexPrompt, tools, chatClient, topK: 5);
");

Console.WriteLine("Pattern 3 - Reusable instance (Mode 1 for high-throughput scenarios):");
Console.WriteLine(@"
await using var index = await ToolIndex.CreateAsync(tools);
var results = await index.SearchAsync(prompt, topK: 3);
");

Console.WriteLine("Pattern 4 - Reusable instance (Mode 2 for high-throughput scenarios):");
Console.WriteLine(@"
await using var router = await ToolRouter.CreateAsync(tools, chatClient);
var results = await router.RouteAsync(complexPrompt, topK: 5);
");

Console.WriteLine("\n✅ Demo completed successfully!");
