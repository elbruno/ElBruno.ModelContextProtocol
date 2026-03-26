using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using ElBruno.ModelContextProtocol.MCPToolRouter;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol.Protocol;
using System.ComponentModel;

Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
Console.WriteLine("║  🤖 Agent with Tool Router — Microsoft Agent Framework   ║");
Console.WriteLine("╚═══════════════════════════════════════════════════════════╝\n");

// ─── 1. Configuration ────────────────────────────────────────────────────────

var configuration = new ConfigurationBuilder()
    .AddUserSecrets<Program>()
    .AddEnvironmentVariables()
    .Build();

var endpoint = configuration["AzureOpenAI:Endpoint"];
var apiKey = configuration["AzureOpenAI:ApiKey"];
var deploymentName = configuration["AzureOpenAI:DeploymentName"];

if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(deploymentName))
{
    Console.WriteLine("❌ Azure OpenAI configuration not found. Set up user secrets:\n");
    Console.WriteLine("   cd src/samples/AgentWithToolRouter");
    Console.WriteLine("   dotnet user-secrets set \"AzureOpenAI:Endpoint\" \"https://your-resource.openai.azure.com/\"");
    Console.WriteLine("   dotnet user-secrets set \"AzureOpenAI:ApiKey\" \"your-api-key\"       # optional — omit to use DefaultAzureCredential");
    Console.WriteLine("   dotnet user-secrets set \"AzureOpenAI:DeploymentName\" \"gpt-4o\"\n");
    return;
}

// API key takes priority; fall back to DefaultAzureCredential for passwordless auth
var azureClient = !string.IsNullOrEmpty(apiKey)
    ? new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey))
    : new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential());

IChatClient chatClient = azureClient.GetChatClient(deploymentName).AsIChatClient();

Console.WriteLine("✅ Azure OpenAI client created");
Console.WriteLine($"   Endpoint:   {endpoint}");
Console.WriteLine($"   Deployment: {deploymentName}");
Console.WriteLine($"   Auth:       {(string.IsNullOrEmpty(apiKey) ? "DefaultAzureCredential" : "API Key")}\n");

// ─── 2. Register ALL function tools (11 tools across 6 domains) ─────────────

var allFunctions = new Dictionary<string, AIFunction>
{
    // Weather
    ["get_weather"] = AIFunctionFactory.Create(GetWeather),
    ["get_forecast"] = AIFunctionFactory.Create(GetForecast),
    // Email
    ["send_email"] = AIFunctionFactory.Create(SendEmail),
    ["read_inbox"] = AIFunctionFactory.Create(ReadInbox),
    // Calendar
    ["create_event"] = AIFunctionFactory.Create(CreateEvent),
    ["list_events"] = AIFunctionFactory.Create(ListEvents),
    // Files
    ["search_files"] = AIFunctionFactory.Create(SearchFiles),
    ["read_file"] = AIFunctionFactory.Create(ReadFile),
    // Math
    ["calculate"] = AIFunctionFactory.Create(Calculate),
    ["convert_units"] = AIFunctionFactory.Create(ConvertUnits),
    // Translation
    ["translate_text"] = AIFunctionFactory.Create(TranslateText),
};

Console.WriteLine($"📦 Registered {allFunctions.Count} function tools across 6 domains:");
Console.WriteLine("   Weather  → get_weather, get_forecast");
Console.WriteLine("   Email    → send_email, read_inbox");
Console.WriteLine("   Calendar → create_event, list_events");
Console.WriteLine("   Files    → search_files, read_file");
Console.WriteLine("   Math     → calculate, convert_units");
Console.WriteLine("   Language → translate_text\n");

// ─── 3. Create MCP Tool definitions (mirrors function tools) ─────────────────

var mcpTools = new Tool[]
{
    new() { Name = "get_weather", Description = "Gets current weather conditions for a specific city or location." },
    new() { Name = "get_forecast", Description = "Gets a multi-day weather forecast for a location." },
    new() { Name = "send_email", Description = "Sends an email message to a recipient with subject and body." },
    new() { Name = "read_inbox", Description = "Reads recent email messages from the inbox." },
    new() { Name = "create_event", Description = "Creates a calendar event with title, date, time, and optional attendees." },
    new() { Name = "list_events", Description = "Lists upcoming calendar events and meetings." },
    new() { Name = "search_files", Description = "Searches for files matching a pattern in the file system." },
    new() { Name = "read_file", Description = "Reads the content of a specific file by path." },
    new() { Name = "calculate", Description = "Performs mathematical calculations and evaluates expressions." },
    new() { Name = "convert_units", Description = "Converts values between different units of measurement." },
    new() { Name = "translate_text", Description = "Translates text from one language to another." },
};

// ─── 4. Build the semantic tool index (one-time cost, reuse for every prompt) ─

Console.WriteLine("🔧 Building semantic tool index...");
await using var toolIndex = await ToolIndex.CreateAsync(mcpTools);
Console.WriteLine($"   Indexed {toolIndex.Count} tools ✅\n");

// ─── 5. Routed prompts — filter tools per prompt, then run agent ─────────────

var testPrompts = new[]
{
    "What's the weather like in Seattle today?",
    "Send an email to alice@example.com about tomorrow's meeting",
    "What is 42 multiplied by 17?",
    "Find all PDF files in my documents folder",
    "Translate 'Good morning, how are you?' to French",
};

foreach (var prompt in testPrompts)
{
    Console.WriteLine("═══════════════════════════════════════════════════════════");
    Console.WriteLine($"💬 User: {prompt}");
    Console.WriteLine("═══════════════════════════════════════════════════════════\n");

    // Step A — Semantic tool routing
    Console.WriteLine("🔍 Tool Router: finding relevant tools...");
    var results = await toolIndex.SearchAsync(prompt, topK: 3, minScore: 0.3f);

    var relevantNames = results.Select(r => r.Tool.Name).ToHashSet();

    Console.WriteLine($"   ALL tools:      {allFunctions.Count}");
    Console.WriteLine($"   SELECTED tools: {relevantNames.Count}");
    foreach (var result in results)
    {
        Console.WriteLine($"     ✅ {result.Tool.Name,-18} (score: {result.Score:F3})");
    }
    Console.WriteLine();

    // Step B — Map filtered MCP results → AIFunction instances
    var filteredTools = allFunctions
        .Where(kvp => relevantNames.Contains(kvp.Key))
        .Select(kvp => (AITool)kvp.Value)
        .ToList();

    // Step C — Create agent with ONLY the filtered tools
    Console.WriteLine($"🤖 Creating agent with {filteredTools.Count} tools (saved {allFunctions.Count - filteredTools.Count} from context)...");
    var agent = chatClient.AsAIAgent(
        instructions: "You are a helpful assistant. Use the available tools to answer the user's question.",
        tools: filteredTools);

    // Step D — Run the agent
    var response = await agent.RunAsync(prompt);
    Console.WriteLine($"\n💬 Assistant: {response.Text}\n");
}

// ─── 6. Multi-turn session — memory across turns ────────────────────────────

Console.WriteLine("═══════════════════════════════════════════════════════════");
Console.WriteLine("🔄 Multi-Turn Session Demo (agent remembers context)");
Console.WriteLine("═══════════════════════════════════════════════════════════\n");

// For the session demo, give a broader tool set to the agent
var sessionAgent = chatClient.AsAIAgent(
    instructions: "You are a helpful assistant. Use tools when needed. Remember context from previous messages in the conversation.",
    tools: allFunctions.Values.Cast<AITool>().ToList());

var session = await sessionAgent.CreateSessionAsync();

var sessionTurns = new[]
{
    "What's the weather in Paris right now?",
    "Now translate 'It is sunny today' to French",
    "Create a calendar event called 'Trip to Paris' for next Monday at 9am",
};

foreach (var turn in sessionTurns)
{
    Console.WriteLine($"💬 User: {turn}");
    var turnResponse = await sessionAgent.RunAsync(turn, session);
    Console.WriteLine($"🤖 Assistant: {turnResponse.Text}\n");
}

Console.WriteLine("═══════════════════════════════════════════════════════════");
Console.WriteLine("✅ Demo complete! Tool routing reduced token usage on every call.");
Console.WriteLine("═══════════════════════════════════════════════════════════");

// ─── Function Tool Definitions ───────────────────────────────────────────────

// Weather tools
[Description("Gets current weather conditions for a specific city or location.")]
static string GetWeather([Description("The city or location")] string location)
    => $"Weather in {location}: Sunny, 72°F (22°C), Humidity: 45%, Wind: 8 mph SW";

[Description("Gets a multi-day weather forecast for a location.")]
static string GetForecast(
    [Description("The city or location")] string location,
    [Description("Number of days (1-7)")] int days = 3)
    => $"Forecast for {location} ({days} days): Day 1: Sunny 75°F, Day 2: Partly cloudy 70°F, Day 3: Rain 65°F";

// Email tools
[Description("Sends an email message to a recipient.")]
static string SendEmail(
    [Description("Recipient email address")] string to,
    [Description("Email subject line")] string subject,
    [Description("Email body text")] string body)
    => $"✅ Email sent to {to} with subject '{subject}'";

[Description("Reads recent email messages from the inbox.")]
static string ReadInbox([Description("Maximum number of emails to return")] int count = 5)
    => $"📧 Inbox ({count} recent): 1) 'Meeting tomorrow' from boss@company.com, 2) 'Invoice #1234' from billing@vendor.com, 3) 'Weekly report' from team@company.com";

// Calendar tools
[Description("Creates a calendar event with title, date, and time.")]
static string CreateEvent(
    [Description("Event title")] string title,
    [Description("Event date (e.g., '2025-01-15')")] string date,
    [Description("Event time (e.g., '09:00')")] string time)
    => $"📅 Event created: '{title}' on {date} at {time}";

[Description("Lists upcoming calendar events and meetings.")]
static string ListEvents([Description("Number of days to look ahead")] int days = 7)
    => $"📅 Events (next {days} days): 1) Team standup — Mon 9:00 AM, 2) Project review — Wed 2:00 PM, 3) 1:1 with manager — Fri 11:00 AM";

// File tools
[Description("Searches for files matching a pattern in the file system.")]
static string SearchFiles([Description("Search pattern (e.g., '*.pdf')")] string pattern)
    => $"📁 Found 3 files matching '{pattern}': report.pdf, invoice.pdf, notes.pdf";

[Description("Reads the content of a specific file by path.")]
static string ReadFile([Description("Path to the file")] string path)
    => $"📄 Content of '{path}': [Sample file content — 1,234 bytes]";

// Math tools
[Description("Performs mathematical calculations and evaluates expressions.")]
static string Calculate([Description("Math expression to evaluate")] string expression)
    => $"🔢 {expression} = 714";

[Description("Converts values between different units of measurement.")]
static string ConvertUnits(
    [Description("The numeric value to convert")] double value,
    [Description("Source unit (e.g., 'km', 'miles')")] string fromUnit,
    [Description("Target unit (e.g., 'miles', 'km')")] string toUnit)
    => $"📐 {value} {fromUnit} = {value * 0.621371:F2} {toUnit}";

// Translation tools
[Description("Translates text from one language to another.")]
static string TranslateText(
    [Description("Text to translate")] string text,
    [Description("Target language (e.g., 'Spanish', 'French')")] string targetLanguage,
    [Description("Source language (auto-detect if not specified)")] string sourceLanguage = "auto")
    => $"🌐 Translated to {targetLanguage}: [{text} → translated text placeholder]";
