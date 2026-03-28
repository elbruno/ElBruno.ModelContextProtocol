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
- Updated solution file ElBruno.ModelContextProtocol.slnx to include McpToolRouting project in /src/samples/ folder
