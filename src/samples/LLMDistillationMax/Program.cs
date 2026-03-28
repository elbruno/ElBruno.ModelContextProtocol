// ============================================================================
// LLM Distillation Max
// Demonstrates Mode 2 (LLM-assisted routing) at scale — 120+ tools across
// 12 domains, with 12 paragraph-length, noisy, real-world prompts that show
// why Mode 1 (embeddings-only) struggles with verbose user input.
//
// This sample runs 100% locally — no Azure OpenAI needed. The local LLM is
// auto-downloaded on first run (~1.5 GB).
//
// API used:
//   Mode 1: ToolRouter.SearchAsync(prompt, tools)
//   Mode 2: ToolRouter.SearchUsingLLMAsync(prompt, tools, chatClient)
// ============================================================================

using System.Diagnostics;
using ElBruno.ModelContextProtocol.MCPToolRouter;
using ModelContextProtocol.Protocol;
using Spectre.Console;

// ── Banner ───────────────────────────────────────────────────────────
AnsiConsole.Write(new FigletText("LLM Distill Max").Color(Color.Cyan1));
AnsiConsole.Write(new Rule("[bold cyan]🧠 LLM Distillation at Scale — 120+ Tools, 12 Long Prompts[/]").RuleStyle("cyan"));
AnsiConsole.WriteLine();
AnsiConsole.MarkupLine("[dim]Mode 1 (embeddings-only) vs Mode 2 (LLM-distilled) — who picks better tools?[/]");
AnsiConsole.MarkupLine("[dim]No Azure required. Everything runs locally.[/]");
AnsiConsole.WriteLine();

// ── 120+ MCP Tool Definitions across 12 domains ─────────────────────
var tools = BuildToolDefinitions();
AnsiConsole.MarkupLine($"[bold]📦 Created [cyan]{tools.Length}[/] tool definitions across 12 domains[/]");
AnsiConsole.WriteLine();

// ── Load local LLM (for distillation display + Mode 2) ──────────────
using var chatClient = await AnsiConsole.Status()
    .Spinner(Spinner.Known.Dots)
    .SpinnerStyle(Style.Parse("cyan"))
    .StartAsync("Loading local LLM for prompt distillation (~1.5 GB first run)...", async ctx =>
    {
        return await ElBruno.LocalLLMs.LocalChatClient.CreateAsync(
            new ElBruno.LocalLLMs.LocalLLMsOptions());
    });

AnsiConsole.MarkupLine("[green]✅ Local LLM ready[/]");
AnsiConsole.WriteLine();

// ── Define 12 scenarios with LONG, messy, real-world prompts ─────────
var scenarios = BuildScenarios();
AnsiConsole.MarkupLine($"[bold]📋 {scenarios.Length} scenarios with paragraph-length prompts[/]");
AnsiConsole.WriteLine();

// ── Process each scenario ────────────────────────────────────────────
var results = new List<ScenarioResult>();
const int topK = 5;

for (int i = 0; i < scenarios.Length; i++)
{
    var (name, prompt, expected) = scenarios[i];
    var wordCount = prompt.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

    AnsiConsole.Write(new Rule($"[bold yellow]Scenario {i + 1}/{scenarios.Length}: {name}[/]").RuleStyle("yellow"));
    AnsiConsole.WriteLine();

    // Show the original verbose prompt
    AnsiConsole.Write(new Panel(Markup.Escape(prompt))
        .Header($"[bold]📝 Original Prompt ({wordCount} words, {prompt.Length} chars)[/]")
        .Border(BoxBorder.Rounded)
        .BorderColor(Color.Grey)
        .Padding(1, 0));
    AnsiConsole.WriteLine();

    // Distill the prompt via local LLM (for display)
    var distillSw = Stopwatch.StartNew();
    var distilled = await PromptDistiller.DistillIntentAsync(chatClient, prompt);
    distillSw.Stop();

    AnsiConsole.Write(new Panel($"[italic cyan]{Markup.Escape(distilled)}[/]")
        .Header($"[bold]🧠 LLM Distilled Intent ({distillSw.ElapsedMilliseconds}ms)[/]")
        .Border(BoxBorder.Rounded)
        .BorderColor(Color.Cyan1)
        .Padding(1, 0));
    AnsiConsole.WriteLine();

    // Mode 1: embeddings-only search with raw verbose prompt
    var m1Sw = Stopwatch.StartNew();
    var m1Results = await ToolRouter.SearchAsync(prompt, tools, topK: topK);
    m1Sw.Stop();

    // Mode 2: LLM-distilled search (distills internally, then searches)
    var m2Sw = Stopwatch.StartNew();
    var m2Results = await ToolRouter.SearchUsingLLMAsync(prompt, tools, chatClient, topK: topK);
    m2Sw.Stop();

    // Build comparison table
    var table = new Table()
        .Border(TableBorder.Rounded)
        .BorderColor(Color.Grey)
        .Title("[bold]Tool Selection Comparison[/]")
        .AddColumn(new TableColumn("[bold]#[/]").Centered().Width(3))
        .AddColumn(new TableColumn($"[bold cyan]Mode 1: Embeddings Only[/] [dim]({m1Sw.ElapsedMilliseconds}ms)[/]").Width(32))
        .AddColumn(new TableColumn("[bold]Score[/]").RightAligned().Width(7))
        .AddColumn(new TableColumn($"[bold green]Mode 2: LLM Distilled[/] [dim]({m2Sw.ElapsedMilliseconds}ms)[/]").Width(32))
        .AddColumn(new TableColumn("[bold]Score[/]").RightAligned().Width(7));

    var maxRows = Math.Max(m1Results.Count, m2Results.Count);
    for (int j = 0; j < maxRows; j++)
    {
        var m1Name = j < m1Results.Count ? m1Results[j].Tool.Name : "";
        var m1Score = j < m1Results.Count ? $"{m1Results[j].Score:F3}" : "";
        var m2Name = j < m2Results.Count ? m2Results[j].Tool.Name : "";
        var m2Score = j < m2Results.Count ? $"{m2Results[j].Score:F3}" : "";

        var m1Hit = expected.Contains(m1Name);
        var m2Hit = expected.Contains(m2Name);

        table.AddRow(
            $"{j + 1}",
            m1Hit ? $"[green]✓ {Markup.Escape(m1Name)}[/]" : $"[red]✗ {Markup.Escape(m1Name)}[/]",
            m1Hit ? $"[green]{m1Score}[/]" : $"[dim]{m1Score}[/]",
            m2Hit ? $"[green]✓ {Markup.Escape(m2Name)}[/]" : $"[red]✗ {Markup.Escape(m2Name)}[/]",
            m2Hit ? $"[green]{m2Score}[/]" : $"[dim]{m2Score}[/]");
    }

    AnsiConsole.Write(table);

    // Score and verdict
    var m1Hits = m1Results.Count(r => expected.Contains(r.Tool.Name));
    var m2Hits = m2Results.Count(r => expected.Contains(r.Tool.Name));

    string winner;
    if (m2Hits > m1Hits)
        winner = "Mode 2";
    else if (m1Hits > m2Hits)
        winner = "Mode 1";
    else
        winner = "Tie";

    var verdictColor = winner == "Mode 2" ? "green" : winner == "Mode 1" ? "red" : "yellow";
    var verdictEmoji = winner == "Mode 2" ? "🏆" : winner == "Mode 1" ? "⚠️" : "🤝";

    AnsiConsole.MarkupLine($"  [{verdictColor} bold]{verdictEmoji} {winner}[/] — " +
        $"Mode 1: [cyan]{m1Hits}/{topK}[/] relevant | " +
        $"Mode 2: [green]{m2Hits}/{topK}[/] relevant | " +
        $"[dim]✓ = matches expected tools for this scenario[/]");
    AnsiConsole.WriteLine();

    results.Add(new ScenarioResult(
        name, prompt, wordCount, distilled,
        m1Hits, m2Hits, winner,
        m1Sw.ElapsedMilliseconds, m2Sw.ElapsedMilliseconds, distillSw.ElapsedMilliseconds,
        m1Results.Select(r => (r.Tool.Name, r.Score)).ToList(),
        m2Results.Select(r => (r.Tool.Name, r.Score)).ToList()));
}

// ── Final Summary Table ──────────────────────────────────────────────
AnsiConsole.Write(new Rule("[bold green]📊 Final Summary — Mode 1 vs Mode 2[/]").RuleStyle("green"));
AnsiConsole.WriteLine();

var summaryTable = new Table()
    .Border(TableBorder.HeavyHead)
    .BorderColor(Color.Green)
    .Title("[bold green]LLM Distillation Results (120+ tools, top-5 selection)[/]")
    .AddColumn(new TableColumn("[bold]#[/]").Centered())
    .AddColumn(new TableColumn("[bold]Scenario[/]").Width(28))
    .AddColumn(new TableColumn("[bold]Words[/]").RightAligned())
    .AddColumn(new TableColumn("[bold cyan]Mode 1 Hits[/]").Centered())
    .AddColumn(new TableColumn("[bold green]Mode 2 Hits[/]").Centered())
    .AddColumn(new TableColumn("[bold]Distill[/]").RightAligned())
    .AddColumn(new TableColumn("[bold]Winner[/]").Centered());

for (int i = 0; i < results.Count; i++)
{
    var r = results[i];
    var winColor = r.Winner == "Mode 2" ? "green" : r.Winner == "Mode 1" ? "red" : "yellow";
    var winEmoji = r.Winner == "Mode 2" ? "🏆" : r.Winner == "Mode 1" ? "⚠️" : "🤝";

    summaryTable.AddRow(
        $"{i + 1}",
        Markup.Escape(r.Name),
        $"[dim]{r.WordCount}[/]",
        $"[cyan]{r.Mode1Hits}/{topK}[/]",
        $"[green]{r.Mode2Hits}/{topK}[/]",
        $"[dim]{r.DistillMs}ms[/]",
        $"[{winColor} bold]{winEmoji} {r.Winner}[/]");
}

AnsiConsole.Write(summaryTable);
AnsiConsole.WriteLine();

// ── Win/Loss/Tie Stats ───────────────────────────────────────────────
var mode2Wins = results.Count(r => r.Winner == "Mode 2");
var mode1Wins = results.Count(r => r.Winner == "Mode 1");
var ties = results.Count(r => r.Winner == "Tie");
var totalM1Hits = results.Sum(r => r.Mode1Hits);
var totalM2Hits = results.Sum(r => r.Mode2Hits);
var maxPossibleHits = results.Count * topK;

var statsTable = new Table()
    .Border(TableBorder.DoubleEdge)
    .BorderColor(Color.Yellow)
    .Title("[bold yellow]🏆 Overall Scoreboard[/]")
    .AddColumn(new TableColumn("[bold]Metric[/]").Width(30))
    .AddColumn(new TableColumn("[bold]Mode 1 (Embeddings)[/]").Centered())
    .AddColumn(new TableColumn("[bold]Mode 2 (LLM Distilled)[/]").Centered());

statsTable.AddRow(
    "[bold]Scenarios Won[/]",
    $"[cyan]{mode1Wins}[/]",
    $"[green bold]{mode2Wins}[/]");
statsTable.AddRow(
    "[bold]Ties[/]",
    $"[yellow]{ties}[/]",
    $"[yellow]{ties}[/]");
statsTable.AddRow(
    "[bold]Total Relevant Hits[/]",
    $"[cyan]{totalM1Hits} / {maxPossibleHits}[/]",
    $"[green bold]{totalM2Hits} / {maxPossibleHits}[/]");
statsTable.AddRow(
    "[bold]Hit Rate[/]",
    $"[cyan]{(double)totalM1Hits / maxPossibleHits * 100:F1}%[/]",
    $"[green bold]{(double)totalM2Hits / maxPossibleHits * 100:F1}%[/]");

AnsiConsole.Write(statsTable);
AnsiConsole.WriteLine();

// ── Key Takeaway Panel ───────────────────────────────────────────────
AnsiConsole.Write(new Panel(
    $"[bold]With {tools.Length} tools and verbose, paragraph-length prompts:[/]\n\n" +
    $"  🏆 Mode 2 won [green bold]{mode2Wins}[/] / {results.Count} scenarios\n" +
    $"  📈 Mode 2 hit rate: [green bold]{(double)totalM2Hits / maxPossibleHits * 100:F1}%[/]  vs  " +
    $"Mode 1: [cyan]{(double)totalM1Hits / maxPossibleHits * 100:F1}%[/]\n\n" +
    "[dim]When users write long, rambling prompts (as they naturally do),\n" +
    "raw embedding search gets diluted by noise words and tangents.\n" +
    "Mode 2 uses a local LLM to extract the core intent first,\n" +
    "then searches with a clean, focused query — better tool selection.[/]\n\n" +
    "[bold]API:[/]\n" +
    "  [cyan]// Mode 1 — embeddings only, no LLM needed[/]\n" +
    "  var results = await ToolRouter.SearchAsync(prompt, tools);\n\n" +
    "  [green]// Mode 2 — LLM distillation, zero-setup local model[/]\n" +
    "  var results = await ToolRouter.SearchUsingLLMAsync(prompt, tools);")
    .Header("[bold yellow]💡 Key Takeaway[/]")
    .Border(BoxBorder.Double)
    .BorderColor(Color.Yellow)
    .Padding(1, 1));

AnsiConsole.WriteLine();
AnsiConsole.Write(new Rule("[bold green]✅ Demo Complete[/]").RuleStyle("green"));
AnsiConsole.MarkupLine("[dim]💡 For production use with many calls, use the instance API (ToolRouter.CreateAsync) to avoid re-indexing tools each call.[/]");
AnsiConsole.WriteLine();

// Clean up shared resources used by the static API
await ToolRouter.ResetSharedResourcesAsync();

return;

// ── Scenario Definitions ─────────────────────────────────────────────
static (string Name, string Prompt, string[] Expected)[] BuildScenarios() =>
[
    (
        "Infrastructure Chaos",
        "So yesterday I was in a meeting with the VP of Engineering and we were discussing " +
        "the quarterly infrastructure review, and she mentioned that our Kubernetes clusters " +
        "have been showing some weird behavior lately — pods restarting randomly, memory " +
        "usage spiking at odd hours. I also noticed that our main PostgreSQL database has " +
        "been running slower than usual, especially the customer analytics queries that feed " +
        "into the monthly report. While I was investigating, I found some old SQL queries " +
        "that haven't been optimized in years. Oh and speaking of databases, the security " +
        "team flagged that we need to rotate all our database credentials by end of week, " +
        "and I should probably run a full vulnerability scan on the production environment " +
        "while I'm at it. The DevOps lead also mentioned something about the Terraform state " +
        "file being out of sync, so I need to run a plan to see what's drifted.",
        ["check_service_health", "query_database", "rotate_credentials", "scan_vulnerabilities", "kubectl_apply", "terraform_plan"]
    ),
    (
        "Marketing Multi-Hat Day",
        "OK so I just got out of the most chaotic Monday morning standup ever. The marketing " +
        "director is freaking out because apparently the competitor launched a new product over " +
        "the weekend and she wants me to do like five things at once. First she needs someone " +
        "to scrape the competitor's pricing page — you know, the one that keeps changing their " +
        "product tiers every month. Then she wants a sentiment analysis run on all the social " +
        "media mentions from the last 48 hours to see how customers are reacting to the launch. " +
        "Oh and she also asked if I could put together a monitoring dashboard showing our web " +
        "traffic metrics compared to last quarter, because the board meeting is Thursday and " +
        "she needs pretty charts. And somewhere in there she mentioned wanting to send a mass " +
        "email campaign to our subscriber list but I'm not even sure we have the email list " +
        "ready, I think the CSV is on someone's shared drive somewhere and I need to find it " +
        "and import it first before we can do anything with it.",
        ["scrape_webpage", "analyze_sentiment", "create_dashboard", "get_metrics", "send_email", "search_files", "import_csv"]
    ),
    (
        "Developer Sprint Panic",
        "Right, so sprint review is in two days and I'm looking at my Jira board thinking how " +
        "did I let this happen again. I've got three PRs that need reviewing, the unit tests " +
        "for the payment module are failing on CI and nobody knows why — the pipeline just shows " +
        "red and the error logs are a mess. I need to check the pipeline status and figure out " +
        "what's going on, then probably deploy the hotfix for that authentication bug to staging " +
        "before the product owner sees it. But first I should probably check if the SSL certificates " +
        "haven't expired because we got that warning email last week and nobody followed up on it. " +
        "Also the junior dev on my team just pinged me asking for help understanding some legacy " +
        "Python code that's throwing weird errors, and I promised the PM I'd generate a progress " +
        "report by end of day showing what we've accomplished this sprint. The DevOps team also " +
        "mentioned that our Docker images are getting bloated — the base image alone is like 2GB " +
        "now — and we should probably rebuild them. I haven't even looked at the error logs from " +
        "last night's batch job failure yet, ugh.",
        ["get_pipeline_status", "deploy_to_environment", "check_ssl_certificate", "explain_code", "generate_report", "docker_build", "query_logs", "get_error_report"]
    ),
    (
        "Data Science Deep Dive",
        "I've been thinking about this all weekend and I think there's a really interesting " +
        "opportunity with our customer data that nobody's explored yet. We have something like " +
        "fifty thousand customer records with behavioral data — page views, purchase history, " +
        "support tickets, NPS scores, the whole nine yards. What if we clustered them into " +
        "meaningful segments using unsupervised learning? I bet we could find patterns that " +
        "the marketing team would absolutely kill for. But first I'd need to clean the data " +
        "and probably export it from the production database into something workable, then " +
        "maybe run some descriptive statistics to understand the distributions and identify " +
        "outliers. The tricky part is that some of the data is in different formats — some " +
        "sitting in the main database, some in CSV files that the analytics team exported " +
        "last month and dumped on the shared drive. I'd also need to generate embeddings for " +
        "the text fields like support ticket descriptions to do any meaningful NLP analysis. " +
        "And while we're at it, maybe we should train a classification model to predict churn " +
        "risk — the VP of Customer Success has been asking about that for months. Oh, and the " +
        "sentiment analysis on our NPS survey free-text responses is still pending too.",
        ["query_database", "export_to_csv", "calculate_statistics", "import_csv", "generate_embeddings", "cluster_data", "train_model", "classify_text", "analyze_sentiment"]
    ),
    (
        "Global Team Coordinator",
        "Being a remote team lead across four timezones is honestly exhausting some days, but " +
        "today takes the cake. So I need to figure out when to schedule our quarterly architecture " +
        "review meeting — there's engineers in Tokyo, London, San Francisco, and São Paulo, and " +
        "half of them have weird schedule constraints that nobody told me about until this morning. " +
        "Before I send out any calendar invites I should probably check everyone's availability " +
        "first so I don't accidentally book over someone's focus time again like last month. " +
        "Actually, I also need to translate the meeting agenda into Japanese because Tanaka-san " +
        "mentioned she strongly prefers reading technical documents in her native language, which " +
        "is totally fair. Oh and I need to send a Slack message to the London team about the API " +
        "freeze starting next week — they keep missing announcements — and then a separate Teams " +
        "message to the São Paulo office about the updated code review process that went into " +
        "effect yesterday. Come to think of it, I should also set up a recurring daily standup " +
        "that actually works across all these timezones without making anyone join at midnight, " +
        "and create a shared document with the meeting notes template so everyone's on the same page.",
        ["check_availability", "get_timezone_info", "create_calendar_event", "translate_text", "send_slack_message", "send_teams_message", "set_recurring_event", "write_file"]
    ),
    (
        "Security Incident Response",
        "OK this is NOT a drill, and I'm honestly trying not to panic right now. We just got " +
        "an automated alert that there might be unauthorized access attempts on our production " +
        "systems. The monitoring dashboard is showing a massive spike in failed authentication " +
        "attempts starting around 3am, and there are some suspicious API calls from IP addresses " +
        "that don't match any of our known offices or VPN ranges. I need to immediately check " +
        "the access logs and audit trail to understand the scope of what happened, then audit " +
        "who currently has permissions to what — because honestly I don't think our IAM policies " +
        "have been reviewed in six months. We should probably generate fresh API keys for all " +
        "the potentially compromised services and rotate the database credentials as a precaution. " +
        "The CISO wants a full incident report by 5pm with a timeline and impact assessment. " +
        "We also need to verify that our encryption at rest is actually working properly and " +
        "that the SSL certs are still valid. While we're at it, we should hash all the sensitive " +
        "customer data that's apparently sitting in near-plain-text in that legacy system nobody " +
        "wants to touch, and update the JWT token configuration to use shorter expiration windows. " +
        "And someone needs to run a comprehensive vulnerability scan on everything, yesterday.",
        ["query_logs", "audit_permissions", "generate_api_key", "rotate_credentials", "generate_report", "check_ssl_certificate", "encrypt_data", "hash_data", "generate_jwt_token", "scan_vulnerabilities"]
    ),
    (
        "End-of-Quarter Reporting",
        "It's the last day of Q3 and my boss just dropped a bomb on me — apparently the entire " +
        "executive leadership team wants a comprehensive quarterly business review report on their " +
        "desks by tomorrow morning, and somehow this is now my problem. I need to pull the revenue " +
        "data from the main analytics database, compute quarter-over-quarter growth rates, run " +
        "some statistical analysis on customer acquisition costs, and compare everything with Q2 " +
        "numbers to show trends. The CFO specifically asked for compound interest projections on " +
        "our company's investment portfolio going out 5 years, which means I need to dig up the " +
        "portfolio details from somewhere. The data is, predictably, scattered across multiple " +
        "sources: some in the database, some in Excel spreadsheets that the finance team maintains " +
        "on SharePoint, and the web traffic engagement numbers apparently need to be scraped from " +
        "our analytics tool's export page because the API is broken again. I also need to do unit " +
        "conversions for the international sales figures — they're reported in euros, pounds, and " +
        "yen but the final report needs everything normalized to USD. And then somehow I need to " +
        "make all of this look presentable with proper charts and formatted tables in a generated " +
        "report, plus export key datasets to CSV for the department leads to do their own analysis.",
        ["query_database", "calculate_statistics", "calculate_compound_interest", "import_csv", "scrape_webpage", "convert_units", "generate_report", "export_to_csv"]
    ),
    (
        "ML Pipeline Debug Session",
        "Alright, so our ML pipeline has been acting up again and I've spent the entire morning " +
        "pulling my hair out trying to figure out what went wrong. The sentiment analysis model " +
        "we deployed to production last Thursday is giving completely nonsensical predictions — " +
        "like it's classifying obviously glowing five-star reviews as negative sentiment, and " +
        "angry complaint emails as positive, which obviously makes zero sense and the customer " +
        "success team is furious. I need to run some targeted inference tests with known inputs " +
        "to verify the model outputs match expected values, then probably evaluate the full model " +
        "on our holdout test dataset to check if the accuracy metrics have degraded since we last " +
        "benchmarked it. I'm starting to suspect we need to either retrain the model from scratch " +
        "with more recent labeled data, or maybe fine-tune the base language model with our " +
        "domain-specific customer feedback corpus. The training data might be contaminated — I " +
        "should check if the CSV we used for training had any systematic labeling errors or " +
        "duplicates. Oh and the data scientist on my team wants me to run a clustering analysis " +
        "on all the misclassified examples to see if there's a pattern, like maybe the model " +
        "fails on a specific product category or customer demographic. The model runs as a Docker " +
        "container, so I should also check if the container is actually healthy and pull the " +
        "prediction logs to look for out-of-memory errors or anything obvious.",
        ["run_inference", "evaluate_model", "analyze_sentiment", "train_model", "finetune_llm", "import_csv", "cluster_data", "check_service_health", "query_logs", "docker_build"]
    ),
    (
        "New Developer Onboarding",
        "So we have three brand new engineers starting next Monday and I volunteered — well, more " +
        "like was voluntold — to have everything ready for their first day. The onboarding checklist " +
        "is honestly kind of insane. I need to send each of them a welcome email with login " +
        "credentials and links to our internal documentation, then create calendar events for all " +
        "the orientation sessions: the HR intro on Monday morning, the engineering culture talk on " +
        "Tuesday, the architecture deep-dive on Wednesday, and the first sprint planning on Thursday. " +
        "Each new hire also needs a Slack welcome message posted in the team channel introducing " +
        "them and linking to the team handbook. On the technical side, I need to generate development " +
        "API keys for each person with appropriate scoping so they can't accidentally hit production, " +
        "and properly audit and configure their system permissions — last time we onboarded someone " +
        "they accidentally got admin access to the production database, which was a whole thing. " +
        "I should probably write an updated getting-started guide since the current one still " +
        "references our old CI system that we deprecated six months ago. Oh and I need to set up " +
        "recurring weekly one-on-one meetings between each new hire and their assigned mentor, " +
        "export the current org chart from the database so they can see who's who on each team, " +
        "and make sure the development Kubernetes cluster pods are actually running properly.",
        ["send_email", "create_calendar_event", "send_slack_message", "generate_api_key", "audit_permissions", "write_file", "set_recurring_event", "export_to_csv", "kubectl_apply", "check_service_health"]
    ),
    (
        "Weather-Dependent Event",
        "My team somehow elected me to organize the annual company offsite retreat and I'm quickly " +
        "realizing this is basically a full-time job on top of my actual full-time job. The event " +
        "is planned for a coastal resort location next month, so weather is absolutely critical to " +
        "get right — I need to pull the extended forecast for that area to check if we're looking " +
        "at rain, because last year it poured and we had zero backup plans. I also need to check " +
        "the UV index because half the engineering team has never seen the sun and the other half " +
        "got second-degree sunburns last year, plus the tide schedule since we've got kayaking and " +
        "beach volleyball on the agenda. Then there's the whole communications piece — I need to " +
        "send a detailed email blast to all 200 employees with the finalized agenda, logistics " +
        "info, and packing suggestions. We should set up a dedicated Teams channel for real-time " +
        "event coordination and questions. Oh and we have employees flying in from Mexico City " +
        "and Shanghai, so I need to translate the safety briefing and event schedule into Spanish " +
        "and Mandarin Chinese. The finance department wants me to calculate the per-person cost " +
        "breakdown and convert the expenses from the resort's local currency into USD, EUR, and " +
        "GBP for the international attendees' expense reports. I should probably also create a " +
        "shared logistics document with room assignments and travel details.",
        ["get_weather_forecast", "get_uv_index", "get_tide_info", "send_email", "send_teams_message", "translate_text", "calculate_expression", "convert_units", "write_file"]
    ),
    (
        "Content Pipeline Emergency",
        "Everything is on fire today in the content team and somehow I'm the one putting out the " +
        "flames even though I'm supposed to be an engineer. The flagship blog post that was supposed " +
        "to go live at 9am this morning has a bunch of embarrassing grammar and spelling errors " +
        "that somehow made it through three rounds of review, and the social media team is sitting " +
        "there refreshing their dashboards waiting for me to green-light the publish. I need to " +
        "run a thorough grammar and spell check on the draft immediately, then also create a " +
        "condensed summary of the post for the Twitter thread and a slightly longer version for " +
        "LinkedIn. The SEO team has been on my case about keyword optimization, so I need to " +
        "extract the primary keywords and key phrases from our last five published articles to " +
        "make sure we're targeting the right search terms. While I'm digging through the content " +
        "archive, I noticed that some of our older articles have embedded URLs that might be dead " +
        "links — I should check those before Google penalizes us for it. Oh and we're expanding " +
        "into three new European markets next month, so the localization team needs translations " +
        "of all our product descriptions into French, German, and Portuguese by end of week. The " +
        "CMO also wants a sentiment analysis on brand mentions from the past month, and someone " +
        "suggested we scrape the top competitor blogs to see what content themes are trending in " +
        "our space. This is honestly too much work for one person but here we are.",
        ["check_grammar", "summarize_text", "extract_keywords", "check_url_status", "translate_text", "analyze_sentiment", "scrape_webpage"]
    ),
    (
        "Late-Night Production Outage",
        "It's 2am and I just got woken up by PagerDuty because production is apparently on fire " +
        "and the on-call engineer before me somehow slept through their alerts for three hours, " +
        "which is a whole separate conversation we need to have on Monday. Users are flooding " +
        "our support channels complaining about 500 errors, timeout pages, and lost transactions. " +
        "First thing I absolutely need to do is check the service health across all our endpoints " +
        "to see exactly what's down and what's still limping along. Then I need to pull up the " +
        "error logs and application traces to understand what actually broke — the monitoring " +
        "dashboard is showing that our p99 latency spiked from 200ms to 30 seconds right around " +
        "midnight, which happens to be exactly when the database team was supposedly running a " +
        "schema migration. I bet that migration is related, so I need to check if it completed " +
        "successfully or if it's stuck halfway through and locking tables. If the migration is " +
        "the culprit, we might need to roll back the entire deployment to the previous stable " +
        "version while we figure out what went wrong. I also need to trace a few specific failed " +
        "requests end-to-end through our microservice call chain to pinpoint where things are " +
        "breaking down. After that, I need to set up proper alerting so this kind of three-hour " +
        "silent failure never happens again. The incident commander wants a preliminary post-mortem " +
        "report drafted by morning, and I should send a status update to the engineering team via " +
        "email and Slack so nobody walks into a surprise tomorrow.",
        ["check_service_health", "get_error_report", "query_logs", "get_metrics", "run_migration", "rollback_deployment", "trace_request", "set_alert_rule", "generate_report", "send_email", "send_slack_message"]
    ),
];

// ── Tool Definitions: 120+ tools across 12 domains ──────────────────
// (Same tools as TokenComparisonMax for direct comparison)
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
    string Name,
    string Prompt,
    int WordCount,
    string DistilledPrompt,
    int Mode1Hits,
    int Mode2Hits,
    string Winner,
    long Mode1Ms,
    long Mode2Ms,
    long DistillMs,
    List<(string Name, float Score)> Mode1Tools,
    List<(string Name, float Score)> Mode2Tools);
