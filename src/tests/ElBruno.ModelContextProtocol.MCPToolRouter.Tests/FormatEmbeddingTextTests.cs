using System.Text.Json;
using ModelContextProtocol.Protocol;
using Xunit;

namespace ElBruno.ModelContextProtocol.MCPToolRouter.Tests;

public class FormatEmbeddingTextTests
{
    #region {Parameters} Placeholder Tests

    [Fact]
    public void FormatEmbeddingText_WithParametersPlaceholder_ResolvesParameterInfo()
    {
        // Arrange
        var schema = new
        {
            type = "object",
            properties = new
            {
                location = new { type = "string", description = "City name" },
                units = new { type = "string", description = "Temperature units" }
            },
            required = new[] { "location" }
        };

        var tool = new Tool
        {
            Name = "get_weather",
            Description = "Get weather info",
            InputSchema = JsonSerializer.SerializeToElement(schema)
        };

        // Act
        var result = ToolIndex.FormatEmbeddingText(tool, "{Name}: {Description}. Parameters: {Parameters}");

        // Assert
        Assert.Equal("get_weather: Get weather info. Parameters: location (string) - City name, units (string) - Temperature units", result);
    }

    [Fact]
    public void FormatEmbeddingText_WithNoInputSchema_ParametersResolvesToEmpty()
    {
        // Arrange — Tool with default InputSchema (Undefined)
        var tool = new Tool
        {
            Name = "simple_tool",
            Description = "A tool with no schema"
        };

        // Act
        var result = ToolIndex.FormatEmbeddingText(tool, "{Name}: {Description}. Parameters: {Parameters}");

        // Assert
        Assert.Equal("simple_tool: A tool with no schema. Parameters: ", result);
    }

    [Fact]
    public void FormatEmbeddingText_WithEmptyProperties_ParametersResolvesToEmpty()
    {
        // Arrange — InputSchema with no properties
        var schema = new { type = "object" };
        var tool = new Tool
        {
            Name = "no_params",
            Description = "Tool without properties",
            InputSchema = JsonSerializer.SerializeToElement(schema)
        };

        // Act
        var result = ToolIndex.FormatEmbeddingText(tool, "{Name}: {Description}. Parameters: {Parameters}");

        // Assert
        Assert.Equal("no_params: Tool without properties. Parameters: ", result);
    }

    [Fact]
    public void FormatEmbeddingText_ParameterWithTypeOnly_FormatsCorrectly()
    {
        // Arrange — parameter has type but no description
        var schema = new
        {
            type = "object",
            properties = new
            {
                count = new { type = "integer" }
            }
        };

        var tool = new Tool
        {
            Name = "counter",
            Description = "Counts things",
            InputSchema = JsonSerializer.SerializeToElement(schema)
        };

        // Act
        var result = ToolIndex.FormatEmbeddingText(tool, "{Name}: {Description}. Parameters: {Parameters}");

        // Assert
        Assert.Equal("counter: Counts things. Parameters: count (integer)", result);
    }

    [Fact]
    public void FormatEmbeddingText_ParameterWithNoTypeOrDescription_FormatsNameOnly()
    {
        // Arrange — parameter has neither type nor description
        var json = """{"type":"object","properties":{"raw":{}}}""";
        var schema = JsonSerializer.Deserialize<JsonElement>(json);

        var tool = new Tool
        {
            Name = "raw_tool",
            Description = "Has raw param",
            InputSchema = schema
        };

        // Act
        var result = ToolIndex.FormatEmbeddingText(tool, "{Parameters}");

        // Assert
        Assert.Equal("raw", result);
    }

    #endregion

    #region {InputSchema} Placeholder Tests

    [Fact]
    public void FormatEmbeddingText_WithInputSchemaPlaceholder_ResolvesToRawJson()
    {
        // Arrange
        var schemaObj = new
        {
            type = "object",
            properties = new
            {
                query = new { type = "string", description = "Search query" }
            }
        };

        var tool = new Tool
        {
            Name = "search",
            Description = "Search things",
            InputSchema = JsonSerializer.SerializeToElement(schemaObj)
        };

        // Act
        var result = ToolIndex.FormatEmbeddingText(tool, "{Name}: {Description}. Schema: {InputSchema}");

        // Assert
        Assert.Contains("\"type\":\"object\"", result);
        Assert.Contains("\"query\"", result);
        Assert.StartsWith("search: Search things. Schema: ", result);
    }

    [Fact]
    public void FormatEmbeddingText_WithDefaultInputSchema_InputSchemaResolvesToDefaultJson()
    {
        // Arrange — Tool's default InputSchema is {"type":"object"}
        var tool = new Tool
        {
            Name = "bare_tool",
            Description = "No schema"
        };

        // Act
        var result = ToolIndex.FormatEmbeddingText(tool, "{Name}. Schema: {InputSchema}");

        // Assert
        Assert.StartsWith("bare_tool. Schema: ", result);
        Assert.Contains("object", result);
    }

    #endregion

    #region Backward Compatibility Tests

    [Fact]
    public void FormatEmbeddingText_WithoutNewPlaceholders_BehaviorUnchanged()
    {
        // Arrange — classic template with no {Parameters} or {InputSchema}
        var tool = new Tool
        {
            Name = "legacy_tool",
            Description = "Does legacy things"
        };

        // Act
        var result = ToolIndex.FormatEmbeddingText(tool, "{Name}: {Description}");

        // Assert
        Assert.Equal("legacy_tool: Does legacy things", result);
    }

    [Fact]
    public void FormatEmbeddingText_WithNullDescription_ResolvesToEmpty()
    {
        // Arrange
        var tool = new Tool
        {
            Name = "no_desc",
            Description = null
        };

        // Act
        var result = ToolIndex.FormatEmbeddingText(tool, "{Name}: {Description}");

        // Assert
        Assert.Equal("no_desc: ", result);
    }

    [Fact]
    public void FormatEmbeddingText_DefaultTemplate_IncludesParameters()
    {
        // Arrange — verify the new default template works end-to-end
        var schema = new
        {
            type = "object",
            properties = new
            {
                a = new { type = "number", description = "First number" },
                b = new { type = "number", description = "Second number" }
            },
            required = new[] { "a", "b" }
        };

        var tool = new Tool
        {
            Name = "add",
            Description = "Add two numbers",
            InputSchema = JsonSerializer.SerializeToElement(schema)
        };

        var defaultTemplate = new ToolIndexOptions().EmbeddingTextTemplate;

        // Act
        var result = ToolIndex.FormatEmbeddingText(tool, defaultTemplate);

        // Assert
        Assert.Equal("add: Add two numbers. Parameters: a (number) - First number, b (number) - Second number", result);
    }

    [Fact]
    public void FormatEmbeddingText_AllPlaceholders_ResolvesCorrectly()
    {
        // Arrange
        var schema = new
        {
            type = "object",
            properties = new
            {
                x = new { type = "number", description = "Value" }
            }
        };

        var tool = new Tool
        {
            Name = "calc",
            Description = "Calculate",
            InputSchema = JsonSerializer.SerializeToElement(schema)
        };

        // Act
        var result = ToolIndex.FormatEmbeddingText(tool, "{Name} | {Description} | {Parameters} | {InputSchema}");

        // Assert
        Assert.StartsWith("calc | Calculate | x (number) - Value | ", result);
        Assert.Contains("\"x\"", result);
    }

    #endregion

    #region FormatParameters Unit Tests

    [Fact]
    public void FormatParameters_WithMultipleParams_JoinsWithComma()
    {
        var schema = new
        {
            type = "object",
            properties = new
            {
                first = new { type = "string", description = "First param" },
                second = new { type = "integer", description = "Second param" }
            }
        };

        var tool = new Tool
        {
            Name = "multi",
            Description = "Multi-param tool",
            InputSchema = JsonSerializer.SerializeToElement(schema)
        };

        var result = ToolIndex.FormatParameters(tool);

        Assert.Equal("first (string) - First param, second (integer) - Second param", result);
    }

    [Fact]
    public void FormatParameters_WithUndefinedSchema_ReturnsEmpty()
    {
        var tool = new Tool { Name = "t", Description = "d" };
        Assert.Equal(string.Empty, ToolIndex.FormatParameters(tool));
    }

    [Fact]
    public void FormatInputSchema_WithDefaultSchema_ReturnsDefaultJson()
    {
        // Tool's default InputSchema is {"type":"object"}, not Undefined
        var tool = new Tool { Name = "t", Description = "d" };
        var result = ToolIndex.FormatInputSchema(tool);
        Assert.Contains("object", result);
    }

    [Fact]
    public void FormatInputSchema_WithSchema_ReturnsRawJson()
    {
        var schema = new { type = "object", properties = new { q = new { type = "string" } } };
        var tool = new Tool
        {
            Name = "t",
            Description = "d",
            InputSchema = JsonSerializer.SerializeToElement(schema)
        };

        var result = ToolIndex.FormatInputSchema(tool);
        Assert.Contains("\"type\":\"object\"", result);
    }

    #endregion
}
