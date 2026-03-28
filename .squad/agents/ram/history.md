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
- Updated README.md for ElBruno.LocalLLMs v0.6.1 upgrade (ModelInfo metadata exposure):
   - Line 120: Added note about auto-detection of model context window via ModelInfo.MaxSequenceLength
   - Line 360: Enhanced Input Validation section explaining auto-detection in Mode 2 API ensures prompts fit model's capacity
   - Lines 176–188: Added new "Model Metadata (v0.6.1+)" subsection with runtime inspection example and note that SearchUsingLLMAsync uses metadata automatically
   - Checked sample READMEs (LLMDistillationDemo, LLMDistillationMax, McpToolRouting, TokenComparison, TokenComparisonMax, etc.) — none mention 0.5.0 or MaxPromptLength, no changes needed
- Updated README.md for ElBruno.LocalLLMs v0.7.1 upgrade (metadata reliability fix):
   - Line 176: Updated section title from "v0.6.1+" to "v0.7.1+"
   - Lines 178–188: Enhanced Model Metadata section with dual-property explanation: `MaxSequenceLength` returns effective runtime limit (e.g., 128 for Phi-3.5 mini), `ConfigMaxSequenceLength` preserves raw config value (e.g., 131072)
   - Line 362: Updated Input Validation section to note v0.7.1+ ensures metadata accuracy for reliable context window management via dual properties
   - Removed warning about metadata potentially differing from runtime limits (v0.7.1 fixes this)
   - No changes needed to docs/ (image-prompts.md not version-specific) or samples — all updates confined to main README.md
- Updated documentation for GPU/DirectML support (GPU acceleration initiative):
   - Main README.md: Added new "GPU Acceleration (Optional)" subsection under Mode 2 (lines 190–202)
     - Explains ElBruno.LocalLLMs defaults to ExecutionProvider.Auto (GPU first, CPU fallback)
     - Added decision table with 3 hardware options: DirectML (Windows any GPU), CUDA (NVIDIA), CPU-only
     - Installation instructions use `dotnet add package` format per conventions (no XML)
     - Note: "Do not mix CPU and GPU variants — add exactly one"
     - Performance claim: "2–5x faster" with GPU
   - Mode 1/Mode 2 comparison table updated: Added GPU performance note (~20–100ms with GPU vs ~50–200ms on CPU)
   - Updated sample READMEs (LLMDistillationDemo, LLMDistillationMax):
     - Added optional GPU acceleration mention in Prerequisites/Requirements sections
     - Links to DirectML package and notes "2–5x faster inference"
   - Updated Program.cs files: Added inline hints about GPU options during model loading
     - LLMDistillationDemo: Added note in console output suggesting DirectML for Windows GPU users
     - LLMDistillationMax: Updated loading status message to mention DirectML option
   - Key decision: GPU acceleration is OPTIONAL — library works CPU-only without extra packages
   - No .csproj changes made (per Tron's responsibility for package updates)
   - All updates maintain existing content; GPU info added alongside current documentation

### Orchestration Log & Session History (2026-03-28T16:49:19Z)
- **Timestamp:** 2026-03-28T16:49:19Z
- **Commit:** 4eac15e (DirectML GPU acceleration)
- **Team:** Tron (Core Dev) + Ram (DevRel) + Coordinator (30 new unit tests)
- **Build status:** Clean build with 115 unit tests passing
- **Decisions merged:** DirectML GPU Runtime (Decision §10) + ModelInfo Auto-Detection Revert (Decision §11) added to `.squad/decisions.md`
- **Session artifacts:**
  - `.squad/log/2026-03-28T16-49-19-directml-gpu.md` — brief session log
  - `.squad/orchestration-log/2026-03-28T16-49-19-tron.md` — detailed Tron orchestration log
  - `.squad/orchestration-log/2026-03-28T16-49-19-ram.md` — detailed Ram orchestration log

### DirectML Revert Coordination (2026-03-28T16:54:12Z)
- **Context:** DirectML GPU acceleration (Decision §10) was reverted due to hard error on Bruno's machine when DirectML is unsupported
- **Root cause:** ElBruno.LocalLLMs `ExecutionProvider.Auto` throws hard error instead of gracefully falling back to CPU
- **Upstream issue:** Coordinator filed elbruno/ElBruno.LocalLLMs#7 to fix fallback behavior
- **Documentation updates by Tron:** CPU as default, GPU as optional in all README and Program.cs
- **Decision §12 merged:** "Revert DirectML GPU Runtime to CPU-Only" documents decision, rationale, and future action plan
- **Impact:** Samples now "just work" on all hardware; GPU acceleration preserved as opt-in with clear guidance
- **Session artifacts:**
  - `.squad/log/2026-03-28T16-54-12-directml-revert.md` — session log
  - `.squad/orchestration-log/2026-03-28T16-54-12-tron-revert-directml.md` — Tron orchestration log

### MaxPromptLength Default Fix Documentation (2026-03-28T17:00:00Z)
- **Bug fixed by Tron:** Mode 1 (embeddings-only) and Mode 2 (LLM-distilled) produced identical results due to mismatched MaxPromptLength defaults: \ToolRouterOptions\ defaulted to 4096 while \PromptDistillerOptions\ defaulted to 300. Local ONNX models silently failed on long prompts, falling back to original prompt (Mode 2 = Mode 1).
- **Root cause:** Both options now default to 300 characters, optimized for local ONNX models with constrained context windows.
- **Documentation updates by Ram:**
  - Main README.md: Updated "Input Validation" section (lines 374–382) to reflect new unified 300-character default for both \PromptDistillerOptions\ and \ToolRouterOptions\
  - Added code example showing how to increase MaxPromptLength for cloud LLMs: \
ew ToolRouterOptions { MaxPromptLength = 2000 }\
  - Clarified that Mode 2 now properly distills with smaller context constraints, distinguishing it from Mode 1
  - Verified LLMDistillationDemo and LLMDistillationMax sample READMEs — no MaxPromptLength mentions, no updates required
- **Testing:** Solution builds clean, documentation is now accurate and reflects the fix

### MaxPromptLength Default Alignment Documentation (2026-03-28T17:23:35Z)
**Task:** Update README.md with MaxPromptLength=300 default and cloud LLM override guidance following Tron's fix.

**Changes Made:**
- **Input Validation Section:** Updated to explicitly state MaxPromptLength defaults to 300 (aligned across ToolRouterOptions and PromptDistillerOptions)
- **Cloud LLM Override Guidance:** Added clear example showing how to increase MaxPromptLength for Azure OpenAI or other cloud LLMs with larger context windows:
  ```csharp
  var options = new ToolRouterOptions { MaxPromptLength = 4096 };
  var tools = await ToolRouter.SearchUsingLLMAsync(prompt, tools, options, chatClient);
  ```
- **Context Window Explanation:** Documented the rationale: 300 chars is safe for local ONNX models with limited context, cloud LLMs can handle 4096+ safely
- **Sample Documentation:** Verified all sample READMEs — none mention MaxPromptLength specifically, no changes needed to samples

**Related Work:**
- Tron: Fixed `ToolRouterOptions.MaxPromptLength` default from 4096 to 300 (core fix)
- Yori: Added 5 regression tests to prevent recurrence
- Commit: Part of coordinated MaxPromptLength fix sprint

**Results:**
- ✅ README.md updated with correct defaults and override pattern
- ✅ Clear guidance for both local ONNX and cloud LLM scenarios
- ✅ Solution builds clean, no warnings
- **Decision merged:** Decision 3.7 (Align defaults) and 3.8 (Regression tests) added to `.squad/decisions.md`
- **Session artifacts:**
  - `.squad/orchestration-log/2026-03-28T17-23-35-ram-readme-update.md` — orchestration log
  - `.squad/log/2026-03-28T17-23-35-maxpromptlength-fix-session.md` — comprehensive session log (shared with Tron, Yori)
