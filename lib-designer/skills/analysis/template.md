# Library Analysis

| Field       | Value                                                    |
|-------------|----------------------------------------------------------|
| Stage       | 0 – Analysis                                             |
| Version     | v<N>                                                     |
| Date        | YYYY-MM-DD                                               |
| Status      | Draft / Awaiting answers / Awaiting approval / Approved  |
| Approved on | —                                                        |
| Based on    | lib-requirements.md                                      |
| Supersedes  | v<N-1> (— for v1)                                        |

## Changelog
- v<N>: <what changed compared with v<N-1>, and why>

## Requirements as understood
<The requirements restated in your own words.>

## Assumptions
- <Assumption, and why it is reasonable. Closed [Minor] questions go here, with the user's agreement.>

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

## Out of scope
- <What the library explicitly does not do. Moves into the brief in the final version.>

<!-- Add the section below only in the final version, then set Status to "Awaiting approval". -->

## Library Design Brief

### Purpose and consumers
<Why the library exists, who calls it and from what kind of application.>

### Functional requirements
| ID     | Requirement   | Source                               |
|--------|---------------|--------------------------------------|
| FR-001 | <requirement> | lib-requirements.md / Q-000          |

### Non-functional requirements
| ID      | Requirement                   | Measure or limit | Source                      |
|---------|-------------------------------|------------------|-----------------------------|
| NFR-001 | <requirement>                 | <target>         | lib-requirements.md / Q-000 |

### Technical decisions
| ID     | Topic                                            | Decision   | Source                      |
|--------|--------------------------------------------------|------------|-----------------------------|
| TD-001 | Solution and project names, source and test folders, root namespace | <decision> | Q-000 |
| TD-002 | Target framework(s), C# version, nullable, warnings-as-errors | <decision> | Q-000 |
| TD-003 | Distribution and package metadata                | <decision> | Q-000 |
| TD-004 | Configuration approach and dependency injection  | <decision> | Q-000 |
| TD-005 | Sync vs async, `CancellationToken`               | <decision> | Q-000 |
| TD-006 | Thread-safety and service lifetime               | <decision> | Q-000 |
| TD-007 | Error handling and input validation              | <decision> | Q-000 |
| TD-008 | Logging and diagnostics                          | <decision> | Q-000 |
| TD-009 | External dependencies (the Stage 2 seams)        | <decision> | Q-000 |
| TD-010 | Performance targets, limits, resource constraints | <decision> | Q-000 |
| TD-011 | Security and secrets handling                    | <decision> | Q-000 |
| TD-012 | Backward compatibility and versioning            | <decision> | Q-000 |
| TD-013 | xUnit and Moq versions                           | <decision> | Q-000 |
| TD-014 | Consumer-facing README wanted                    | <decision> | Q-000 |

### Constraints
- <Constraint>

### Out of scope
- <What the library explicitly does not do>

### Glossary
| Term   | Meaning   |
|--------|-----------|
| <term> | <meaning> |
