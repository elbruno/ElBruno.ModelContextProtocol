# BasicUsage Sample

This sample demonstrates the basic usage of the **ElBruno.ModelContextProtocol.MCPToolRouter** library.

## What It Demonstrates

- Creating a `ToolIndex` from MCP tool definitions
- Performing semantic search queries against indexed tools
- Filtering results by `topK` and `minScore`
- Understanding similarity scores

## How to Run

```bash
cd src/samples/BasicUsage
dotnet run
```

### First Run

On the first run, the library will download the ONNX embedding model (~90MB). This is a one-time operation and subsequent runs will use the cached model.

## Sample Output

```
═══════════════════════════════════════════════════════════
  🔧 MCPToolRouter - Basic Usage Sample
═══════════════════════════════════════════════════════════

📦 Indexed 8 tools

⏳ Creating tool index (downloading embedding model on first run)...

✅ Index created successfully!

─────────────────────────────────────────────────────────
🔍 Query: "What's the temperature outside?"

📊 Top 3 Results:
  🟢 get_weather               (score: 0.782)
  🟠 get_stock_price           (score: 0.345)
  🟠 calculate                 (score: 0.312)
```

## Key Concepts

### Similarity Score

The similarity score (0.0 to 1.0) indicates how relevant a tool is to the query:
- 🟢 >= 0.7: Highly relevant
- 🟡 >= 0.5: Moderately relevant
- 🟠 >= 0.3: Somewhat relevant
- 🔴 < 0.3: Low relevance

### Parameters

- **topK**: Maximum number of results to return (default: 5)
- **minScore**: Minimum similarity score threshold (default: 0.0)

## Next Steps

- See **TokenComparison** sample for real-world token usage savings
- See **FilteredFunctionCalling** sample for end-to-end Azure OpenAI integration
