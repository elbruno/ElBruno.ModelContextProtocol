// ============================================================================
// LLM Distillation Demo
// Demonstrates why Mode 2 (LLM-assisted routing) produces better tool
// selection than Mode 1 (embeddings-only) when user prompts are long,
// verbose, and conversational — the way humans actually write.
//
// For each scenario, we:
//   1. Show the original verbose prompt
//   2. Distill it via a local LLM into a focused intent
//   3. Run embeddings search on BOTH the raw prompt (Mode 1) and the
//      distilled intent (Mode 2) using the same ToolIndex
//   4. Compare which mode selects more relevant tools
// ============================================================================

using System.Diagnostics;
using ElBruno.ModelContextProtocol.MCPToolRouter;
using ModelContextProtocol.Protocol;

Console.WriteLine("╔══════════════════════════════════════════════════════════════════╗");
Console.WriteLine("║        LLM Distillation Demo — Why Mode 2 Exists               ║");
Console.WriteLine("║   Mode 1 (Embeddings Only) vs Mode 2 (LLM-Assisted Routing)    ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════════╝");
Console.WriteLine();

// ---------------------------------------------------------------------------
// 30 realistic MCP tools across 8 domains
// ---------------------------------------------------------------------------

var allTools = new Tool[]
{
    // Weather & Location (4 tools)
    new() { Name = "get_weather", Description = "Get current weather for a specific location including temperature, humidity, and forecast" },
    new() { Name = "get_weather_forecast", Description = "Get a 7-day weather forecast for a location with hourly details" },
    new() { Name = "get_time_zone", Description = "Get the time zone and current time for a specified location" },
    new() { Name = "find_location", Description = "Find geographic coordinates and details for a city or address" },

    // Email & Communication (4 tools)
    new() { Name = "send_email", Description = "Send an email message with attachments to one or more recipients" },
    new() { Name = "check_email", Description = "Retrieve and read recent emails from your inbox with sender and subject info" },
    new() { Name = "create_meeting_invite", Description = "Create and send a calendar meeting invitation to attendees" },
    new() { Name = "send_slack_message", Description = "Send a message to a Slack channel or direct message to a user" },

    // Calendar & Task Management (4 tools)
    new() { Name = "create_event", Description = "Create a calendar event with date, time, attendees, and reminder settings" },
    new() { Name = "list_calendar_events", Description = "List all upcoming calendar events for the next N days" },
    new() { Name = "create_todo", Description = "Create a to-do item with due date, priority, and description" },
    new() { Name = "complete_todo", Description = "Mark a to-do item as completed and update your task list" },

    // File & Document Management (4 tools)
    new() { Name = "search_files", Description = "Search for files by name, extension, or content using full-text search" },
    new() { Name = "read_file", Description = "Read the contents of a text, JSON, CSV, or markdown file" },
    new() { Name = "write_file", Description = "Create or update a file with specified text content" },
    new() { Name = "import_spreadsheet", Description = "Import and parse data from Excel or CSV spreadsheets into structured format" },

    // Web & Information (4 tools)
    new() { Name = "web_search", Description = "Search the web for information, news, and resources on any topic" },
    new() { Name = "fetch_webpage", Description = "Fetch and parse the contents of a webpage for text extraction" },
    new() { Name = "get_stock_price", Description = "Get current stock price, historical data, and market trends for a ticker symbol" },
    new() { Name = "translate_text", Description = "Translate text between 50+ languages with context awareness" },

    // Math & Data Analysis (4 tools)
    new() { Name = "calculate", Description = "Perform mathematical calculations including advanced functions and statistics" },
    new() { Name = "analyze_data", Description = "Analyze datasets with descriptive statistics, correlations, and visualizations" },
    new() { Name = "generate_report", Description = "Generate formatted reports from data with charts and summaries" },
    new() { Name = "analyze_sentiment", Description = "Analyze text sentiment and extract emotions, topics, and key phrases from customer feedback" },

    // Code & Development (3 tools)
    new() { Name = "run_code", Description = "Execute Python, JavaScript, or C# code snippets and return results" },
    new() { Name = "check_syntax", Description = "Validate code syntax for multiple programming languages" },
    new() { Name = "explain_code", Description = "Provide detailed explanations of code logic and functionality" },

    // DevOps & Infrastructure (3 tools)
    new() { Name = "query_database", Description = "Query a SQL or NoSQL database and return structured results with pagination" },
    new() { Name = "check_system_health", Description = "Check the health and status of infrastructure services, Kubernetes pods, and deployments" },
    new() { Name = "run_security_scan", Description = "Run automated security vulnerability scans on code repositories and infrastructure" },
};

// ---------------------------------------------------------------------------
// 7 scenarios with long, verbose, realistic user prompts
// Each lists the tools a human would expect to be relevant.
// ---------------------------------------------------------------------------

var scenarios = new (string Name, string Prompt, string[] ExpectedTools)[]
{
    (
        "Trip Planning Ramble",
        "Hey, so I was talking to my colleague Sarah and she mentioned that Tokyo has " +
        "amazing cherry blossoms in spring. I'm actually thinking of going there next " +
        "month for a conference... anyway, I need to figure out what the weather will be " +
        "like because I need to pack appropriate clothes. Also, while I'm at it, I should " +
        "probably translate some of my presentation slides to Japanese...",
        ["get_weather", "get_weather_forecast", "translate_text", "find_location", "get_time_zone"]
    ),
    (
        "Kitchen-Sink Email",
        "OK so I've been meaning to do this all week but keep forgetting — I need to send " +
        "an email to the marketing team, specifically Jennifer, Mike, and the new intern " +
        "Alex, about the Q3 budget projections. Oh and also CC the finance department. " +
        "The subject should be about next quarter's budget review but honestly I'm not " +
        "even sure what numbers to include yet, maybe I should also look at our analytics " +
        "dashboard first to get the latest revenue figures...",
        ["send_email", "analyze_data", "generate_report", "import_spreadsheet"]
    ),
    (
        "Developer Stream of Consciousness",
        "Hmm, I've been debugging this issue all morning and I think there might be a " +
        "problem with our database queries. The response times for the customer lookup " +
        "API have been really slow lately, especially during peak hours. I wonder if we " +
        "need to add an index or maybe optimize the SQL. Can someone also check if the " +
        "Kubernetes pods are healthy? Oh and we should probably review the recent " +
        "security scan results too...",
        ["query_database", "check_system_health", "run_security_scan", "explain_code"]
    ),
    (
        "Vague Meeting Request",
        "I need to coordinate a bunch of things for next week. There's the team standup " +
        "that needs rescheduling because half the team is in a different timezone now, " +
        "plus I promised I'd set up a project review meeting with the stakeholders. Come " +
        "to think of it, I should check everyone's calendars first before sending any " +
        "invites...",
        ["list_calendar_events", "create_event", "create_meeting_invite", "get_time_zone"]
    ),
    (
        "Research Ramble",
        "I saw this fascinating article about machine learning applied to customer " +
        "sentiment analysis and it made me think — we should really be doing something " +
        "similar with our feedback data. We have thousands of customer reviews sitting " +
        "in a spreadsheet that nobody's analyzed. Maybe we could run some calculations " +
        "on the satisfaction scores first, then try to find patterns in the text...",
        ["analyze_sentiment", "import_spreadsheet", "calculate", "analyze_data"]
    ),
    (
        "Multi-Domain Chaos",
        "So my morning has been crazy — I got an urgent Slack from the VP asking about " +
        "yesterday's outage, then I realized I forgot to send the incident report email " +
        "to the whole engineering team, and on top of that we have a client demo at 3pm " +
        "that I need to set up a calendar invite for. Oh, and the client is in Japan so " +
        "I need to double-check the time zone difference and maybe translate the demo " +
        "agenda...",
        ["send_slack_message", "send_email", "create_event", "get_time_zone", "translate_text"]
    ),
    (
        "Procrastinator's Todo List",
        "Alright, I really need to get organized. Let me start by checking what meetings " +
        "I have this week so I can block out focus time. Then I need to go through my " +
        "inbox because I'm sure there are like 50 unread emails. After that, I should " +
        "create a proper todo list for the sprint — there's the API refactor, the " +
        "database migration script, and oh yeah, someone filed a security vulnerability " +
        "that I need to look into. Maybe I should also write up a status report while " +
        "I'm at it...",
        ["list_calendar_events", "check_email", "create_todo", "run_security_scan", "generate_report"]
    ),
};

Console.WriteLine($"  📦  {allTools.Length} tools loaded across 8 domains");
Console.WriteLine($"  📋  {scenarios.Length} scenarios to compare");
Console.WriteLine();

// ---------------------------------------------------------------------------
// Initialize models once (reuse across all scenarios for performance)
// ---------------------------------------------------------------------------

Console.WriteLine("⏳ Loading embedding model and local LLM...");
Console.WriteLine("   (First run downloads ~1.5 GB of models — subsequent runs are fast)");
Console.WriteLine();

var initSw = Stopwatch.StartNew();

// Shared ToolIndex — used by both modes for embeddings search
await using var index = await ToolIndex.CreateAsync(allTools);

// Shared local LLM — used for prompt distillation in Mode 2
using var chatClient = await ElBruno.LocalLLMs.LocalChatClient.CreateAsync(
    new ElBruno.LocalLLMs.LocalLLMsOptions());

initSw.Stop();
Console.WriteLine($"✅ Models ready ({initSw.Elapsed.TotalSeconds:F1}s)");

// Show model capabilities
if (chatClient.ModelInfo is { } modelInfo)
{
    Console.WriteLine($"  📊 Model: {modelInfo.ModelName ?? "unknown"}, Context: {modelInfo.MaxSequenceLength} tokens");
}

Console.WriteLine();

// ---------------------------------------------------------------------------
// Run each scenario: Mode 1 vs Mode 2
// ---------------------------------------------------------------------------

int mode2Wins = 0;
int tieCount = 0;
const int topK = 5;

for (int i = 0; i < scenarios.Length; i++)
{
    var (name, prompt, expected) = scenarios[i];

    Console.WriteLine(new string('─', 70));
    Console.WriteLine($"  SCENARIO {i + 1}/{scenarios.Length}: {name}");
    Console.WriteLine(new string('─', 70));
    Console.WriteLine();

    // Show the original verbose prompt
    Console.WriteLine($"  📝 Original prompt ({prompt.Length} chars):");
    PrintWrapped(prompt, "     ");
    Console.WriteLine();

    // Distill the prompt using the local LLM
    var distillSw = Stopwatch.StartNew();
    var distilled = await PromptDistiller.DistillIntentAsync(chatClient, prompt);
    distillSw.Stop();

    Console.WriteLine($"  🧠 LLM distilled to ({distilled.Length} chars, {distillSw.ElapsedMilliseconds}ms):");
    Console.WriteLine($"     \"{distilled}\"");
    Console.WriteLine();

    // Mode 1: raw verbose prompt → embeddings search
    var mode1Results = await index.SearchAsync(prompt, topK: topK);

    // Mode 2: distilled intent → embeddings search (same index, cleaner query)
    var mode2Results = await index.SearchAsync(distilled, topK: topK);

    // Display Mode 1 results
    Console.WriteLine("  MODE 1 — Embeddings Only (verbose prompt → vector search):");
    PrintResults(mode1Results, expected);
    Console.WriteLine();

    // Display Mode 2 results
    Console.WriteLine("  MODE 2 — LLM Distilled (verbose prompt → LLM → clean intent → vector search):");
    PrintResults(mode2Results, expected);
    Console.WriteLine();

    // Compare
    int mode1Hits = mode1Results.Count(r => expected.Contains(r.Tool.Name));
    int mode2Hits = mode2Results.Count(r => expected.Contains(r.Tool.Name));

    string verdict;
    if (mode2Hits > mode1Hits)
    {
        verdict = $"MODE 2 WINS 🏆 ({mode2Hits} vs {mode1Hits} relevant tools in top-{topK})";
        mode2Wins++;
    }
    else if (mode1Hits > mode2Hits)
    {
        verdict = $"Mode 1 wins ({mode1Hits} vs {mode2Hits} relevant tools in top-{topK})";
    }
    else
    {
        verdict = $"TIE ({mode1Hits} relevant tools each in top-{topK})";
        tieCount++;
    }

    Console.WriteLine($"  📊 {verdict}");
    Console.WriteLine($"     ✓ = tool matches expected intent for this scenario");
    Console.WriteLine();
}

// ---------------------------------------------------------------------------
// Final summary
// ---------------------------------------------------------------------------

Console.WriteLine(new string('═', 70));
Console.WriteLine();
Console.WriteLine("  📊 FINAL RESULTS");
Console.WriteLine($"     Mode 2 won:  {mode2Wins}/{scenarios.Length} scenarios");
Console.WriteLine($"     Tied:        {tieCount}/{scenarios.Length} scenarios");
Console.WriteLine($"     Mode 1 won:  {scenarios.Length - mode2Wins - tieCount}/{scenarios.Length} scenarios");
Console.WriteLine();
Console.WriteLine("  💡 KEY TAKEAWAY");
Console.WriteLine("     When users write long, rambling prompts (as they naturally do),");
Console.WriteLine("     raw embedding search gets diluted by noise words and tangents.");
Console.WriteLine("     Mode 2 uses a local LLM to extract the core intent first,");
Console.WriteLine("     then searches with a clean, focused query — better tool picks.");
Console.WriteLine();
Console.WriteLine("     ┌───────────────────────────────────────────────────────────┐");
Console.WriteLine("     │  Mode 1: Best for short, clear, direct prompts           │");
Console.WriteLine("     │  Mode 2: Best for verbose, conversational prompts        │");
Console.WriteLine("     └───────────────────────────────────────────────────────────┘");
Console.WriteLine();
Console.WriteLine("  🔗 API:");
Console.WriteLine("     // Mode 1 — embeddings only, no LLM needed");
Console.WriteLine("     var results = await ToolRouter.SearchAsync(prompt, tools);");
Console.WriteLine();
Console.WriteLine("     // Mode 2 — LLM distillation, zero-setup local model");
Console.WriteLine("     var results = await ToolRouter.SearchUsingLLMAsync(prompt, tools);");
Console.WriteLine();
Console.WriteLine("✅ Demo complete!");

// ---------------------------------------------------------------------------
// Helper methods
// ---------------------------------------------------------------------------

void PrintResults(IReadOnlyList<ToolSearchResult> results, string[] expected)
{
    for (int j = 0; j < results.Count; j++)
    {
        var r = results[j];
        var match = expected.Contains(r.Tool.Name) ? "✓" : " ";
        Console.WriteLine($"     {j + 1}. {match} {r.Tool.Name,-25} (score: {r.Score:F3})");
    }
}

void PrintWrapped(string text, string indent, int maxWidth = 65)
{
    var words = text.Split(' ');
    var line = indent;
    foreach (var word in words)
    {
        if (line.Length + word.Length > maxWidth + indent.Length && line.Length > indent.Length)
        {
            Console.WriteLine(line);
            line = indent;
        }
        line += word + " ";
    }
    if (line.Trim().Length > 0)
        Console.WriteLine(line.TrimEnd());
}
