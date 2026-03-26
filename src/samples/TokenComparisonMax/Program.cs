using Azure;
using Azure.AI.OpenAI;
using ElBruno.ModelContextProtocol.MCPToolRouter;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol.Protocol;
using OpenAI.Chat;
using Spectre.Console;

// ── Configuration ────────────────────────────────────────────────────
var configuration = new ConfigurationBuilder()
    .AddUserSecrets<Program>()
    .AddEnvironmentVariables()
    .Build();

var endpoint = configuration["AzureOpenAI:Endpoint"];
var apiKey = configuration["AzureOpenAI:ApiKey"];
var deploymentName = configuration["AzureOpenAI:DeploymentName"];

if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(deploymentName))
{
    AnsiConsole.MarkupLine("[red bold]❌ Azure OpenAI configuration not found.[/]\n");
    AnsiConsole.MarkupLine("[yellow]Set up user secrets:[/]");
    AnsiConsole.MarkupLine("  cd src/samples/TokenComparisonMax");
    AnsiConsole.MarkupLine("  dotnet user-secrets set \"AzureOpenAI:Endpoint\" \"https://your-resource.openai.azure.com/\"");
    AnsiConsole.MarkupLine("  dotnet user-secrets set \"AzureOpenAI:ApiKey\" \"your-api-key\"");
    AnsiConsole.MarkupLine("  dotnet user-secrets set \"AzureOpenAI:DeploymentName\" \"gpt-4o-mini\"");
    return;
}

// ── Banner ───────────────────────────────────────────────────────────
AnsiConsole.Write(new FigletText("MCPToolRouter").Color(Color.Cyan1));
AnsiConsole.Write(new Rule("[bold cyan]🚀 EXTREME Token Comparison — 120+ Tools[/]").RuleStyle("cyan"));
AnsiConsole.WriteLine();

AnsiConsole.MarkupLine($"[green]✅ Configuration loaded[/]");
AnsiConsole.MarkupLine($"   Endpoint:   [dim]{endpoint}[/]");
AnsiConsole.MarkupLine($"   Deployment: [dim]{deploymentName}[/]");
AnsiConsole.WriteLine();

// ── Azure OpenAI client ──────────────────────────────────────────────
var azureClient = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
var chatClient = azureClient.GetChatClient(deploymentName);

// Azure OpenAI Pricing — GPT-5-mini (March 2026)
// Source: https://azure.microsoft.com/en-us/pricing/details/azure-openai/
const decimal InputPricePerToken = 0.25m / 1_000_000m;   // $0.25 per 1M input tokens
const decimal OutputPricePerToken = 2.00m / 1_000_000m;   // $2.00 per 1M output tokens

// ── 120+ MCP Tool Definitions across 12 domains ─────────────────────
var mcpTools = BuildToolDefinitions();
AnsiConsole.MarkupLine($"[bold]📦 Created [cyan]{mcpTools.Length}[/] tool definitions across 12 domains[/]\n");

// ── Build ToolIndex once (embedding model loads here) ────────────────
ToolIndex index;
await AnsiConsole.Status()
    .Spinner(Spinner.Known.Dots)
    .SpinnerStyle(Style.Parse("cyan"))
    .StartAsync("Loading embedding model and indexing tools...", async ctx =>
    {
        // Intentional no-op — we build outside so we can assign
        await Task.CompletedTask;
    });

index = await AnsiConsole.Status()
    .Spinner(Spinner.Known.Dots)
    .SpinnerStyle(Style.Parse("cyan"))
    .StartAsync("Loading embedding model and indexing 120+ tools...", async ctx =>
    {
        var indexOptions = new ToolIndexOptions { QueryCacheSize = 20 };
        return await ToolIndex.CreateAsync(mcpTools, indexOptions);
    });

AnsiConsole.MarkupLine($"[green]✅ ToolIndex ready — {index.Count} tools indexed[/]\n");

// ── Test Scenarios ───────────────────────────────────────────────────
var scenarios = new (string Prompt, string Domain)[]
{
    ("What is the weather like in Tokyo right now?", "Weather"),
    ("Send an email to the marketing team about next quarter's budget", "Email"),
    ("List all CSV files modified in the last week", "FileSystem"),
    ("Run a SQL query to find top 10 customers by revenue", "Database"),
    ("Schedule a recurring standup meeting every weekday at 9am", "Calendar"),
    ("Calculate the compound interest on a $50,000 investment at 7% over 10 years", "Math"),
    ("Translate this document from English to Japanese", "Translation"),
    ("Deploy the latest Docker image to the staging Kubernetes cluster", "DevOps"),
    ("Check the SSL certificate expiration for our production domains", "Security"),
    ("Show me the API error rate and p99 latency for the last hour", "Analytics"),
    ("Fine-tune a sentiment analysis model on our customer feedback data", "AI/ML"),
    ("Scrape product prices from the competitor's website and compare", "Web"),
};

var results = new List<ScenarioResult>();

// Convert all MCP tools to ChatTools once
var allChatTools = mcpTools.Select(ConvertToChatTool).ToList();

// ── Live Table: real-time updates as each scenario processes ─────────
var liveTable = new Table()
    .Border(TableBorder.Rounded)
    .BorderColor(Color.Cyan1)
    .Title("[bold cyan]⚡ Live Token Comparison — 120+ Tools[/]")
    .AddColumn(new TableColumn("[bold]#[/]").Centered())
    .AddColumn(new TableColumn("[bold]Domain[/]").Width(12))
    .AddColumn(new TableColumn("[bold]Prompt[/]").Width(40))
    .AddColumn(new TableColumn("[bold]Standard[/]").RightAligned())
    .AddColumn(new TableColumn("[bold]Routed[/]").RightAligned())
    .AddColumn(new TableColumn("[bold]Saved[/]").RightAligned())
    .AddColumn(new TableColumn("[bold]Savings %[/]").RightAligned());

await AnsiConsole.Live(liveTable)
    .AutoClear(false)
    .Overflow(VerticalOverflow.Ellipsis)
    .StartAsync(async ctx =>
    {
        for (int i = 0; i < scenarios.Length; i++)
        {
            var (prompt, domain) = scenarios[i];
            var num = i + 1;

            // Add a "processing" row
            liveTable.AddRow(
                $"[yellow]{num}[/]",
                $"[yellow]{domain}[/]",
                $"[yellow]{Truncate(prompt, 38)}[/]",
                "[dim]running...[/]",
                "[dim]...[/]",
                "[dim]...[/]",
                "[dim]...[/]");
            ctx.Refresh();

            // ── Standard mode: all tools ─────────────────────────
            var standardOptions = new ChatCompletionOptions();
            foreach (var tool in allChatTools)
                standardOptions.Tools.Add(tool);

            var standardResponse = await chatClient.CompleteChatAsync(
                [new UserChatMessage(prompt)], standardOptions);
            var stdInput = standardResponse.Value.Usage.InputTokenCount;
            var stdOutput = standardResponse.Value.Usage.OutputTokenCount;

            // ── Routed mode: top-5 relevant tools ────────────────
            var relevant = await index.SearchAsync(prompt, topK: 5);
            var relevantNames = relevant.Select(r => r.Tool.Name).ToHashSet();
            var filteredTools = mcpTools
                .Where(t => relevantNames.Contains(t.Name))
                .Select(ConvertToChatTool)
                .ToList();

            var routedOptions = new ChatCompletionOptions();
            foreach (var tool in filteredTools)
                routedOptions.Tools.Add(tool);

            var routedResponse = await chatClient.CompleteChatAsync(
                [new UserChatMessage(prompt)], routedOptions);
            var rtdInput = routedResponse.Value.Usage.InputTokenCount;
            var rtdOutput = routedResponse.Value.Usage.OutputTokenCount;

            var saved = stdInput - rtdInput;
            var pct = stdInput > 0 ? (double)saved / stdInput * 100 : 0;

            var standardCost = (stdInput * InputPricePerToken) + (stdOutput * OutputPricePerToken);
            var routedCost = (rtdInput * InputPricePerToken) + (rtdOutput * OutputPricePerToken);
            var moneySaved = standardCost - routedCost;

            results.Add(new ScenarioResult(prompt, domain, stdInput, rtdInput, saved, pct,
                standardCost, routedCost, moneySaved,
                relevant.Select(r => (r.Tool.Name, r.Score)).ToList()));

            // Update the last row with actual numbers
            liveTable.Rows.Update(liveTable.Rows.Count - 1, 0, new Markup($"[green]{num}[/]"));
            liveTable.Rows.Update(liveTable.Rows.Count - 1, 1, new Markup($"[green]{domain}[/]"));
            liveTable.Rows.Update(liveTable.Rows.Count - 1, 2, new Markup($"[white]{Truncate(prompt, 38)}[/]"));
            liveTable.Rows.Update(liveTable.Rows.Count - 1, 3, new Markup($"[red]{stdInput:N0}[/]"));
            liveTable.Rows.Update(liveTable.Rows.Count - 1, 4, new Markup($"[green]{rtdInput:N0}[/]"));
            liveTable.Rows.Update(liveTable.Rows.Count - 1, 5, new Markup($"[cyan]{saved:N0}[/]"));
            liveTable.Rows.Update(liveTable.Rows.Count - 1, 6, new Markup($"[bold magenta]{pct:F1}%[/]"));
            ctx.Refresh();
        }
    });

AnsiConsole.WriteLine();

// ── Summary Table ────────────────────────────────────────────────────
AnsiConsole.Write(new Rule("[bold green]📊 Final Summary[/]").RuleStyle("green"));
AnsiConsole.WriteLine();

var summaryTable = new Table()
    .Border(TableBorder.HeavyHead)
    .BorderColor(Color.Green)
    .Title("[bold green]Token Savings with MCPToolRouter (120+ tools → top 5)[/]")
    .AddColumn(new TableColumn("[bold]#[/]").Centered())
    .AddColumn(new TableColumn("[bold]Domain[/]").Width(12))
    .AddColumn(new TableColumn("[bold]Prompt[/]").Width(44))
    .AddColumn(new TableColumn("[bold]Standard Tokens[/]").RightAligned())
    .AddColumn(new TableColumn("[bold]Routed Tokens[/]").RightAligned())
    .AddColumn(new TableColumn("[bold]Tokens Saved[/]").RightAligned())
    .AddColumn(new TableColumn("[bold]Savings[/]").RightAligned());

for (int i = 0; i < results.Count; i++)
{
    var r = results[i];
    summaryTable.AddRow(
        $"{i + 1}",
        $"[cyan]{r.Domain}[/]",
        Truncate(r.Prompt, 42),
        $"[red]{r.StandardTokens:N0}[/]",
        $"[green]{r.RoutedTokens:N0}[/]",
        $"[cyan]{r.Saved:N0}[/]",
        $"[bold magenta]{r.Pct:F1}%[/]");
}

// Totals row
var totalStd = results.Sum(r => r.StandardTokens);
var totalRtd = results.Sum(r => r.RoutedTokens);
var totalSaved = totalStd - totalRtd;
var totalPct = totalStd > 0 ? (double)totalSaved / totalStd * 100 : 0;
var totalMoneySaved = results.Sum(r => r.MoneySaved);

summaryTable.AddEmptyRow();
summaryTable.AddRow(
    "[bold]Σ[/]",
    "[bold yellow]TOTAL[/]",
    $"[bold]{results.Count} scenarios[/]",
    $"[bold red]{totalStd:N0}[/]",
    $"[bold green]{totalRtd:N0}[/]",
    $"[bold cyan]{totalSaved:N0}[/]",
    $"[bold yellow on black] {totalPct:F1}% [/]");

AnsiConsole.Write(summaryTable);
AnsiConsole.WriteLine();

// ── Production-Scale Cost Projections ─────────────────────────────
var savedTokensPerCall = totalSaved / results.Count;
var costSavedPerCall = totalMoneySaved / results.Count;

var projectionTable = new Table()
    .Border(TableBorder.Rounded)
    .BorderColor(Color.Yellow)
    .Title("[bold yellow]💰 Projected Savings at Production Scale[/]")
    .AddColumn(new TableColumn("[bold]Daily Calls[/]").RightAligned())
    .AddColumn(new TableColumn("[bold]Tokens Saved / Day[/]").RightAligned())
    .AddColumn(new TableColumn("[bold]Monthly Savings[/]").RightAligned())
    .AddColumn(new TableColumn("[bold]Yearly Savings[/]").RightAligned());

int[] scales = [100, 1_000, 10_000, 100_000, 1_000_000];
foreach (var daily in scales)
{
    var dailyTokens = (long)savedTokensPerCall * daily;
    var monthlyCost = costSavedPerCall * daily * 30;
    var yearlyCost = costSavedPerCall * daily * 365;
    projectionTable.AddRow(
        $"[white]{daily:N0}[/]",
        $"[cyan]{dailyTokens:N0}[/]",
        $"[green]${monthlyCost:F2}[/]",
        $"[bold green]${yearlyCost:F2}[/]");
}

AnsiConsole.Write(projectionTable);
AnsiConsole.WriteLine();

AnsiConsole.Write(new Panel(
    $"[bold]By routing {mcpTools.Length} tools through MCPToolRouter, " +
    $"you save [cyan]{totalSaved:N0}[/] input tokens across {results.Count} calls " +
    $"([magenta]{totalPct:F1}%[/] average savings per request).\n" +
    $"At [yellow]10,000 calls/day[/], that's [bold green]${costSavedPerCall * 10_000 * 30:F2}/month[/] saved " +
    $"on GPT-5-mini pricing![/]")
    .Header("[bold yellow]💰 Bottom Line[/]")
    .Border(BoxBorder.Double)
    .BorderColor(Color.Yellow)
    .Padding(1, 1));
AnsiConsole.WriteLine();

// ── Per-scenario tool selections ─────────────────────────────────────
AnsiConsole.Write(new Rule("[bold dim]🔍 Routed Tool Selections (per scenario)[/]").RuleStyle("dim"));
AnsiConsole.WriteLine();

foreach (var r in results)
{
    AnsiConsole.MarkupLine($"[bold]{r.Domain}[/]: [dim]{Truncate(r.Prompt, 60)}[/]");
    foreach (var (name, score) in r.SelectedTools)
    {
        AnsiConsole.MarkupLine($"   ✅ [green]{name,-36}[/] score: [cyan]{score:F3}[/]");
    }
    AnsiConsole.WriteLine();
}

AnsiConsole.Write(new Rule("[bold green]✅ Comparison Complete[/]").RuleStyle("green"));
AnsiConsole.WriteLine();
AnsiConsole.MarkupLine("[dim]💡 Pricing based on Azure OpenAI GPT-5-mini: $0.25/1M input, $2.00/1M output tokens[/]");
AnsiConsole.MarkupLine("[dim]   Source: https://azure.microsoft.com/en-us/pricing/details/azure-openai/[/]");

await index.DisposeAsync();
return;

// ── Helpers ──────────────────────────────────────────────────────────
static string Truncate(string text, int maxLen) =>
    text.Length <= maxLen ? text : text[..(maxLen - 1)] + "…";

static ChatTool ConvertToChatTool(Tool mcpTool) =>
    ChatTool.CreateFunctionTool(mcpTool.Name, mcpTool.Description ?? string.Empty);

// ── Tool Definitions: 120+ tools across 12 domains ──────────────────
static Tool[] BuildToolDefinitions() =>
[
    // ── Weather & Environment (10) ───────────────────────────────
    new() { Name = "get_current_weather", Description = "Retrieves current weather conditions for a specified location including temperature, humidity, wind speed, pressure, and sky conditions" },
    new() { Name = "get_weather_forecast", Description = "Gets a multi-day weather forecast for a location with daily highs, lows, precipitation probability, and conditions" },
    new() { Name = "get_weather_alerts", Description = "Retrieves active severe weather alerts, watches, and warnings for a specified geographic region" },
    new() { Name = "get_air_quality", Description = "Returns the Air Quality Index (AQI) and pollutant levels for a given location including PM2.5, PM10, and ozone" },
    new() { Name = "get_uv_index", Description = "Returns the current and forecasted UV index for a location along with sun protection recommendations" },
    new() { Name = "get_sunrise_sunset", Description = "Returns sunrise, sunset, dawn, and dusk times for a given location and date" },
    new() { Name = "get_historical_weather", Description = "Retrieves historical weather data for a location over a specified date range for climate analysis" },
    new() { Name = "get_pollen_count", Description = "Returns current pollen counts and allergy forecast for a location including grass, tree, and weed pollen levels" },
    new() { Name = "get_tide_info", Description = "Returns tide predictions and current tide levels for coastal locations and harbors" },
    new() { Name = "get_wind_map", Description = "Generates a wind speed and direction map for a region showing current atmospheric wind patterns" },

    // ── Email & Messaging (10) ───────────────────────────────────
    new() { Name = "send_email", Description = "Sends an email message with subject, body, recipients (to, cc, bcc), and optional file attachments via SMTP" },
    new() { Name = "read_inbox", Description = "Retrieves unread emails from the inbox with options to filter by sender, subject, date range, or importance" },
    new() { Name = "search_emails", Description = "Performs full-text search across the email archive matching keywords, phrases, sender, date range, and labels" },
    new() { Name = "delete_email", Description = "Permanently deletes an email message by its unique identifier or moves it to the trash folder" },
    new() { Name = "create_email_draft", Description = "Creates a new email draft that can be edited and sent later, with subject, body, and recipients" },
    new() { Name = "send_sms", Description = "Sends an SMS text message to a phone number with optional delivery confirmation and scheduling" },
    new() { Name = "send_slack_message", Description = "Posts a message to a Slack channel or direct message thread with optional attachments and formatting" },
    new() { Name = "send_teams_message", Description = "Sends a message to a Microsoft Teams channel or chat including rich text, images, and adaptive cards" },
    new() { Name = "list_email_folders", Description = "Lists all email folders and labels in the mailbox with message counts and unread counts" },
    new() { Name = "set_email_rule", Description = "Creates an email rule to automatically sort, label, forward, or delete incoming messages based on criteria" },

    // ── File System & Storage (10) ───────────────────────────────
    new() { Name = "read_file", Description = "Reads and returns the full text content of a file at the specified path with encoding detection" },
    new() { Name = "write_file", Description = "Writes or overwrites content to a file at the specified path, creating parent directories if needed" },
    new() { Name = "list_directory", Description = "Lists all files and subdirectories in a directory path with size, modification date, and permissions" },
    new() { Name = "search_files", Description = "Recursively searches the filesystem for files matching a glob pattern, name fragment, or containing text" },
    new() { Name = "copy_file", Description = "Copies a file or directory from source to destination path with optional overwrite and progress tracking" },
    new() { Name = "move_file", Description = "Moves or renames a file or directory from one path to another with conflict resolution options" },
    new() { Name = "delete_file", Description = "Deletes a file or directory permanently or moves it to the recycle bin with confirmation" },
    new() { Name = "get_file_info", Description = "Returns detailed metadata for a file including size, creation date, modified date, hash, and MIME type" },
    new() { Name = "compress_files", Description = "Compresses files and directories into a ZIP, TAR.GZ, or 7Z archive with compression level options" },
    new() { Name = "upload_to_cloud_storage", Description = "Uploads a local file to cloud storage (S3, Azure Blob, GCS) with progress tracking and checksum verification" },

    // ── Database & Data (10) ─────────────────────────────────────
    new() { Name = "query_database", Description = "Executes a read-only SQL SELECT query against a database and returns formatted tabular results with column types" },
    new() { Name = "insert_record", Description = "Inserts one or more new records into a database table with input validation and returns generated IDs" },
    new() { Name = "update_record", Description = "Updates existing database records matching specified conditions with new field values and returns affected row count" },
    new() { Name = "delete_record", Description = "Deletes database records matching specified conditions with optional soft-delete support and audit logging" },
    new() { Name = "list_tables", Description = "Lists all tables and views in a database schema with row counts, column summaries, and size information" },
    new() { Name = "describe_table", Description = "Returns the full schema definition of a database table including columns, types, constraints, and indexes" },
    new() { Name = "export_to_csv", Description = "Exports query results or an entire table to a CSV file with configurable delimiter, encoding, and headers" },
    new() { Name = "import_csv", Description = "Imports data from a CSV file into a database table with column mapping, type conversion, and error handling" },
    new() { Name = "run_migration", Description = "Executes a database schema migration script to create, alter, or drop tables, columns, and indexes" },
    new() { Name = "backup_database", Description = "Creates a full or incremental backup of a database to a specified location with compression and encryption" },

    // ── Calendar & Scheduling (10) ───────────────────────────────
    new() { Name = "create_calendar_event", Description = "Creates a new calendar event with title, date, time, duration, location, description, and attendee invitations" },
    new() { Name = "list_calendar_events", Description = "Lists calendar events within a specified date range with filtering by calendar, attendee, or keyword" },
    new() { Name = "update_calendar_event", Description = "Modifies an existing calendar event's details including time, location, description, or attendee list" },
    new() { Name = "delete_calendar_event", Description = "Deletes a calendar event by ID with options to notify attendees and handle recurring event instances" },
    new() { Name = "check_availability", Description = "Checks schedule availability for one or more people across a date range to find open meeting slots" },
    new() { Name = "create_reminder", Description = "Creates a time-based or location-based reminder with customizable notification settings and recurrence" },
    new() { Name = "set_recurring_event", Description = "Creates a recurring calendar event with flexible patterns: daily, weekly, monthly, or custom recurrence rules" },
    new() { Name = "get_timezone_info", Description = "Returns timezone details and current local time for a city or timezone identifier with DST information" },
    new() { Name = "sync_calendars", Description = "Synchronizes events between multiple calendar providers (Google, Outlook, Apple) with conflict resolution" },
    new() { Name = "create_scheduling_poll", Description = "Creates a poll for attendees to vote on preferred meeting times from a set of proposed slots" },

    // ── Math & Science (10) ──────────────────────────────────────
    new() { Name = "calculate_expression", Description = "Evaluates a mathematical expression supporting arithmetic, trigonometry, logarithms, and complex numbers" },
    new() { Name = "convert_units", Description = "Converts a value between measurement units including length, mass, temperature, volume, speed, and currency" },
    new() { Name = "calculate_statistics", Description = "Computes descriptive statistics for a dataset: mean, median, mode, standard deviation, variance, percentiles" },
    new() { Name = "solve_equation", Description = "Solves algebraic equations symbolically or numerically, including linear, quadratic, and systems of equations" },
    new() { Name = "calculate_compound_interest", Description = "Computes compound interest over time with principal, rate, compounding frequency, and regular contributions" },
    new() { Name = "matrix_operations", Description = "Performs matrix arithmetic: multiplication, inversion, determinant, eigenvalues, and decomposition" },
    new() { Name = "generate_random_numbers", Description = "Generates random numbers from specified distributions: uniform, normal, Poisson, exponential, or custom ranges" },
    new() { Name = "calculate_distance", Description = "Calculates the geographic distance between two coordinates using the Haversine formula with elevation support" },
    new() { Name = "chemical_formula_parser", Description = "Parses chemical formulas and returns molecular weight, elemental composition, and structural information" },
    new() { Name = "physics_calculator", Description = "Solves common physics problems: kinematics, force, energy, electricity, optics, and thermodynamics calculations" },

    // ── Translation & Language (10) ──────────────────────────────
    new() { Name = "translate_text", Description = "Translates text from a source language to a target language using neural machine translation with context awareness" },
    new() { Name = "detect_language", Description = "Detects the language of input text with confidence scores and returns ISO language codes" },
    new() { Name = "transliterate_text", Description = "Converts text from one script to another (e.g., Cyrillic to Latin, Kanji to Romaji) preserving pronunciation" },
    new() { Name = "check_grammar", Description = "Analyzes text for grammar, spelling, punctuation, and style errors with correction suggestions" },
    new() { Name = "summarize_text", Description = "Generates a concise summary of a long document preserving key points, themes, and conclusions" },
    new() { Name = "extract_keywords", Description = "Extracts the most important keywords and key phrases from text using NLP-based relevance scoring" },
    new() { Name = "analyze_sentiment", Description = "Performs sentiment analysis on text returning positive, negative, neutral scores and emotional tone categories" },
    new() { Name = "text_to_speech", Description = "Converts text to natural-sounding audio in multiple languages and voices with speed and pitch controls" },
    new() { Name = "speech_to_text", Description = "Transcribes audio or speech recordings to text with speaker diarization and punctuation restoration" },
    new() { Name = "dictionary_lookup", Description = "Looks up word definitions, synonyms, antonyms, etymology, pronunciation, and usage examples in multiple languages" },

    // ── Web & HTTP (10) ──────────────────────────────────────────
    new() { Name = "http_get", Description = "Sends an HTTP GET request to a URL and returns the response body, status code, and headers" },
    new() { Name = "http_post", Description = "Sends an HTTP POST request with a JSON or form body to a URL and returns the response" },
    new() { Name = "scrape_webpage", Description = "Extracts structured content from a webpage including text, links, images, and metadata using CSS selectors" },
    new() { Name = "check_url_status", Description = "Checks if a URL is reachable and returns HTTP status code, response time, redirect chain, and SSL info" },
    new() { Name = "download_file_url", Description = "Downloads a file from a URL to a local path with progress tracking, resume support, and checksum validation" },
    new() { Name = "shorten_url", Description = "Creates a shortened URL using a URL shortening service with optional custom alias and expiration" },
    new() { Name = "parse_html", Description = "Parses an HTML document and extracts elements using XPath or CSS selector queries returning structured data" },
    new() { Name = "whois_lookup", Description = "Performs a WHOIS lookup for a domain name returning registrar, registration dates, nameservers, and contact info" },
    new() { Name = "dns_lookup", Description = "Queries DNS records for a domain: A, AAAA, MX, CNAME, TXT, NS, and SOA records with TTL information" },
    new() { Name = "trace_route", Description = "Performs a network traceroute to a host showing each hop, latency, and geographic location of routers" },

    // ── DevOps & CI/CD (10) ──────────────────────────────────────
    new() { Name = "run_ci_pipeline", Description = "Triggers a CI/CD pipeline or workflow by name with optional parameters, branch, and environment selection" },
    new() { Name = "get_pipeline_status", Description = "Returns the current status of a CI/CD pipeline run including stage progress, logs, and duration" },
    new() { Name = "deploy_to_environment", Description = "Deploys an application version to a specified environment (dev, staging, production) with rollback options" },
    new() { Name = "docker_build", Description = "Builds a Docker container image from a Dockerfile with build arguments, caching, and multi-stage support" },
    new() { Name = "docker_push", Description = "Pushes a Docker image to a container registry (Docker Hub, ACR, ECR, GCR) with tag and manifest management" },
    new() { Name = "kubectl_apply", Description = "Applies Kubernetes manifests to a cluster to create, update, or delete pods, services, and deployments" },
    new() { Name = "terraform_plan", Description = "Runs Terraform plan to preview infrastructure changes showing resources to create, modify, or destroy" },
    new() { Name = "terraform_apply", Description = "Applies Terraform configuration changes to provision or update cloud infrastructure with state management" },
    new() { Name = "check_service_health", Description = "Performs health checks on deployed services returning status, uptime, response time, and error rates" },
    new() { Name = "rollback_deployment", Description = "Rolls back a deployment to a previous version with automatic health checking and traffic shifting" },

    // ── Security & Auth (10) ─────────────────────────────────────
    new() { Name = "generate_api_key", Description = "Generates a new API key with configurable permissions, expiration, rate limits, and scope restrictions" },
    new() { Name = "rotate_credentials", Description = "Rotates passwords, API keys, or certificates for a service with zero-downtime credential swapping" },
    new() { Name = "check_ssl_certificate", Description = "Inspects an SSL/TLS certificate for a domain returning issuer, expiration, chain validity, and cipher details" },
    new() { Name = "scan_vulnerabilities", Description = "Scans a codebase or container image for known security vulnerabilities with severity ratings and remediation advice" },
    new() { Name = "encrypt_data", Description = "Encrypts data using AES-256, RSA, or other algorithms with key management and initialization vector handling" },
    new() { Name = "decrypt_data", Description = "Decrypts previously encrypted data using the specified algorithm and decryption key or private key" },
    new() { Name = "hash_data", Description = "Computes cryptographic hashes (SHA-256, SHA-512, MD5, BLAKE3) of data or files for integrity verification" },
    new() { Name = "manage_secrets", Description = "Stores, retrieves, or rotates secrets in a vault (Azure Key Vault, AWS Secrets Manager, HashiCorp Vault)" },
    new() { Name = "audit_permissions", Description = "Audits user and service account permissions across systems to identify excessive access and policy violations" },
    new() { Name = "generate_jwt_token", Description = "Creates a signed JWT token with custom claims, issuer, audience, and expiration for authentication" },

    // ── Analytics & Monitoring (10) ──────────────────────────────
    new() { Name = "get_metrics", Description = "Retrieves time-series metrics (CPU, memory, requests, latency, error rate) for services and infrastructure" },
    new() { Name = "create_dashboard", Description = "Creates a monitoring dashboard with configurable widgets, charts, and real-time data visualizations" },
    new() { Name = "set_alert_rule", Description = "Configures an alerting rule that triggers notifications when metrics exceed thresholds or anomalies are detected" },
    new() { Name = "query_logs", Description = "Searches and filters application and infrastructure logs using structured queries with time range and severity" },
    new() { Name = "get_error_report", Description = "Generates an error report summarizing exceptions, stack traces, affected users, and occurrence frequency" },
    new() { Name = "track_event", Description = "Records a custom analytics event with properties and metrics for product usage tracking and funnels" },
    new() { Name = "generate_report", Description = "Generates a formatted analytics report with charts, tables, and insights from metrics and log data" },
    new() { Name = "get_uptime_status", Description = "Returns uptime percentage, incident history, and SLA compliance for monitored services and endpoints" },
    new() { Name = "trace_request", Description = "Retrieves a distributed trace for a request ID showing the full call chain across microservices with timing" },
    new() { Name = "forecast_capacity", Description = "Analyzes historical metrics to forecast future resource usage and capacity needs with confidence intervals" },

    // ── AI & ML (10) ─────────────────────────────────────────────
    new() { Name = "generate_image", Description = "Generates an image from a text description prompt using AI image generation models with size and style options" },
    new() { Name = "analyze_image", Description = "Analyzes an image using computer vision to identify objects, faces, text (OCR), scenes, and generate descriptions" },
    new() { Name = "classify_text", Description = "Classifies text into categories using a trained ML model with confidence scores for each predicted label" },
    new() { Name = "generate_embeddings", Description = "Generates vector embeddings for text, images, or other data for semantic search and similarity comparisons" },
    new() { Name = "train_model", Description = "Initiates training of a machine learning model with specified dataset, hyperparameters, and evaluation metrics" },
    new() { Name = "evaluate_model", Description = "Evaluates a trained ML model against a test dataset returning accuracy, precision, recall, F1, and confusion matrix" },
    new() { Name = "run_inference", Description = "Runs inference on a deployed ML model with input data and returns predictions with confidence scores" },
    new() { Name = "finetune_llm", Description = "Fine-tunes a large language model on a custom dataset with LoRA/QLoRA configuration and training parameters" },
    new() { Name = "extract_entities", Description = "Performs Named Entity Recognition (NER) on text to extract people, organizations, locations, dates, and custom entities" },
    new() { Name = "cluster_data", Description = "Performs clustering analysis on a dataset using K-means, DBSCAN, or hierarchical methods with visualization" },
];

// ── Result record ────────────────────────────────────────────────────
record ScenarioResult(
    string Prompt,
    string Domain,
    int StandardTokens,
    int RoutedTokens,
    int Saved,
    double Pct,
    decimal StandardCost,
    decimal RoutedCost,
    decimal MoneySaved,
    List<(string Name, float Score)> SelectedTools);

// Required for user-secrets binding
public partial class Program { }
