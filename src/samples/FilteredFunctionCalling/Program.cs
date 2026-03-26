using Azure;
using Azure.AI.OpenAI;
using ElBruno.ModelContextProtocol.MCPToolRouter;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol.Protocol;
using System.ClientModel;
using System.Text.Json;
using OpenAI.Chat;

Console.WriteLine("╔════════════════════════════════════════════════════════╗");
Console.WriteLine("║      🎯 Filtered Function Calling with Azure OpenAI   ║");
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
    Console.WriteLine("   cd src/samples/FilteredFunctionCalling");
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

// Define tools (MCP definitions + implementation functions)
var toolDefinitions = new Dictionary<string, (Tool McpTool, Func<Dictionary<string, object>, string> Implementation)>
{
    ["get_weather"] = (
        new Tool { Name = "get_weather", Description = "Gets current weather for a city. Parameters: city (string)" },
        (args) => GetWeather(args.GetValueOrDefault("city", "Unknown").ToString() ?? "Unknown")
    ),
    ["send_email"] = (
        new Tool { Name = "send_email", Description = "Sends an email message. Parameters: to (string), subject (string), body (string)" },
        (args) => SendEmail(
            args.GetValueOrDefault("to", "").ToString() ?? "",
            args.GetValueOrDefault("subject", "").ToString() ?? "",
            args.GetValueOrDefault("body", "").ToString() ?? "")
    ),
    ["calculate"] = (
        new Tool { Name = "calculate", Description = "Performs mathematical calculations. Parameters: expression (string)" },
        (args) => Calculate(args.GetValueOrDefault("expression", "").ToString() ?? "")
    ),
    ["search_files"] = (
        new Tool { Name = "search_files", Description = "Searches for files by pattern. Parameters: pattern (string)" },
        (args) => SearchFiles(args.GetValueOrDefault("pattern", "").ToString() ?? "")
    ),
    ["translate"] = (
        new Tool { Name = "translate", Description = "Translates text between languages. Parameters: text (string), targetLanguage (string)" },
        (args) => Translate(
            args.GetValueOrDefault("text", "").ToString() ?? "",
            args.GetValueOrDefault("targetLanguage", "").ToString() ?? "")
    )
};

Console.WriteLine($"📦 Registered {toolDefinitions.Count} tools with implementations\n");

// Build index once and reuse for all prompts
Console.WriteLine("⏳ Building tool index...");
var allMcpTools = toolDefinitions.Values.Select(t => t.McpTool).ToArray();
var indexOptions = new ToolIndexOptions { QueryCacheSize = 10 };
await using var toolIndex = await ToolIndex.CreateAsync(allMcpTools, indexOptions);
Console.WriteLine($"✅ Index ready — {toolIndex.Count} tools indexed\n");

// Test prompts
var prompts = new[]
{
    "What's the weather like in Seattle?",
    "Send an email to john@example.com with subject 'Meeting' and message 'Let's meet tomorrow'",
    "What is 125 multiplied by 48?"
};

foreach (var userPrompt in prompts)
{
    await ProcessPromptAsync(userPrompt, toolDefinitions, toolIndex, chatClient);
    Console.WriteLine();
}

Console.WriteLine("╔════════════════════════════════════════════════════════╗");
Console.WriteLine("║              ✅ Demo Complete!                         ║");
Console.WriteLine("╚════════════════════════════════════════════════════════╝");

static async Task ProcessPromptAsync(
    string userPrompt,
    Dictionary<string, (Tool McpTool, Func<Dictionary<string, object>, string> Implementation)> toolDefinitions,
    ToolIndex toolIndex,
    ChatClient chatClient)
{
    Console.WriteLine("════════════════════════════════════════════════════════");
    Console.WriteLine($"💬 User: {userPrompt}");
    Console.WriteLine("════════════════════════════════════════════════════════\n");

    // Step 1: Use MCPToolRouter to filter tools
    Console.WriteLine("🔍 Step 1: Filtering tools with MCPToolRouter...");
    var relevantTools = await toolIndex.SearchAsync(userPrompt, topK: 3);

    Console.WriteLine($"   Found {relevantTools.Count()} relevant tools:");
    foreach (var result in relevantTools)
    {
        Console.WriteLine($"     ✅ {result.Tool.Name} (score: {result.Score:F3})");
    }
    Console.WriteLine();

    // Step 2: Create chat tools from filtered MCP tools
    var relevantNames = relevantTools.Select(r => r.Tool.Name).ToHashSet();
    var filteredToolDefs = toolDefinitions
        .Where(kvp => relevantNames.Contains(kvp.Key))
        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

    var chatOptions = new ChatCompletionOptions();
    foreach (var (name, (mcpTool, _)) in filteredToolDefs)
    {
        chatOptions.Tools.Add(ChatTool.CreateFunctionTool(mcpTool.Name, mcpTool.Description ?? ""));
    }

    // Step 3: Call Azure OpenAI with filtered tools
    Console.WriteLine("🤖 Step 2: Calling Azure OpenAI with filtered tools...");
    var messages = new List<ChatMessage> { new UserChatMessage(userPrompt) };

    var response = await chatClient.CompleteChatAsync(messages, chatOptions);
    var responseMessage = response.Value.Content[0];

    // Step 4: Handle tool calls
    if (response.Value.FinishReason == ChatFinishReason.ToolCalls)
    {
        var toolCalls = response.Value.ToolCalls;
        Console.WriteLine($"   Model requested {toolCalls.Count} tool call(s)\n");

        messages.Add(new AssistantChatMessage(response.Value));

        foreach (var toolCall in toolCalls)
        {
            if (toolCall is ChatToolCall functionCall)
            {
                Console.WriteLine($"⚙️  Step 3: Executing tool: {functionCall.FunctionName}");

                // Parse arguments and execute
                var args = JsonSerializer.Deserialize<Dictionary<string, object>>(functionCall.FunctionArguments.ToString()) 
                    ?? new Dictionary<string, object>();
                
                if (filteredToolDefs.TryGetValue(functionCall.FunctionName, out var toolDef))
                {
                    var result = toolDef.Implementation(args);
                    Console.WriteLine($"   Result: {result}\n");

                    messages.Add(new ToolChatMessage(functionCall.Id, result));
                }
            }
        }

        // Step 5: Get final response
        Console.WriteLine("🤖 Step 4: Getting final response from model...");
        var finalResponse = await chatClient.CompleteChatAsync(messages);
        Console.WriteLine($"\n💬 Assistant: {finalResponse.Value.Content[0].Text}\n");
    }
    else
    {
        Console.WriteLine($"\n💬 Assistant: {responseMessage.Text}\n");
    }
}

// Tool implementations (simple stubs)
static string GetWeather(string city)
{
    return $"Weather in {city}: Sunny, 72°F (22°C), Humidity: 45%, Wind: 5 mph";
}

static string SendEmail(string to, string subject, string body)
{
    return $"✅ Email sent to {to} with subject '{subject}'";
}

static string SearchFiles(string pattern)
{
    return $"Found 3 files matching '{pattern}': file1.txt, file2.txt, file3.txt";
}

static string Calculate(string expression)
{
    // Simple calculation stub - in real app, use a proper expression evaluator
    return $"Result of '{expression}': 42 (stub implementation)";
}

static string Translate(string text, string targetLanguage)
{
    return $"'{text}' translated to {targetLanguage}: [translation placeholder]";
}
