// ============================================================================
// ScenarioResult — captures the outcome of a single Mode 1 vs Mode 2 run.
// ============================================================================

record ScenarioResult(
    string Name,
    string Prompt,
    int WordCount,
    string DistilledPrompt,
    int Mode1Hits,
    int Mode2Hits,
    string Winner,
    long Mode1Ms,
    long Mode2Ms,
    long DistillMs,
    List<(string Name, float Score)> Mode1Tools,
    List<(string Name, float Score)> Mode2Tools);
