# Decision: Spectre.Console for Sample UX

**Date:** 2025-03-26  
**Agent:** Tron  
**Status:** Implemented

## Context

The TokenComparisonMax sample needed rich terminal output to showcase extreme token savings across 120+ tools and 12 scenarios. Standard `Console.WriteLine` with manual box-drawing characters (as used in TokenComparison) would be fragile and hard to maintain at this scale.

## Decision

Use `Spectre.Console` (v0.49.1) for terminal UX in the TokenComparisonMax sample.

## Rationale

- **Live tables:** `AnsiConsole.Live()` enables real-time row updates as each scenario completes — impossible with plain Console output
- **Formatted tables:** `Table` class with borders, colors, and alignment replaces manual padding/box-drawing
- **Rich markup:** Colors, emojis, and styles via `[bold cyan]` syntax make output scannable
- **FigletText:** Eye-catching banner for demo scenarios
- **Maintained library:** 10k+ GitHub stars, MIT license, .NET Standard 2.0 compatible

## Impact

- Only affects the TokenComparisonMax sample (not the library or other samples)
- Adds a single NuGet dependency (`Spectre.Console 0.49.1`) to one sample project
- Could be adopted in future samples if the team prefers this pattern
