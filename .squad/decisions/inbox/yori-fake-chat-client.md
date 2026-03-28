# Decision: FakeChatClient Pattern for LLM-Dependent Tests

**Date:** 2025-XX-XX  
**Author:** Yori (Tester/QA)  
**Status:** Implemented

## Context

The new `PromptDistiller` and `ToolRouter` classes depend on `IChatClient` for LLM-powered prompt distillation. Tests must avoid real LLM calls (network dependency, cost, non-determinism).

## Decision

Use a `FakeChatClient` implementing `IChatClient` that returns predetermined responses and optionally captures the messages it receives. This is defined as a private nested class inside each test file that needs it.

## Rationale

- **No external mocking framework needed** — keeps test dependencies minimal (xUnit only)
- **Message capturing** enables verifying system prompts and user messages are forwarded correctly
- **Deterministic** — fixed responses allow precise assertion on fallback behavior, trimming, etc.

## Pattern

```csharp
private class FakeChatClient : IChatClient
{
    private readonly string _response;
    public IList<ChatMessage>? LastMessages { get; private set; }
    public FakeChatClient(string response) => _response = response;
    // ... implements GetResponseAsync, captures messages
}
```

## Impact

- No new package dependencies for mocking
- Pattern can be reused for any future IChatClient-dependent tests
- If a full mocking framework is adopted later, these fakes can be replaced
