# FilteredFunctionCalling Sample

This sample demonstrates a **complete end-to-end workflow** using MCPToolRouter to filter tools before performing function calling with Azure OpenAI.

## What It Demonstrates

1. **Tool Filtering**: Using MCPToolRouter to identify the most relevant tools
2. **Function Calling**: Sending filtered tools to Azure OpenAI
3. **Tool Execution**: Handling tool call responses and executing functions
4. **Response Loop**: Getting the final response after tool execution

## Flow Diagram

```
User Prompt
    ↓
MCPToolRouter filters tools (top 3)
    ↓
Azure OpenAI receives filtered tools
    ↓
Model requests tool calls
    ↓
Execute tools locally
    ↓
Send results back to model
    ↓
Final response to user
```

## Prerequisites

You need:
- Azure OpenAI resource with a deployment (e.g., `gpt-5-mini` or `gpt-4`)
- API key and endpoint URL

## Configuration

This sample uses **User Secrets** for secure configuration management.

### Setup User Secrets

```bash
cd src/samples/FilteredFunctionCalling

# Set your Azure OpenAI endpoint
dotnet user-secrets set "AzureOpenAI:Endpoint" "https://your-resource.openai.azure.com/"

# Set your API key
dotnet user-secrets set "AzureOpenAI:ApiKey" "your-api-key-here"

# Set your deployment name
dotnet user-secrets set "AzureOpenAI:DeploymentName" "gpt-5-mini"
```

## How to Run

```bash
cd src/samples/FilteredFunctionCalling
dotnet run
```

## Sample Output

```
╔════════════════════════════════════════════════════════╗
║      🎯 Filtered Function Calling with Azure OpenAI   ║
╚════════════════════════════════════════════════════════╝

✅ Configuration loaded
   Endpoint: https://your-resource.openai.azure.com/
   Deployment: gpt-5-mini

📦 Registered 8 tools with implementations

════════════════════════════════════════════════════════
💬 User: What's the weather like in Seattle?
════════════════════════════════════════════════════════

🔍 Step 1: Filtering tools with MCPToolRouter...
   Found 3 relevant tools:
     ✅ get_weather (score: 0.785)
     ✅ get_time (score: 0.432)
     ✅ get_stock_price (score: 0.301)

🤖 Step 2: Calling Azure OpenAI with filtered tools...
   Model requested 1 tool call(s)

⚙️  Step 3: Executing tool: get_weather
   Result: Weather in Seattle: Sunny, 72°F (22°C), Humidity: 45%, Wind: 5 mph

🤖 Step 4: Getting final response from model...

💬 Assistant: The current weather in Seattle is sunny with a temperature 
of 72°F (22°C). The humidity is at 45% and there's a light wind of 5 mph. 
Perfect weather for outdoor activities!
```

## Key Concepts

### Tool Filtering Benefits

- **Reduced token usage**: Only 3 tools sent instead of 8
- **Improved accuracy**: Model sees only relevant tools
- **Lower latency**: Smaller context = faster processing
- **Cost savings**: Fewer tokens = lower API costs

### Function Calling Flow

1. **First API call**: Model decides which tool(s) to call
2. **Tool execution**: Your code executes the requested functions
3. **Second API call**: Model receives results and generates final response

### Scaling to Production

In a real application:
- Replace stub implementations with actual API calls
- Add error handling and retries
- Implement authentication and authorization
- Add logging and monitoring
- Consider caching for frequently called tools

## Comparison with Other Samples

| Sample | Purpose |
|--------|---------|
| **BasicUsage** | Learn the library basics |
| **TokenComparison** | Measure token savings |
| **FilteredFunctionCalling** | Build real applications (this sample) |

## Next Steps

- Implement real tool functions (API calls, database queries, etc.)
- Add error handling for failed tool executions
- Experiment with different `topK` values
- Try multi-turn conversations with context
