# Decision: Microsoft Agent Framework Integration Pattern

**Date:** 2025-07-17
**Agent:** Tron
**Status:** Implemented

## Context

The `AgentWithToolRouter` sample needed to integrate MCPToolRouter with `Microsoft.Agents.AI.OpenAI` 1.0.0-rc4. This required discovering the correct API surface for creating agents from Azure OpenAI clients.

## Decision

Use the following conversion chain: `AzureOpenAIClient` → `ChatClient` → `IChatClient` (via `.AsIChatClient()`) → `AIAgent` (via `.AsAIAgent()`).

## Key Findings

- `AsAIAgent()` is defined on `IChatClient` (from `Microsoft.Extensions.AI`), **not** on `ChatClient` (from `OpenAI.Chat`)
- The conversion method is `.AsIChatClient()` (from `Microsoft.Extensions.AI.OpenAIClientExtensions`), **not** `.AsChatClient()`
- `Microsoft.Extensions.AI.OpenAI` 10.3.0 is available as a transitive dependency through `Microsoft.Agents.AI.OpenAI` 1.0.0-rc4
- The `tools` parameter on `AsAIAgent()` is typed `IList<AITool>?`, requiring explicit cast from `AIFunction` to `AITool`

## Impact

This pattern should be used for any future samples that combine MCPToolRouter with the Microsoft Agent Framework.
