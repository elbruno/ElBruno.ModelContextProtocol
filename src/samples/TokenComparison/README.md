# TokenComparison Sample

This sample demonstrates the **key value proposition** of MCPToolRouter by comparing token usage when sending ALL tools vs. only the relevant tools to an Azure OpenAI model.

## What It Demonstrates

- **Standard Mode**: Sending all 18 tools to the LLM for every request
- **Routed Mode**: Using MCPToolRouter to filter down to the top 3 most relevant tools
- **Token Savings**: Real measurements showing 60-80% reduction in input tokens

## Prerequisites

You need:
- Azure OpenAI resource with a deployment (e.g., `gpt-5-mini` or `gpt-4`)
- API key and endpoint URL

## Configuration

This sample uses **User Secrets** for secure configuration management.

### Setup User Secrets

```bash
cd src/samples/TokenComparison

# Set your Azure OpenAI endpoint
dotnet user-secrets set "AzureOpenAI:Endpoint" "https://your-resource.openai.azure.com/"

# Set your API key
dotnet user-secrets set "AzureOpenAI:ApiKey" "your-api-key-here"

# Set your deployment name
dotnet user-secrets set "AzureOpenAI:DeploymentName" "gpt-5-mini"
```

### Alternative: Environment Variables

You can also use environment variables:
- `AzureOpenAI__Endpoint`
- `AzureOpenAI__ApiKey`
- `AzureOpenAI__DeploymentName`

## How to Run

```bash
cd src/samples/TokenComparison
dotnet run
```

## Sample Output

```
╔════════════════════════════════════════════════════════╗
║    🔀 MCPToolRouter Token Usage Comparison Demo       ║
╚════════════════════════════════════════════════════════╝

✅ Configuration loaded
   Endpoint: https://your-resource.openai.azure.com/
   Deployment: gpt-5-mini

📦 Created 18 tool definitions

════════════════════════════════════════════════════════
User Prompt: "What's the weather in Seattle?"
════════════════════════════════════════════════════════

🔵 STANDARD MODE: Sending ALL 18 tools to the model...
   Input tokens:  1,842
   Output tokens: 45
   Total tokens:  1,887

🟢 ROUTED MODE: Using MCPToolRouter to find relevant tools...
   Selected tools:
     ✅ get_weather (score: 0.782)
     ✅ get_forecast (score: 0.654)
     ✅ get_weather_alerts (score: 0.621)

   Input tokens:  523
   Output tokens: 42
   Total tokens:  565

╔════════════════════════════════════════════════════════╗
║                    💰 SAVINGS                          ║
╠════════════════════════════════════════════════════════╣
║  Input tokens saved:    1,319 (71.6%)                  ║
║  Total tokens saved:    1,322 (70.1%)                  ║
╚════════════════════════════════════════════════════════╝
```

## Key Insights

### Token Savings Breakdown

- **Input tokens**: 60-80% reduction by sending only 3 relevant tools instead of 18
- **Cost impact**: Direct proportional cost savings on API usage
- **Performance**: Faster response times with smaller context

### When Token Savings Matter Most

1. **High-volume applications**: Thousands of requests per day
2. **Large tool catalogs**: 50+ available tools
3. **Cost-sensitive deployments**: Budget constraints
4. **Latency-critical systems**: Every token counts

## Scaling Impact

With 100 tools instead of 18:
- Standard mode: ~8,000-10,000 input tokens per request
- Routed mode (top 5): ~1,500-2,000 input tokens per request
- **Savings: 80-85%**

## Next Steps

- See **FilteredFunctionCalling** for end-to-end function calling implementation
- Experiment with different `topK` values (3, 5, 10)
- Try different Azure OpenAI models to see token differences
