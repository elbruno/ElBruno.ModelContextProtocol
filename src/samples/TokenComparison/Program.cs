using Azure;
using Azure.AI.OpenAI;
using ElBruno.ModelContextProtocol.MCPToolRouter;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol.Protocol;
using System.ClientModel;
using System.Text.Json;
using OpenAI.Chat;

Console.WriteLine("╔════════════════════════════════════════════════════════╗");
Console.WriteLine("║    🔀 MCPToolRouter Token Usage Comparison Demo       ║");
Console.WriteLine("╚════════════════════════════════════════════════════════╝\n");

// Load configuration
var configuration = new ConfigurationBuilder()
    .AddUserSecrets<Program>()
    .AddEnvironmentVariables()
    .Build();

var endpoint = configuration["AzureOpenAI:Endpoint"];
var apiKey = configuration["AzureOpenAI:ApiKey"];
var deploymentName = configuration["AzureOpenAI:DeploymentName"];

if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(deploymentName))
{
    Console.WriteLine("❌ Azure OpenAI configuration not found. Please set up user secrets:\n");
    Console.WriteLine("   cd src/samples/TokenComparison");
    Console.WriteLine("   dotnet user-secrets set \"AzureOpenAI:Endpoint\" \"https://your-resource.openai.azure.com/\"");
    Console.WriteLine("   dotnet user-secrets set \"AzureOpenAI:ApiKey\" \"your-api-key\"");
    Console.WriteLine("   dotnet user-secrets set \"AzureOpenAI:DeploymentName\" \"gpt-5-mini\"\n");
    return;
}

Console.WriteLine($"✅ Configuration loaded");
Console.WriteLine($"   Endpoint: {endpoint}");
Console.WriteLine($"   Deployment: {deploymentName}\n");

// Create Azure OpenAI chat client
var azureClient = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
var chatClient = azureClient.GetChatClient(deploymentName);

// Define a comprehensive set of MCP tools
var mcpTools = new[]
{
    new Tool { Name = "get_weather", Description = "Retrieves current weather information for a specified location including temperature, humidity, wind speed, and conditions" },
    new Tool { Name = "get_forecast", Description = "Gets weather forecast for the next 7 days for a specified location with daily highs and lows" },
    new Tool { Name = "get_weather_alerts", Description = "Retrieves active weather alerts and warnings for a specified region" },
    new Tool { Name = "send_email", Description = "Sends an email message to specified recipients with subject, body content, and optional attachments" },
    new Tool { Name = "read_inbox", Description = "Reads unread emails from the inbox with filtering options by sender, subject, or date range" },
    new Tool { Name = "search_emails", Description = "Searches email archive for messages matching specified criteria or keywords" },
    new Tool { Name = "search_files", Description = "Searches the file system for files matching a pattern, name, or containing specific text content" },
    new Tool { Name = "read_file", Description = "Reads and returns the contents of a specified file from the file system" },
    new Tool { Name = "write_file", Description = "Writes or updates content to a file at the specified path" },
    new Tool { Name = "list_directory", Description = "Lists all files and subdirectories in a specified directory path" },
    new Tool { Name = "calculate", Description = "Performs mathematical calculations and evaluates complex mathematical expressions" },
    new Tool { Name = "convert_units", Description = "Converts measurements between different units (length, weight, temperature, currency, etc.)" },
    new Tool { Name = "translate_text", Description = "Translates text from one language to another using advanced machine translation" },
    new Tool { Name = "detect_language", Description = "Detects the language of a given text with confidence score" },
    new Tool { Name = "create_calendar_event", Description = "Creates a new event in the calendar with date, time, location, and description" },
    new Tool { Name = "list_calendar_events", Description = "Lists calendar events within a specified date range with filtering options" },
    new Tool { Name = "query_database", Description = "Executes SQL queries against the database and returns formatted results" },
    new Tool { Name = "insert_database_record", Description = "Inserts new records into database tables with validation" }
};

Console.WriteLine($"📦 Created {mcpTools.Length} tool definitions\n");

// Build the index once and reuse across all prompts (with query caching)
Console.WriteLine("⏳ Building tool index...");
var indexOptions = new ToolIndexOptions { QueryCacheSize = 10 };
await using var toolIndex = await ToolIndex.CreateAsync(mcpTools, indexOptions);
Console.WriteLine($"✅ Index ready — {toolIndex.Count} tools indexed\n");

// Test with multiple prompts
var testPrompts = new[]
{
    "What's the weather in Seattle?",
    "Send an email to my team about the meeting",
    "Find all Python files in the project"
};

foreach (var userPrompt in testPrompts)
{
    await RunComparisonAsync(userPrompt, mcpTools, toolIndex, chatClient);
    Console.WriteLine();
}

Console.WriteLine("╔════════════════════════════════════════════════════════╗");
Console.WriteLine("║              ✅ Comparison Complete!                   ║");
Console.WriteLine("╚════════════════════════════════════════════════════════╝");

static ChatTool ConvertToChatTool(Tool mcpTool)
{
    return ChatTool.CreateFunctionTool(
        mcpTool.Name,
        mcpTool.Description ?? string.Empty);
}

static async Task RunComparisonAsync(string userPrompt, Tool[] mcpTools, ToolIndex toolIndex, ChatClient chatClient)
{
    Console.WriteLine("════════════════════════════════════════════════════════");
    Console.WriteLine($"User Prompt: \"{userPrompt}\"");
    Console.WriteLine("════════════════════════════════════════════════════════\n");

    // STANDARD MODE: Send all tools
    Console.WriteLine($"🔵 STANDARD MODE: Sending ALL {mcpTools.Length} tools to the model...");
    var allChatTools = mcpTools.Select(ConvertToChatTool).ToList();
    
    var standardOptions = new ChatCompletionOptions();
    foreach (var tool in allChatTools)
    {
        standardOptions.Tools.Add(tool);
    }

    var standardResponse = await chatClient.CompleteChatAsync(
        [new UserChatMessage(userPrompt)],
        standardOptions);

    var standardInputTokens = standardResponse.Value.Usage.InputTokenCount;
    var standardOutputTokens = standardResponse.Value.Usage.OutputTokenCount;
    var standardTotalTokens = standardResponse.Value.Usage.TotalTokenCount;

    Console.WriteLine($"   Input tokens:  {standardInputTokens:N0}");
    Console.WriteLine($"   Output tokens: {standardOutputTokens:N0}");
    Console.WriteLine($"   Total tokens:  {standardTotalTokens:N0}\n");

    // ROUTED MODE: Use MCPToolRouter to filter
    Console.WriteLine("🟢 ROUTED MODE: Using MCPToolRouter to find relevant tools...");
    var relevantTools = await toolIndex.SearchAsync(userPrompt, topK: 3);

    Console.WriteLine("   Selected tools:");
    foreach (var result in relevantTools)
    {
        Console.WriteLine($"     ✅ {result.Tool.Name} (score: {result.Score:F3})");
    }
    Console.WriteLine();

    // Filter chat tools to only the relevant ones
    var relevantNames = relevantTools.Select(r => r.Tool.Name).ToHashSet();
    var filteredChatTools = mcpTools
        .Where(t => relevantNames.Contains(t.Name))
        .Select(ConvertToChatTool)
        .ToList();

    var routedOptions = new ChatCompletionOptions();
    foreach (var tool in filteredChatTools)
    {
        routedOptions.Tools.Add(tool);
    }

    var routedResponse = await chatClient.CompleteChatAsync(
        [new UserChatMessage(userPrompt)],
        routedOptions);

    var routedInputTokens = routedResponse.Value.Usage.InputTokenCount;
    var routedOutputTokens = routedResponse.Value.Usage.OutputTokenCount;
    var routedTotalTokens = routedResponse.Value.Usage.TotalTokenCount;

    Console.WriteLine($"   Input tokens:  {routedInputTokens:N0}");
    Console.WriteLine($"   Output tokens: {routedOutputTokens:N0}");
    Console.WriteLine($"   Total tokens:  {routedTotalTokens:N0}\n");

    // Calculate savings
    var inputSaved = standardInputTokens - routedInputTokens;
    var totalSaved = standardTotalTokens - routedTotalTokens;
    var inputSavedPct = standardInputTokens > 0 ? (float)inputSaved / standardInputTokens * 100 : 0;
    var totalSavedPct = standardTotalTokens > 0 ? (float)totalSaved / standardTotalTokens * 100 : 0;

    Console.WriteLine("╔════════════════════════════════════════════════════════╗");
    Console.WriteLine("║                    💰 SAVINGS                          ║");
    Console.WriteLine("╠════════════════════════════════════════════════════════╣");
    Console.WriteLine($"║  Input tokens saved:  {inputSaved,8:N0} ({inputSavedPct,5:F1}%)             ║");
    Console.WriteLine($"║  Total tokens saved:  {totalSaved,8:N0} ({totalSavedPct,5:F1}%)             ║");
    Console.WriteLine("╚════════════════════════════════════════════════════════╝");
}
