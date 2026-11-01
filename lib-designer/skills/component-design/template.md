# Library Component Design

| Field       | Value                                                    |
|-------------|----------------------------------------------------------|
| Stage       | 4 – Component design                                     |
| Version     | v<N>                                                     |
| Date        | YYYY-MM-DD                                               |
| Status      | Draft / Awaiting answers / Awaiting approval / Approved  |
| Approved on | —                                                        |
| Based on    | analysis-v<N>.md (Approved), configuration-v<N>.md (Approved), api-methods-v<N>.md (Approved), test-plan-v<N>.md (Approved) |
| Supersedes  | v<N-1> (— for v1)                                        |

## Changelog
- v<N>: <what changed compared with v<N-1>, and why>

## Component diagram
![Component diagram](component-diagram-v<N>.svg)

## Components
| Name        | Responsibility | Public / internal | Depends on   | Requirements |
|-------------|----------------|-------------------|--------------|--------------|
| <component> | <one sentence> | public / internal | <components> | FR-001       |

## Main flows

### <Public operation>
1. <Step through a component.>

## Component to code mapping
| Component   | Classes   | Namespace   | Folder   |
|-------------|-----------|-------------|----------|
| <component> | <classes> | <namespace> | <folder> |

## Cross-cutting concerns
- **Configuration:** <per TD-xxx>
- **Logging:** <per TD-xxx>
- **Errors:** <per TD-xxx>
- **Threading:** <per TD-xxx>

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
