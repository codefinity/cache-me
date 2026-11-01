# Library Test Plan

| Field       | Value                                                    |
|-------------|----------------------------------------------------------|
| Stage       | 3 – Tests                                                |
| Version     | v<N>                                                     |
| Date        | YYYY-MM-DD                                               |
| Status      | Draft / Awaiting answers / Awaiting approval / Approved  |
| Approved on | —                                                        |
| Based on    | analysis-v<N>.md (Approved), configuration-v<N>.md (Approved), api-methods-v<N>.md (Approved) |
| Supersedes  | v<N-1> (— for v1)                                        |

## Changelog
- v<N>: <what changed compared with v<N-1>, and why>

## Test project
- **Path:** <path/to/Project.Tests>
- **Packages:** <package and version, one per item>
- **Run:** `dotnet test <solution or project>`

## Mocking approach
| Seam       | Mocked with    | Typical setup   |
|------------|----------------|-----------------|
| `IExample` | `Mock<IExample>` | <how it is set up> |

## Traceability matrix
| Requirement | Tests       |
|-------------|-------------|
| FR-001      | <test name> |
| NFR-001     | <test name, or see Gaps> |

## Test catalogue

### `<Class.Method>`
| Test name                        | Scenario   | Mocks set up | Expected result |
|----------------------------------|------------|--------------|-----------------|
| `Method_Scenario_ExpectedResult` | <scenario> | <setup>      | <result>        |

## Baseline run result
| Result                                                    | Count |
|-----------------------------------------------------------|-------|
| Total                                                     | <n>   |
| Failing as expected: `NotImplementedException`            | <n>   |
| Failing as expected: stub constructor has no logic        | <n>   |
| Passing (each explained below)                            | <n>   |
| Unexpected failures (must be 0)                           | 0     |

**Stub constructor tests:** <test names>

**Passing tests and why:** <test name: explanation>

## Gaps
| Requirement | Why it could not be tested here |
|-------------|---------------------------------|
| <ID>        | <reason>                        |

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
