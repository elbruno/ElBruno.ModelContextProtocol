# Ram — History

## Project Context

- **Project:** ElBruno.MCPToolRouter
- **User:** Bruno Capuano
- **Stack:** .NET (C#), NuGet library, xUnit, ElBruno.LocalEmbeddings
- **Description:** .NET library that ingests MCP tool definitions, embeds them into a local vector store, and returns top-K most relevant tools via cosine similarity.

## Learnings

- Created comprehensive README.md following exact ElBruno .NET conventions: badges, tagline, packages table, installation, quick start example, and author/acknowledgments sections
- Established MIT LICENSE with "Copyright (c) 2026 Bruno Capuano"
- Set up CI Build workflow (build.yml) with ubuntu-latest runner, SDK 8.0.x, multi-target compilation strategy
- Set up Publish workflow (publish.yml) with OIDC trusted publishing (no API key secrets), version extraction from tags/inputs, and per-project packing for MCPToolRouter
- images/ directory already existed; nuget_logo.png to be added manually by Bruno
- All workflows use solution-level operations against ElBruno.ModelContextProtocol.slnx as per conventions
- Added "How It Works" section explaining MCPToolRouter's semantic search process: ingestion, embedding, query embedding, similarity search, and tool selection
- Added comprehensive "Samples" section with overview table and three sample applications: BasicUsage (no Azure), TokenComparison (marquee sample showing ~72% token savings), and FilteredFunctionCalling (end-to-end pattern)
- Used dotnet user-secrets CLI format (not XML PackageReference) for Azure OpenAI configuration instructions per conventions
- Positioned new sections between "Quick Start" and "Building from Source" to maintain README structure and flow
- Created detailed `docs/image-prompts.md` with 4 image generation prompts (NuGet logo, YouTube thumbnail, blog header, social card) including DALL-E prompts, color palettes, technical specs, visual guidelines
- Updated README.md samples table and section to include TokenComparisonMax (120+ tools scenario with Spectre.Console UX)
- Changed samples count from "Three sample applications" to "Four sample applications" in README intro text
- Created McpToolRouting sample demonstrating LLM-powered tool routing with local inference (no cloud APIs)
  - Program.cs showcases 3 scenarios: complex multi-step prompt distillation, simple one-shot routing, token savings analysis
  - 28 realistic MCP tools across 7 domains (weather, email, calendar, files, web, math, code) for compelling demo
  - Uses ElBruno.LocalLLMs with Qwen 2.5 0.5B ONNX model for local LLM inference
  - README explains distillation pipeline, prerequisites (~1GB for model cache), and usage patterns
- Added ToolRouter section to main README showing 4 usage patterns: simple routing, LLM distillation, one-shot static method, DI registration
- Updated samples table from 6 to 7 samples, positioned McpToolRouting at #2 (after BasicUsage)
- Updated McpToolRouting sample and README.md to showcase new simplified static API:
  - New static one-liners: `ToolRouter.SearchAsync()` (Mode 1, embeddings-only) and `ToolRouter.SearchUsingLLMAsync()` (Mode 2, LLM-distilled)
  - Deleted old verbose static method `ToolRouter.RouteAsync()`
  - Restructured README to put simple API first: Quick Start now shows `SearchAsync` one-liner, two mode sections highlight static methods as primary, instance API positioned as "Advanced: Reusable Instance"
  - Updated comparison table with both static and instance API columns
  - Updated Program.cs to use one-liner methods instead of `await using` router patterns
  - Program.cs now has 4 clear patterns: Mode 1 one-liner, Mode 2 one-liner, and two reusable instance patterns
  - Added note about instance API for performance-critical scenarios (servers, multi-turn agents)
- Restructured README.md for improved developer experience:
  - Replaced verbose "Quick Start" section with compact TL;DR hero block showing both Mode 1 and Mode 2 API side-by-side (~2 lines)
  - Surfaced value proposition early: moved "reduce token costs by 70–85%" from deep "How It Works" into opening tagline for instant impact clarity
  - Trimmed per-sample descriptions from 200+ words (with repeated Azure setup blocks) to 1–2 sentence summaries
  - Consolidated Azure OpenAI setup from 5 repeated blocks into single unified section that applies to all requiring-Azure samples
  - Result: README reduced by 112 lines (137 deletions, 25 insertions) while maintaining all valuable information
  - Kept unchanged: Mode 1/2 code examples, pipeline diagrams, comparison table, Advanced Features, Building from Source, License, Author, Acknowledgments sections
- Created LLMDistillationDemo sample (`src/samples/LLMDistillationDemo/`) showcasing Mode 1 vs Mode 2 comparison
  - 30 tools across 8 domains (Weather, Email, Calendar, Files, Web, Math/Data, Code, DevOps)
  - 7 verbose real-world prompt scenarios: trip planning, kitchen-sink email, developer stream-of-consciousness, vague meeting, research ramble, multi-domain chaos, procrastinator's todo list
  - Uses shared `ToolIndex` + `LocalChatClient` instances across all scenarios for performance
  - Calls `PromptDistiller.DistillIntentAsync` directly to display the distilled intent before searching
  - Mode 1 searches with raw verbose prompt; Mode 2 searches with LLM-distilled intent — same embedding index, different query quality
  - Console.WriteLine output only (no Spectre.Console), simple and clean formatting
  - Added to solution file, README samples table (8 samples now), and wrote sample README.md explaining the problem, scenarios, and API usage
- Added two new sections to README before "Samples":
  - ⚡ **Performance Guide** (lines 291–325): Explains Static vs Instance API trade-offs, highlights new shared singletons behavior for static API (much faster than before), describes QueryCacheSize for embedding caching, quick recommendation table for common scenarios
  - 🔒 **Security Considerations** (lines 327–353): Covers prompt injection risks in Mode 2, model download security with EmbeddingModelCacheDirectory option, input validation with MaxPromptLength, and NuGet lock file supply chain integrity
  - Both sections are concise (3–4 paragraphs each) with practical code examples matching existing README tone
  - LLMDistillationDemo already listed in samples table (no changes needed)
  - Solution builds successfully in Release mode (no regressions)
- Created LLMDistillationMax sample (`src/samples/LLMDistillationMax/`) — Mode 2 at TokenComparisonMax scale
  - 120+ tools across 12 domains (same as TokenComparisonMax for direct comparison)
  - 12 paragraph-length scenarios (100–200+ words each): stream-of-consciousness, multi-intent, noisy real-world prompts
  - Uses static one-liner API: `ToolRouter.SearchAsync()` (Mode 1) and `ToolRouter.SearchUsingLLMAsync()` (Mode 2)
  - Spectre.Console rich UX: FigletText banner, per-scenario comparison tables (side-by-side Mode 1 vs Mode 2), final summary table, overall scoreboard with win/loss/tie stats and hit rate
  - No Azure required — runs 100% locally with auto-downloaded LLM and embedding model
  - Shows distilled prompt for each scenario via `PromptDistiller.DistillIntentAsync`
  - Cleans up shared resources via `ToolRouter.ResetSharedResourcesAsync()` at exit
  - Added to solution file, README samples table (9 samples now), and wrote sample README.md
  - Full solution builds clean: 0 warnings, 0 errors
