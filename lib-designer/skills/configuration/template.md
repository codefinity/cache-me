# Library Configuration

| Field       | Value                                                    |
|-------------|----------------------------------------------------------|
| Stage       | 1 – Configuration                                        |
| Version     | v<N>                                                     |
| Date        | YYYY-MM-DD                                               |
| Status      | Draft / Awaiting answers / Awaiting approval / Approved  |
| Approved on | —                                                        |
| Based on    | analysis-v<N>.md (Approved)                              |
| Supersedes  | v<N-1> (— for v1)                                        |

## Changelog
- v<N>: <what changed compared with v<N-1>, and why>

## Summary of the configuration approach
<The approach, per TD-xxx.>

## Options reference
| Key               | C# property  | Type   | Required | Default | Validation / allowed values | Description | Requirement |
|-------------------|--------------|--------|----------|---------|-----------------------------|-------------|-------------|
| `MyLib:Section:Key` | `Section.Key` | <type> | yes/no | <value> | <rule>                      | <text>      | FR-001      |

## Example `appsettings.json`
```json
{
  "MyLib": {
  }
}
```

## Client registration
```csharp
// Registration code matching the TDs.
```

## Options classes
Shown here only; the code is created in Stage 2.

```csharp
public sealed class MyLibOptions
{
}
```

## Validation
| Rule   | When it runs           | On invalid configuration   | Requirement |
|--------|------------------------|----------------------------|-------------|
| <rule> | startup / first use    | <exception type: message>  | FR-001      |

## Secrets and environment-specific overrides
<Only if relevant per the TDs; otherwise delete this section.>

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
