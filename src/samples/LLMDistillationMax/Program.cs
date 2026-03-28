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
using Spectre.Console;

// ── Banner ───────────────────────────────────────────────────────────
AnsiConsole.Write(new FigletText("LLM Distill Max").Color(Color.Cyan1));
AnsiConsole.Write(new Rule("[bold cyan]🧠 LLM Distillation at Scale — 120+ Tools, 12 Long Prompts[/]").RuleStyle("cyan"));
AnsiConsole.WriteLine();
AnsiConsole.MarkupLine("[dim]Mode 1 (embeddings-only) vs Mode 2 (LLM-distilled) — who picks better tools?[/]");
AnsiConsole.MarkupLine("[dim]No Azure required. Everything runs locally.[/]");
AnsiConsole.WriteLine();

// ── 120+ MCP Tool Definitions across 12 domains ─────────────────────
var tools = ToolDefinitions.Build();
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

// Show model metadata (v0.7.1: ConfigMaxSequenceLength = raw config, MaxSequenceLength = effective runtime limit)
var modelInfo = chatClient.ModelInfo;
if (modelInfo is not null)
{
    AnsiConsole.MarkupLine($"[dim]  Model: {Markup.Escape(modelInfo.ModelName ?? "unknown")}[/]");
    AnsiConsole.MarkupLine($"[dim]  Config context window: {modelInfo.ConfigMaxSequenceLength} tokens[/]");
    AnsiConsole.MarkupLine($"[dim]  Effective context window: {modelInfo.MaxSequenceLength} tokens[/]");
    if (modelInfo.VocabSize.HasValue)
        AnsiConsole.MarkupLine($"[dim]  Vocab size: {modelInfo.VocabSize.Value:N0}[/]");
}

// ── Define 12 scenarios with LONG, messy, real-world prompts ─────────
var scenarios = Scenarios.Build();
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
