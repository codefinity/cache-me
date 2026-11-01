# Library Public API

| Field       | Value                                                    |
|-------------|----------------------------------------------------------|
| Stage       | 2 – API methods                                          |
| Version     | v<N>                                                     |
| Date        | YYYY-MM-DD                                               |
| Status      | Draft / Awaiting answers / Awaiting approval / Approved  |
| Approved on | —                                                        |
| Based on    | analysis-v<N>.md (Approved), configuration-v<N>.md (Approved) |
| Supersedes  | v<N-1> (— for v1)                                        |

## Changelog
- v<N>: <what changed compared with v<N-1>, and why>

## Public interfaces and classes
```csharp
public interface IMyLibClient
{
}

public sealed class MyLibClient : IMyLibClient
{
}
```

## Members

### `<C# signature>`
- **Purpose:** <one sentence>
- **Parameters:** <name: meaning, constraints>
- **Returns:** <return value>
- **Exceptions:** <type: when it is thrown>
- **Async / cancellation:** <behaviour>
- **Thread-safety:** <behaviour>
- **Requirements:** FR-001, TD-001

```csharp
// Short usage example.
```

## Models, enums and exception types
```csharp
// Public DTOs, enums and custom exception types.
```

## Options and DI registration
```csharp
// Options classes from Stage 1 and the DI extension method signature(s).
```

## Dependencies (seams)
| Interface | External concern | Visibility                 | Constructor used by tests and DI | Requirement |
|-----------|------------------|----------------------------|----------------------------------|-------------|
| `IExample` | <concern>       | public / internal          | <constructor>                    | TD-001      |

If any seam is internal, also state the `InternalsVisibleTo` entries: the test assembly and `DynamicProxyGenAssembly2`.

## Client usage example
```csharp
// The end-to-end code a consumer would write.
```

## Coverage
| Requirement | API member(s) |
|-------------|---------------|
| FR-001      | <member>      |

**FRs with no member:** none
**Members with no FR:** none

## Assumptions
- <Closed [Minor] questions, with the user's agreement.>

## Open Questions

### Q-001 [Blocking] <Short title>
**Why it matters:** <what this decides>
**Options:** a) <option>  b) <option>
**Suggested:** a — <one-line reason>   (include only when there is a sensible default)
**Answer:**
>

## Decisions

### Q-000 <Short title>
**Question:** <the question>
**Answer:** <the user's answer, verbatim>
**Interpretation:** <your one-line interpretation>
