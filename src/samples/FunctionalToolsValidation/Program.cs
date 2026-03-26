using Azure;
using Azure.AI.OpenAI;
using ElBruno.ModelContextProtocol.MCPToolRouter;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol.Protocol;
using System.Text.Json;
using OpenAI.Chat;

Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║  🔬 Functional Tools Validation — 50+ Real Tools           ║");
Console.WriteLine("╠══════════════════════════════════════════════════════════════╣");
Console.WriteLine();

// ── Configuration ──────────────────────────────────────────
var configuration = new ConfigurationBuilder()
    .AddUserSecrets<Program>()
    .AddEnvironmentVariables()
    .Build();

var endpoint = configuration["AzureOpenAI:Endpoint"];
var apiKey = configuration["AzureOpenAI:ApiKey"];
var deploymentName = configuration["AzureOpenAI:DeploymentName"];

if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(deploymentName))
{
    Console.WriteLine("❌ Azure OpenAI configuration not found. Please set up user secrets:\n");
    Console.WriteLine("   cd src/samples/FunctionalToolsValidation");
    Console.WriteLine("   dotnet user-secrets set \"AzureOpenAI:Endpoint\" \"https://your-resource.openai.azure.com/\"");
    Console.WriteLine("   dotnet user-secrets set \"AzureOpenAI:ApiKey\" \"your-api-key\"");
    Console.WriteLine("   dotnet user-secrets set \"AzureOpenAI:DeploymentName\" \"gpt-5-mini\"\n");
    return;
}

Console.WriteLine($"✅ Configuration loaded");
Console.WriteLine($"   Endpoint: {endpoint}");
Console.WriteLine($"   Deployment: {deploymentName}\n");

var azureClient = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
var chatClient = azureClient.GetChatClient(deploymentName);

// ══════════════════════════════════════════════════════════════
//  TOOL REGISTRY — 53 real C# implementations
// ══════════════════════════════════════════════════════════════

var toolRegistry = new Dictionary<string, Func<JsonElement, string>>
{
    // ── Math Operations (20 tools) ──────────────────────────
    ["add_numbers"] = args =>
    {
        var a = args.GetProperty("a").GetDouble();
        var b = args.GetProperty("b").GetDouble();
        return (a + b).ToString("G");
    },
    ["subtract_numbers"] = args =>
    {
        var a = args.GetProperty("a").GetDouble();
        var b = args.GetProperty("b").GetDouble();
        return (a - b).ToString("G");
    },
    ["multiply_numbers"] = args =>
    {
        var a = args.GetProperty("a").GetDouble();
        var b = args.GetProperty("b").GetDouble();
        return (a * b).ToString("G");
    },
    ["divide_numbers"] = args =>
    {
        var a = args.GetProperty("a").GetDouble();
        var b = args.GetProperty("b").GetDouble();
        return b == 0 ? "Error: Division by zero" : (a / b).ToString("G");
    },
    ["power"] = args =>
    {
        var baseNum = args.GetProperty("base").GetDouble();
        var exponent = args.GetProperty("exponent").GetDouble();
        return Math.Pow(baseNum, exponent).ToString("G");
    },
    ["sqrt"] = args =>
    {
        var n = args.GetProperty("n").GetDouble();
        return Math.Sqrt(n).ToString("G");
    },
    ["modulo"] = args =>
    {
        var a = args.GetProperty("a").GetDouble();
        var b = args.GetProperty("b").GetDouble();
        return (a % b).ToString("G");
    },
    ["factorial"] = args =>
    {
        var n = args.GetProperty("n").GetInt32();
        long result = 1;
        for (int i = 2; i <= n; i++) result *= i;
        return result.ToString();
    },
    ["fibonacci"] = args =>
    {
        var n = args.GetProperty("n").GetInt32();
        if (n <= 0) return "0";
        if (n == 1) return "1";
        long a = 0, b = 1;
        for (int i = 2; i <= n; i++) { var temp = a + b; a = b; b = temp; }
        return b.ToString();
    },
    ["gcd"] = args =>
    {
        var a = Math.Abs(args.GetProperty("a").GetInt64());
        var b = Math.Abs(args.GetProperty("b").GetInt64());
        while (b != 0) { var temp = b; b = a % b; a = temp; }
        return a.ToString();
    },
    ["lcm"] = args =>
    {
        var a = Math.Abs(args.GetProperty("a").GetInt64());
        var b = Math.Abs(args.GetProperty("b").GetInt64());
        long gcd = a, temp = b;
        while (temp != 0) { var r = gcd % temp; gcd = temp; temp = r; }
        return (a / gcd * b).ToString();
    },
    ["abs"] = args =>
    {
        var n = args.GetProperty("n").GetDouble();
        return Math.Abs(n).ToString("G");
    },
    ["min_number"] = args =>
    {
        var a = args.GetProperty("a").GetDouble();
        var b = args.GetProperty("b").GetDouble();
        return Math.Min(a, b).ToString("G");
    },
    ["max_number"] = args =>
    {
        var a = args.GetProperty("a").GetDouble();
        var b = args.GetProperty("b").GetDouble();
        return Math.Max(a, b).ToString("G");
    },
    ["round_number"] = args =>
    {
        var n = args.GetProperty("n").GetDouble();
        var decimals = args.TryGetProperty("decimals", out var d) ? d.GetInt32() : 0;
        return Math.Round(n, decimals).ToString("G");
    },
    ["ceiling"] = args =>
    {
        var n = args.GetProperty("n").GetDouble();
        return Math.Ceiling(n).ToString("G");
    },
    ["floor"] = args =>
    {
        var n = args.GetProperty("n").GetDouble();
        return Math.Floor(n).ToString("G");
    },
    ["percentage"] = args =>
    {
        var value = args.GetProperty("value").GetDouble();
        var total = args.GetProperty("total").GetDouble();
        return total == 0 ? "Error: Total is zero" : (value / total * 100).ToString("G");
    },
    ["average"] = args =>
    {
        var numbers = args.GetProperty("numbers").EnumerateArray().Select(e => e.GetDouble()).ToList();
        return numbers.Count == 0 ? "Error: Empty list" : numbers.Average().ToString("G");
    },
    ["median"] = args =>
    {
        var numbers = args.GetProperty("numbers").EnumerateArray().Select(e => e.GetDouble()).OrderBy(n => n).ToList();
        if (numbers.Count == 0) return "Error: Empty list";
        int mid = numbers.Count / 2;
        return (numbers.Count % 2 == 0 ? (numbers[mid - 1] + numbers[mid]) / 2.0 : numbers[mid]).ToString("G");
    },

    // ── String Operations (16 tools) ────────────────────────
    ["reverse_string"] = args =>
    {
        var s = args.GetProperty("text").GetString() ?? "";
        return new string(s.Reverse().ToArray());
    },
    ["uppercase"] = args =>
    {
        var s = args.GetProperty("text").GetString() ?? "";
        return s.ToUpperInvariant();
    },
    ["lowercase"] = args =>
    {
        var s = args.GetProperty("text").GetString() ?? "";
        return s.ToLowerInvariant();
    },
    ["trim_string"] = args =>
    {
        var s = args.GetProperty("text").GetString() ?? "";
        return s.Trim();
    },
    ["string_length"] = args =>
    {
        var s = args.GetProperty("text").GetString() ?? "";
        return s.Length.ToString();
    },
    ["string_contains"] = args =>
    {
        var text = args.GetProperty("text").GetString() ?? "";
        var search = args.GetProperty("search").GetString() ?? "";
        return text.Contains(search, StringComparison.OrdinalIgnoreCase).ToString();
    },
    ["string_replace"] = args =>
    {
        var text = args.GetProperty("text").GetString() ?? "";
        var oldValue = args.GetProperty("old_value").GetString() ?? "";
        var newValue = args.GetProperty("new_value").GetString() ?? "";
        return text.Replace(oldValue, newValue);
    },
    ["string_split"] = args =>
    {
        var text = args.GetProperty("text").GetString() ?? "";
        var delimiter = args.GetProperty("delimiter").GetString() ?? " ";
        return string.Join(", ", text.Split(delimiter));
    },
    ["string_join"] = args =>
    {
        var items = args.GetProperty("items").EnumerateArray().Select(e => e.GetString() ?? "").ToList();
        var delimiter = args.TryGetProperty("delimiter", out var d) ? d.GetString() ?? " " : " ";
        return string.Join(delimiter, items);
    },
    ["string_repeat"] = args =>
    {
        var text = args.GetProperty("text").GetString() ?? "";
        var count = args.GetProperty("count").GetInt32();
        return string.Concat(Enumerable.Repeat(text, count));
    },
    ["pad_left"] = args =>
    {
        var text = args.GetProperty("text").GetString() ?? "";
        var width = args.GetProperty("width").GetInt32();
        var ch = args.TryGetProperty("char", out var c) ? (c.GetString() ?? " ")[0] : ' ';
        return text.PadLeft(width, ch);
    },
    ["pad_right"] = args =>
    {
        var text = args.GetProperty("text").GetString() ?? "";
        var width = args.GetProperty("width").GetInt32();
        var ch = args.TryGetProperty("char", out var c) ? (c.GetString() ?? " ")[0] : ' ';
        return text.PadRight(width, ch);
    },
    ["starts_with"] = args =>
    {
        var text = args.GetProperty("text").GetString() ?? "";
        var prefix = args.GetProperty("prefix").GetString() ?? "";
        return text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase).ToString();
    },
    ["ends_with"] = args =>
    {
        var text = args.GetProperty("text").GetString() ?? "";
        var suffix = args.GetProperty("suffix").GetString() ?? "";
        return text.EndsWith(suffix, StringComparison.OrdinalIgnoreCase).ToString();
    },
    ["char_count"] = args =>
    {
        var text = args.GetProperty("text").GetString() ?? "";
        var ch = args.GetProperty("char").GetString() ?? "";
        return text.Count(c => ch.Contains(c, StringComparison.OrdinalIgnoreCase)).ToString();
    },
    ["word_count"] = args =>
    {
        var text = args.GetProperty("text").GetString() ?? "";
        return text.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length.ToString();
    },

    // ── Date/Time Operations (8 tools) ──────────────────────
    ["current_time"] = _ =>
    {
        return DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC");
    },
    ["add_days"] = args =>
    {
        var date = DateTime.Parse(args.GetProperty("date").GetString() ?? "");
        var days = args.GetProperty("days").GetInt32();
        return date.AddDays(days).ToString("yyyy-MM-dd");
    },
    ["days_between"] = args =>
    {
        var date1 = DateTime.Parse(args.GetProperty("date1").GetString() ?? "");
        var date2 = DateTime.Parse(args.GetProperty("date2").GetString() ?? "");
        return Math.Abs((date2 - date1).Days).ToString();
    },
    ["day_of_week"] = args =>
    {
        var date = DateTime.Parse(args.GetProperty("date").GetString() ?? "");
        return date.DayOfWeek.ToString();
    },
    ["is_weekend"] = args =>
    {
        var date = DateTime.Parse(args.GetProperty("date").GetString() ?? "");
        return (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday).ToString();
    },
    ["format_date"] = args =>
    {
        var date = DateTime.Parse(args.GetProperty("date").GetString() ?? "");
        var format = args.TryGetProperty("format", out var f) ? f.GetString() ?? "yyyy-MM-dd" : "yyyy-MM-dd";
        return date.ToString(format);
    },
    ["parse_date"] = args =>
    {
        var text = args.GetProperty("text").GetString() ?? "";
        return DateTime.TryParse(text, out var d) ? d.ToString("yyyy-MM-dd") : "Error: Invalid date";
    },
    ["time_zone_convert"] = args =>
    {
        var time = DateTime.Parse(args.GetProperty("time").GetString() ?? "");
        var fromZone = args.GetProperty("from_zone").GetString() ?? "UTC";
        var toZone = args.GetProperty("to_zone").GetString() ?? "UTC";
        var srcTz = TimeZoneInfo.FindSystemTimeZoneById(fromZone);
        var dstTz = TimeZoneInfo.FindSystemTimeZoneById(toZone);
        var utc = TimeZoneInfo.ConvertTimeToUtc(time, srcTz);
        var converted = TimeZoneInfo.ConvertTimeFromUtc(utc, dstTz);
        return converted.ToString("yyyy-MM-dd HH:mm:ss");
    },

    // ── Conversion Operations (9 tools) ─────────────────────
    ["celsius_to_fahrenheit"] = args =>
    {
        var c = args.GetProperty("celsius").GetDouble();
        return (c * 9.0 / 5.0 + 32).ToString("G");
    },
    ["fahrenheit_to_celsius"] = args =>
    {
        var f = args.GetProperty("fahrenheit").GetDouble();
        return ((f - 32) * 5.0 / 9.0).ToString("G");
    },
    ["km_to_miles"] = args =>
    {
        var km = args.GetProperty("km").GetDouble();
        return (km * 0.621371).ToString("G");
    },
    ["miles_to_km"] = args =>
    {
        var miles = args.GetProperty("miles").GetDouble();
        return (miles * 1.60934).ToString("G");
    },
    ["kg_to_lbs"] = args =>
    {
        var kg = args.GetProperty("kg").GetDouble();
        return (kg * 2.20462).ToString("G");
    },
    ["lbs_to_kg"] = args =>
    {
        var lbs = args.GetProperty("lbs").GetDouble();
        return (lbs * 0.453592).ToString("G");
    },
    ["hex_to_decimal"] = args =>
    {
        var hex = args.GetProperty("hex").GetString() ?? "";
        return Convert.ToInt64(hex, 16).ToString();
    },
    ["decimal_to_hex"] = args =>
    {
        var dec = args.GetProperty("decimal").GetInt64();
        return dec.ToString("X");
    },
    ["binary_to_decimal"] = args =>
    {
        var bin = args.GetProperty("binary").GetString() ?? "";
        return Convert.ToInt64(bin, 2).ToString();
    },
};

Console.WriteLine($"📦 Registered {toolRegistry.Count} functional tools across 4 domains\n");

// ══════════════════════════════════════════════════════════════
//  MCP TOOL DEFINITIONS — with proper JSON schemas
// ══════════════════════════════════════════════════════════════

var mcpTools = new List<Tool>();

// Helper to create a Tool with InputSchema
static Tool MakeTool(string name, string description, object schema)
{
    return new Tool
    {
        Name = name,
        Description = description,
        InputSchema = JsonSerializer.SerializeToElement(schema)
    };
}

// ── Math tool schemas ───────────────────────────────────────
var twoNumbers = new { type = "object", properties = new { a = new { type = "number", description = "First number" }, b = new { type = "number", description = "Second number" } }, required = new[] { "a", "b" } };
mcpTools.Add(MakeTool("add_numbers", "Add two numbers together and return their sum", twoNumbers));
mcpTools.Add(MakeTool("subtract_numbers", "Subtract the second number from the first", twoNumbers));
mcpTools.Add(MakeTool("multiply_numbers", "Multiply two numbers together", twoNumbers));
mcpTools.Add(MakeTool("divide_numbers", "Divide the first number by the second", twoNumbers));
mcpTools.Add(MakeTool("modulo", "Calculate the remainder of dividing a by b (a mod b)", twoNumbers));
mcpTools.Add(MakeTool("gcd", "Calculate the greatest common divisor of two integers", twoNumbers));
mcpTools.Add(MakeTool("lcm", "Calculate the least common multiple of two integers", twoNumbers));
mcpTools.Add(MakeTool("min_number", "Return the smaller of two numbers", twoNumbers));
mcpTools.Add(MakeTool("max_number", "Return the larger of two numbers", twoNumbers));
mcpTools.Add(MakeTool("percentage", "Calculate what percentage value is of total",
    new { type = "object", properties = new { value = new { type = "number", description = "The part value" }, total = new { type = "number", description = "The total value" } }, required = new[] { "value", "total" } }));
mcpTools.Add(MakeTool("power", "Raise a base number to an exponent power",
    new { type = "object", properties = new { @base = new { type = "number", description = "The base number" }, exponent = new { type = "number", description = "The exponent" } }, required = new[] { "base", "exponent" } }));
mcpTools.Add(MakeTool("sqrt", "Calculate the square root of a number",
    new { type = "object", properties = new { n = new { type = "number", description = "The number" } }, required = new[] { "n" } }));
mcpTools.Add(MakeTool("factorial", "Calculate the factorial of a non-negative integer (n!)",
    new { type = "object", properties = new { n = new { type = "integer", description = "Non-negative integer" } }, required = new[] { "n" } }));
mcpTools.Add(MakeTool("fibonacci", "Calculate the nth Fibonacci number (0-indexed: fib(0)=0, fib(1)=1, ...)",
    new { type = "object", properties = new { n = new { type = "integer", description = "The position in the Fibonacci sequence (0-indexed)" } }, required = new[] { "n" } }));
mcpTools.Add(MakeTool("abs", "Calculate the absolute value of a number",
    new { type = "object", properties = new { n = new { type = "number", description = "The number" } }, required = new[] { "n" } }));
mcpTools.Add(MakeTool("round_number", "Round a number to specified decimal places",
    new { type = "object", properties = new { n = new { type = "number", description = "The number to round" }, decimals = new { type = "integer", description = "Number of decimal places (default 0)" } }, required = new[] { "n" } }));
mcpTools.Add(MakeTool("ceiling", "Round a number up to the nearest integer",
    new { type = "object", properties = new { n = new { type = "number", description = "The number" } }, required = new[] { "n" } }));
mcpTools.Add(MakeTool("floor", "Round a number down to the nearest integer",
    new { type = "object", properties = new { n = new { type = "number", description = "The number" } }, required = new[] { "n" } }));
mcpTools.Add(MakeTool("average", "Calculate the arithmetic mean of a list of numbers",
    new { type = "object", properties = new { numbers = new { type = "array", items = new { type = "number" }, description = "List of numbers" } }, required = new[] { "numbers" } }));
mcpTools.Add(MakeTool("median", "Calculate the median of a list of numbers",
    new { type = "object", properties = new { numbers = new { type = "array", items = new { type = "number" }, description = "List of numbers" } }, required = new[] { "numbers" } }));

// ── String tool schemas ─────────────────────────────────────
var singleText = new { type = "object", properties = new { text = new { type = "string", description = "Input text" } }, required = new[] { "text" } };
mcpTools.Add(MakeTool("reverse_string", "Reverse a string of text character by character", singleText));
mcpTools.Add(MakeTool("uppercase", "Convert text to all uppercase letters", singleText));
mcpTools.Add(MakeTool("lowercase", "Convert text to all lowercase letters", singleText));
mcpTools.Add(MakeTool("trim_string", "Remove leading and trailing whitespace from text", singleText));
mcpTools.Add(MakeTool("string_length", "Return the number of characters in a string", singleText));
mcpTools.Add(MakeTool("word_count", "Count the number of words in a text string", singleText));
mcpTools.Add(MakeTool("string_contains", "Check if a text string contains a specified substring",
    new { type = "object", properties = new { text = new { type = "string", description = "Text to search in" }, search = new { type = "string", description = "Substring to search for" } }, required = new[] { "text", "search" } }));
mcpTools.Add(MakeTool("string_replace", "Replace all occurrences of a substring with another string",
    new { type = "object", properties = new { text = new { type = "string", description = "Original text" }, old_value = new { type = "string", description = "Substring to replace" }, new_value = new { type = "string", description = "Replacement string" } }, required = new[] { "text", "old_value", "new_value" } }));
mcpTools.Add(MakeTool("string_split", "Split a text string by a delimiter and return the parts",
    new { type = "object", properties = new { text = new { type = "string", description = "Text to split" }, delimiter = new { type = "string", description = "Delimiter character(s)" } }, required = new[] { "text", "delimiter" } }));
mcpTools.Add(MakeTool("string_join", "Join a list of strings with a delimiter",
    new { type = "object", properties = new { items = new { type = "array", items = new { type = "string" }, description = "Strings to join" }, delimiter = new { type = "string", description = "Delimiter (default: space)" } }, required = new[] { "items" } }));
mcpTools.Add(MakeTool("string_repeat", "Repeat a text string a specified number of times",
    new { type = "object", properties = new { text = new { type = "string", description = "Text to repeat" }, count = new { type = "integer", description = "Number of repetitions" } }, required = new[] { "text", "count" } }));
mcpTools.Add(MakeTool("pad_left", "Pad a string on the left to a specified width",
    new { type = "object", properties = new { text = new { type = "string", description = "Text to pad" }, width = new { type = "integer", description = "Total width" }, @char = new { type = "string", description = "Padding character (default: space)" } }, required = new[] { "text", "width" } }));
mcpTools.Add(MakeTool("pad_right", "Pad a string on the right to a specified width",
    new { type = "object", properties = new { text = new { type = "string", description = "Text to pad" }, width = new { type = "integer", description = "Total width" }, @char = new { type = "string", description = "Padding character (default: space)" } }, required = new[] { "text", "width" } }));
mcpTools.Add(MakeTool("starts_with", "Check if a text string starts with a specified prefix",
    new { type = "object", properties = new { text = new { type = "string", description = "Text to check" }, prefix = new { type = "string", description = "Prefix to look for" } }, required = new[] { "text", "prefix" } }));
mcpTools.Add(MakeTool("ends_with", "Check if a text string ends with a specified suffix",
    new { type = "object", properties = new { text = new { type = "string", description = "Text to check" }, suffix = new { type = "string", description = "Suffix to look for" } }, required = new[] { "text", "suffix" } }));
mcpTools.Add(MakeTool("char_count", "Count occurrences of a specific character in text",
    new { type = "object", properties = new { text = new { type = "string", description = "Text to search" }, @char = new { type = "string", description = "Character to count" } }, required = new[] { "text", "char" } }));

// ── DateTime tool schemas ───────────────────────────────────
mcpTools.Add(MakeTool("current_time", "Get the current date and time in UTC",
    new { type = "object", properties = new { } }));
mcpTools.Add(MakeTool("add_days", "Add a specified number of days to a date",
    new { type = "object", properties = new { date = new { type = "string", description = "Date in yyyy-MM-dd format" }, days = new { type = "integer", description = "Number of days to add (negative to subtract)" } }, required = new[] { "date", "days" } }));
mcpTools.Add(MakeTool("days_between", "Calculate the number of days between two dates",
    new { type = "object", properties = new { date1 = new { type = "string", description = "First date (yyyy-MM-dd)" }, date2 = new { type = "string", description = "Second date (yyyy-MM-dd)" } }, required = new[] { "date1", "date2" } }));
mcpTools.Add(MakeTool("day_of_week", "Get the day of the week for a given date",
    new { type = "object", properties = new { date = new { type = "string", description = "Date in yyyy-MM-dd format" } }, required = new[] { "date" } }));
mcpTools.Add(MakeTool("is_weekend", "Check if a given date falls on a weekend (Saturday or Sunday)",
    new { type = "object", properties = new { date = new { type = "string", description = "Date in yyyy-MM-dd format" } }, required = new[] { "date" } }));
mcpTools.Add(MakeTool("format_date", "Format a date string according to a specified format pattern",
    new { type = "object", properties = new { date = new { type = "string", description = "Date to format" }, format = new { type = "string", description = "Format pattern (e.g., yyyy-MM-dd, MM/dd/yyyy)" } }, required = new[] { "date" } }));
mcpTools.Add(MakeTool("parse_date", "Parse a text string into a standardized date format",
    new { type = "object", properties = new { text = new { type = "string", description = "Date text to parse" } }, required = new[] { "text" } }));
mcpTools.Add(MakeTool("time_zone_convert", "Convert a time from one time zone to another",
    new { type = "object", properties = new { time = new { type = "string", description = "Time to convert" }, from_zone = new { type = "string", description = "Source time zone ID" }, to_zone = new { type = "string", description = "Destination time zone ID" } }, required = new[] { "time", "from_zone", "to_zone" } }));

// ── Conversion tool schemas ─────────────────────────────────
mcpTools.Add(MakeTool("celsius_to_fahrenheit", "Convert a temperature from Celsius to Fahrenheit",
    new { type = "object", properties = new { celsius = new { type = "number", description = "Temperature in Celsius" } }, required = new[] { "celsius" } }));
mcpTools.Add(MakeTool("fahrenheit_to_celsius", "Convert a temperature from Fahrenheit to Celsius",
    new { type = "object", properties = new { fahrenheit = new { type = "number", description = "Temperature in Fahrenheit" } }, required = new[] { "fahrenheit" } }));
mcpTools.Add(MakeTool("km_to_miles", "Convert kilometers to miles",
    new { type = "object", properties = new { km = new { type = "number", description = "Distance in kilometers" } }, required = new[] { "km" } }));
mcpTools.Add(MakeTool("miles_to_km", "Convert miles to kilometers",
    new { type = "object", properties = new { miles = new { type = "number", description = "Distance in miles" } }, required = new[] { "miles" } }));
mcpTools.Add(MakeTool("kg_to_lbs", "Convert kilograms to pounds",
    new { type = "object", properties = new { kg = new { type = "number", description = "Weight in kilograms" } }, required = new[] { "kg" } }));
mcpTools.Add(MakeTool("lbs_to_kg", "Convert pounds to kilograms",
    new { type = "object", properties = new { lbs = new { type = "number", description = "Weight in pounds" } }, required = new[] { "lbs" } }));
mcpTools.Add(MakeTool("hex_to_decimal", "Convert a hexadecimal string to a decimal number",
    new { type = "object", properties = new { hex = new { type = "string", description = "Hexadecimal value (e.g., FF, 1A3)" } }, required = new[] { "hex" } }));
mcpTools.Add(MakeTool("decimal_to_hex", "Convert a decimal number to hexadecimal string",
    new { type = "object", properties = new { @decimal = new { type = "integer", description = "Decimal number" } }, required = new[] { "decimal" } }));
mcpTools.Add(MakeTool("binary_to_decimal", "Convert a binary string to a decimal number",
    new { type = "object", properties = new { binary = new { type = "string", description = "Binary string (e.g., 1010, 11111111)" } }, required = new[] { "binary" } }));

Console.WriteLine($"🔧 Defined {mcpTools.Count} MCP tool schemas\n");

// ══════════════════════════════════════════════════════════════
//  TEST SCENARIOS
// ══════════════════════════════════════════════════════════════

var scenarios = new (string Prompt, string Expected, string Domain)[]
{
    ("What is 42 + 58?", "100", "Math"),
    ("What is the factorial of 5?", "120", "Math"),
    ("What is the GCD of 48 and 18?", "6", "Math"),
    ("Reverse the word 'hello'", "olleh", "String"),
    ("How many words are in the sentence 'the quick brown fox jumps'?", "5", "String"),
    ("Convert the text 'hello world' to uppercase", "HELLO WORLD", "String"),
    ("What day of the week is 2026-01-01?", "Thursday", "DateTime"),
    ("How many days are between 2025-01-01 and 2025-12-31?", "364", "DateTime"),
    ("Convert 100 degrees Celsius to Fahrenheit", "212", "Conversion"),
    ("Convert the hexadecimal value FF to decimal", "255", "Conversion"),
    ("Convert the binary number 11111111 to decimal", "255", "Conversion"),
    ("What is 2 raised to the power of 10?", "1024", "Math"),
};

Console.WriteLine($"📋 Loaded {scenarios.Length} test scenarios\n");

// ══════════════════════════════════════════════════════════════
//  BUILD TOOL INDEX
// ══════════════════════════════════════════════════════════════

Console.WriteLine("🔍 Building ToolIndex...");
var indexOptions = new ToolIndexOptions { QueryCacheSize = 15 };
await using var toolIndex = await ToolIndex.CreateAsync(mcpTools.ToArray(), indexOptions);
Console.WriteLine($"🔍 ToolIndex ready — {mcpTools.Count} tools indexed\n");

// ══════════════════════════════════════════════════════════════
//  HELPER: Convert MCP Tool → ChatTool (with JSON schema)
// ══════════════════════════════════════════════════════════════

static ChatTool ConvertToChatTool(Tool mcpTool)
{
    BinaryData? parameters = mcpTool.InputSchema.ValueKind != JsonValueKind.Undefined
        ? BinaryData.FromString(mcpTool.InputSchema.GetRawText())
        : null;
    return ChatTool.CreateFunctionTool(mcpTool.Name, mcpTool.Description ?? "", parameters);
}

// ══════════════════════════════════════════════════════════════
//  HELPER: Execute tool call loop
// ══════════════════════════════════════════════════════════════

async Task<(string Response, int InputTokens, int OutputTokens, List<string> ToolsCalled)> RunToolLoop(
    string prompt, IEnumerable<Tool> tools, string label)
{
    var messages = new List<ChatMessage> { new UserChatMessage(prompt) };
    var options = new ChatCompletionOptions();
    foreach (var tool in tools)
        options.Tools.Add(ConvertToChatTool(tool));

    var toolsCalled = new List<string>();
    int totalInputTokens = 0, totalOutputTokens = 0;

    var response = await chatClient.CompleteChatAsync(messages, options);
    totalInputTokens += response.Value.Usage.InputTokenCount;
    totalOutputTokens += response.Value.Usage.OutputTokenCount;

    int maxIterations = 10;
    int iteration = 0;
    while (response.Value.FinishReason == ChatFinishReason.ToolCalls && iteration < maxIterations)
    {
        iteration++;
        var assistantMessage = new AssistantChatMessage(response.Value);
        messages.Add(assistantMessage);

        foreach (var toolCall in response.Value.ToolCalls)
        {
            string result;
            try
            {
                var args = JsonDocument.Parse(toolCall.FunctionArguments).RootElement;
                if (toolRegistry.TryGetValue(toolCall.FunctionName, out var handler))
                {
                    result = handler(args);
                }
                else
                {
                    result = $"Error: Unknown tool '{toolCall.FunctionName}'";
                }
            }
            catch (Exception ex)
            {
                result = $"Error: {ex.Message}";
            }

            toolsCalled.Add(toolCall.FunctionName);
            Console.WriteLine($"    🔧 {toolCall.FunctionName}({toolCall.FunctionArguments}) → {result}");
            messages.Add(new ToolChatMessage(toolCall.Id, result));
        }

        response = await chatClient.CompleteChatAsync(messages, options);
        totalInputTokens += response.Value.Usage.InputTokenCount;
        totalOutputTokens += response.Value.Usage.OutputTokenCount;
    }

    var finalText = response.Value.Content.Count > 0 ? response.Value.Content[0].Text ?? "" : "";
    return (finalText, totalInputTokens, totalOutputTokens, toolsCalled);
}

// ══════════════════════════════════════════════════════════════
//  RUN SCENARIOS
// ══════════════════════════════════════════════════════════════

var allChatToolsList = mcpTools.ToArray();
var results = new List<(string Domain, string Prompt, string Expected,
    string StdResult, int StdInput, int StdOutput, List<string> StdTools, bool StdPass,
    string RtResult, int RtInput, int RtOutput, List<string> RtTools, bool RtPass,
    string SelectedTools)>();

for (int i = 0; i < scenarios.Length; i++)
{
    var (prompt, expected, domain) = scenarios[i];

    Console.WriteLine($"── Scenario {i + 1}: {domain} ──────────────────────────────────");
    Console.WriteLine($"  Prompt: \"{prompt}\"");
    Console.WriteLine($"  Expected: {expected}\n");

    // ── Standard Mode ──
    Console.WriteLine($"  📋 Standard Mode (all {mcpTools.Count} tools):");
    var (stdResponse, stdInput, stdOutput, stdTools) = await RunToolLoop(prompt, allChatToolsList, "Standard");
    var stdPass = stdResponse.Contains(expected, StringComparison.OrdinalIgnoreCase);
    var stdIcon = stdPass ? "✅" : "❌";
    Console.WriteLine($"    {stdIcon} Result: \"{Truncate(stdResponse, 80)}\" (tokens: {stdInput + stdOutput:N0})\n");

    // ── Routed Mode ──
    Console.WriteLine($"  🎯 Routed Mode (top 5 tools):");
    var searchResults = await toolIndex.SearchAsync(prompt, topK: 5);
    var selectedNames = searchResults.Select(r => $"{r.Tool.Name} ({r.Score:F3})").ToList();
    Console.WriteLine($"    Selected: {string.Join(", ", selectedNames)}");
    var routedTools = searchResults.Select(r => r.Tool).ToArray();
    var (rtResponse, rtInput, rtOutput, rtTools) = await RunToolLoop(prompt, routedTools, "Routed");
    var rtPass = rtResponse.Contains(expected, StringComparison.OrdinalIgnoreCase);
    var rtIcon = rtPass ? "✅" : "❌";
    Console.WriteLine($"    {rtIcon} Result: \"{Truncate(rtResponse, 80)}\" (tokens: {rtInput + rtOutput:N0})\n");

    var stdTotal = stdInput + stdOutput;
    var rtTotal = rtInput + rtOutput;
    var saved = stdTotal - rtTotal;
    var savedPct = stdTotal > 0 ? (double)saved / stdTotal * 100 : 0;
    Console.WriteLine($"  💾 Saved: {saved:N0} tokens ({savedPct:F1}%)\n");

    results.Add((domain, prompt, expected,
        stdResponse, stdInput, stdOutput, stdTools, stdPass,
        rtResponse, rtInput, rtOutput, rtTools, rtPass,
        string.Join(", ", selectedNames)));
}

// ══════════════════════════════════════════════════════════════
//  SUMMARY
// ══════════════════════════════════════════════════════════════

var totalStdTokens = results.Sum(r => r.StdInput + r.StdOutput);
var totalRtTokens = results.Sum(r => r.RtInput + r.RtOutput);
var totalSaved = totalStdTokens - totalRtTokens;
var totalSavedPct = totalStdTokens > 0 ? (double)totalSaved / totalStdTokens * 100 : 0;
var stdCorrect = results.Count(r => r.StdPass);
var rtCorrect = results.Count(r => r.RtPass);

Console.WriteLine("══════════════════════════════════════════════════════════════");
Console.WriteLine("  📊 Summary");
Console.WriteLine("  ──────────────────────────────────────────────────────────");
Console.WriteLine($"  Total scenarios:     {results.Count}");
Console.WriteLine($"  Standard correct:    {stdCorrect}/{results.Count} {(stdCorrect == results.Count ? "✅" : "⚠️")}");
Console.WriteLine($"  Routed correct:      {rtCorrect}/{results.Count} {(rtCorrect == results.Count ? "✅" : "⚠️")}");
Console.WriteLine($"  Standard tokens:     {totalStdTokens:N0}");
Console.WriteLine($"  Routed tokens:       {totalRtTokens:N0}");
Console.WriteLine($"  Tokens saved:        {totalSaved:N0} ({totalSavedPct:F1}%)");
Console.WriteLine();
if (stdCorrect == results.Count && rtCorrect == results.Count)
    Console.WriteLine("  ✅ Both modes produce identical correct results!");
else
    Console.WriteLine($"  ⚠️ Some scenarios did not match expected results.");
Console.WriteLine($"  🎯 Routed mode saved {totalSavedPct:F1}% of tokens!");
Console.WriteLine("══════════════════════════════════════════════════════════════\n");

// ── Detailed Comparison Table ───────────────────────────────
Console.WriteLine("  📋 Detailed Results:");
Console.WriteLine("  ┌─────┬──────────────┬────────────────┬────────────────┬──────────┐");
Console.WriteLine("  │  #  │ Domain       │ Standard       │ Routed         │ Saved    │");
Console.WriteLine("  ├─────┼──────────────┼────────────────┼────────────────┼──────────┤");

for (int i = 0; i < results.Count; i++)
{
    var r = results[i];
    var sIcon = r.StdPass ? "✅" : "❌";
    var rIcon = r.RtPass ? "✅" : "❌";
    var sTokens = r.StdInput + r.StdOutput;
    var rTokens = r.RtInput + r.RtOutput;
    var saved = sTokens - rTokens;
    var pct = sTokens > 0 ? (double)saved / sTokens * 100 : 0;
    Console.WriteLine($"  │ {i + 1,3} │ {r.Domain,-12} │ {sIcon} {sTokens,9:N0} tk │ {rIcon} {rTokens,9:N0} tk │ {pct,5:F1}%   │");
}

Console.WriteLine("  ├─────┼──────────────┼────────────────┼────────────────┼──────────┤");
Console.WriteLine($"  │ TOT │              │    {totalStdTokens,9:N0} tk │    {totalRtTokens,9:N0} tk │ {totalSavedPct,5:F1}%   │");
Console.WriteLine("  └─────┴──────────────┴────────────────┴────────────────┴──────────┘\n");

Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║              ✅ Validation Complete!                         ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");

static string Truncate(string text, int maxLength)
{
    var singleLine = text.Replace("\n", " ").Replace("\r", "");
    return singleLine.Length <= maxLength ? singleLine : singleLine[..maxLength] + "...";
}
