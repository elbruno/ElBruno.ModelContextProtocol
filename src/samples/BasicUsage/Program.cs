using ElBruno.ModelContextProtocol.MCPToolRouter;
using ModelContextProtocol.Protocol;

Console.WriteLine("═══════════════════════════════════════════════════════════");
Console.WriteLine("  🔧 MCPToolRouter - Basic Usage Sample");
Console.WriteLine("═══════════════════════════════════════════════════════════\n");

// Define a diverse set of MCP tools
var tools = new[]
{
    new Tool
    {
        Name = "get_weather",
        Description = "Retrieves current weather information for a specified location including temperature, humidity, and conditions"
    },
    new Tool
    {
        Name = "send_email",
        Description = "Sends an email message to specified recipients with subject and body content"
    },
    new Tool
    {
        Name = "search_files",
        Description = "Searches the file system for files matching a pattern or containing specific text content"
    },
    new Tool
    {
        Name = "calculate",
        Description = "Performs mathematical calculations and evaluates mathematical expressions"
    },
    new Tool
    {
        Name = "translate_text",
        Description = "Translates text from one language to another using machine translation"
    },
    new Tool
    {
        Name = "create_calendar_event",
        Description = "Creates a new event in the calendar with date, time, and description"
    },
    new Tool
    {
        Name = "query_database",
        Description = "Executes SQL queries against the database and returns results"
    },
    new Tool
    {
        Name = "get_stock_price",
        Description = "Retrieves current stock price and market data for a given ticker symbol"
    }
};

Console.WriteLine($"📦 Indexed {tools.Length} tools\n");
Console.WriteLine("⏳ Creating tool index (downloading embedding model on first run)...\n");

// Create the ToolIndex with options
var options = new ToolIndexOptions { QueryCacheSize = 10 };
await using var index = await ToolIndex.CreateAsync(tools, options);

Console.WriteLine("✅ Index created successfully!\n");

// Run several search queries
var queries = new[]
{
    "What's the temperature outside?",
    "I need to contact my team",
    "Find Python files in the project",
    "Convert 100 USD to EUR"
};

foreach (var query in queries)
{
    Console.WriteLine("─────────────────────────────────────────────────────────");
    Console.WriteLine($"🔍 Query: \"{query}\"\n");

    // Search with default topK=5
    var results = await index.SearchAsync(query, topK: 3);

    Console.WriteLine("📊 Top 3 Results:");
    foreach (var result in results)
    {
        Console.WriteLine($"  {GetScoreEmoji(result.Score)} {result.Tool.Name,-25} (score: {result.Score:F3})");
    }
    Console.WriteLine();
}

// Demonstrate minScore filtering
Console.WriteLine("─────────────────────────────────────────────────────────");
Console.WriteLine("🎯 Demonstrating minScore filter\n");
Console.WriteLine("Query: \"Schedule a meeting\"\n");

var meetingResults = await index.SearchAsync("Schedule a meeting", topK: 5, minScore: 0.3f);

Console.WriteLine($"Results with minScore=0.3 ({meetingResults.Count()} tools):");
foreach (var result in meetingResults)
{
    Console.WriteLine($"  {GetScoreEmoji(result.Score)} {result.Tool.Name,-25} (score: {result.Score:F3})");
}

Console.WriteLine("\n═══════════════════════════════════════════════════════════");
Console.WriteLine("  ✅ Sample completed successfully!");
Console.WriteLine("═══════════════════════════════════════════════════════════");

static string GetScoreEmoji(float score)
{
    return score switch
    {
        >= 0.7f => "🟢",
        >= 0.5f => "🟡",
        >= 0.3f => "🟠",
        _ => "🔴"
    };
}
