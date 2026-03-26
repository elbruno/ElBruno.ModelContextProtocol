# FunctionalToolsValidation Sample

Validates that **53 real C# tool implementations** produce correct results when called by an LLM, comparing standard mode (all tools) vs. routed mode (MCPToolRouter-filtered top-5).

## What it does

1. Registers **53 functional tools** across 4 domains: Math (20), String (16), DateTime (8), Conversion (9)
2. Defines **12 test scenarios** with known expected answers
3. For each scenario, runs a full **tool execution loop** in both modes — the LLM calls the tool, the app executes the real C# code, sends the result back, and the LLM gives the final answer
4. **Validates correctness** by checking that the expected answer appears in the LLM's response
5. Shows a **comparison table** with token usage and savings

## Setup

```bash
cd src/samples/FunctionalToolsValidation
dotnet user-secrets set "AzureOpenAI:Endpoint" "https://your-resource.openai.azure.com/"
dotnet user-secrets set "AzureOpenAI:ApiKey" "your-api-key"
dotnet user-secrets set "AzureOpenAI:DeploymentName" "gpt-5-mini"
```

## Run

```bash
dotnet run --project src/samples/FunctionalToolsValidation
```

## Tool Domains

| Domain     | Count | Examples                                               |
|------------|-------|--------------------------------------------------------|
| Math       | 20    | add, subtract, multiply, factorial, fibonacci, gcd     |
| String     | 16    | reverse, uppercase, word_count, replace, starts_with   |
| DateTime   | 8     | day_of_week, days_between, add_days, is_weekend        |
| Conversion | 9     | celsius_to_fahrenheit, hex_to_decimal, km_to_miles     |

## Key Features

- **Real implementations** — every tool executes actual C# code (not stubs)
- **Full tool loop** — handles multi-turn LLM ↔ tool interactions
- **JSON schemas** — each tool has a proper InputSchema so the LLM knows the parameter types
- **Fuzzy validation** — checks if expected value appears in the LLM's natural language response
- **Token comparison** — measures and reports savings from MCPToolRouter filtering
