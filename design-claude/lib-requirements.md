# Library Requirements

<!--
How to use this template:
1. Copy this file to design-claude/lib-requirements.md and fill in the copy.
2. Replace each [TODO: ...] with your own words. If a line doesn't apply, write "None".
3. If you don't know something yet, write "Undecided". Claude will ask about it in Stage 0.
4. Use numbers instead of words like "fast", "simple" or "secure" where you can.

You don't need requirement IDs. Stage 0 assigns them.
-->

## 1. Purpose

- **What the library does:** [TODO: One or two sentences on what it does and the problem it solves.]
The library is used to cache values in an application using redis or some other caching database.
It Implements read and write stratergies mentioned in Readme.md file in the root folder.

- **Why it is needed:** [TODO: Why existing libraries or code are not enough. Name any you considered.]
So that read and write stratergies can be abstracted and implemented and used in the application.
It helps the application to use caching stratergies easily.

## 2. Consumers

- **Who calls it:** [TODO: Your team, other teams or public NuGet users.]
The repository layer of hexagonal architecture will call it to store the values.
- **From what kind of application:** [TODO: ASP.NET Core service, worker, console app, desktop app or another library.]
Any application using hexagonal architecture.

## 3. Capabilities

<!--
Add one heading per capability, copying the block below for each.
Describe what the caller sees, not how to build it.
-->

### [TODO: Writing to cache]

- **The caller provides:** [TODO: Inputs.] Values to be written to cache, and the stratergy to be used for writing.
- **The library does:** It caches the value and implements the stratergy
- **The caller gets back:** sucess or failure if the value is cached.
- **Failures and edge cases:** [TODO: Invalid input, missing data, dependency failures, and what should happen in each case.] Give options and ask questions on it.
- **Example:** [TODO: One concrete case with real values and the expected result.] Find it in the diagram.

### [TODO: reading from cache]

- **The caller provides:** [TODO: Inputs.] Values to be read cache, and the stratergy to be used for reading.
- **The library does:** It reads the value from the caches with the stratergy mentioned
- **The caller gets back:** sucess or failure if the value is not received from cache.
- **Failures and edge cases:** [TODO: Invalid input, missing data, dependency failures, and what should happen in each case.] Ask questions on it. Give options and ask questions on it.
- **Example:** [TODO: One concrete case with real values and the expected result.] Find it in the diagram.

## 4. Example usage

<!-- Show the code you wish you could write. It doesn't need to compile. Write "None" if you have no preference. -->

```csharp
// [TODO: How a consumer would call the library.]
```

## 5. Quality targets


| Area          | Requirement                                                                           |
| ------------- | ------------------------------------------------------------------------------------- |
| Performance   | [TODO: A target and how it is measured, e.g. under 1 ms per read at 1,000 reads/sec.] |
| Limits        | [TODO: Maximum sizes, counts, memory or concurrent callers.]                          |
| Thread-safety | [TODO: Will it be called from many threads at once?]                                  |
| Security      | [TODO: Secrets, sensitive data, and what you need to protect against.]                |
| Compatibility | [TODO: Existing callers or versions that must keep working.]                          |

## 6. External dependencies

<!-- List everything the library uses outside its own code, one per row. Write "None" if there are none. -->


| Dependency                                                             | Used for        | Timeouts, retries, availability |
| ---------------------------------------------------------------------- | --------------- | ------------------------------- |
| [TODO: HTTP API, database, file system, system clock or other service] | [TODO: Purpose] | [TODO: Expected behaviour]      |

## 7. Technical choices

<!--
Change a row only if you already have a view, and fill in Choice when you do.
- Decided: treated as fixed.
- Preferred: your starting point, which Stage 0 may challenge.
- Undecided: Stage 0 will ask you.
-->


| Topic                                             | Status    | Choice |
| ------------------------------------------------- | --------- | ------ |
| Solution and project names                        | Undecided |        |
| Source and test folders                           | Undecided |        |
| Root namespace                                    | Undecided |        |
| Target framework(s)                               | Undecided |        |
| C# language version                               | Undecided |        |
| Nullable reference types (on / off)               | Undecided |        |
| Warnings as errors (yes / no)                     | Undecided |        |
| Distribution (project reference / NuGet)          | Undecided |        |
| Configuration (`appsettings.json` / code / both)  | Undecided |        |
| Microsoft dependency injection (yes / no)         | Undecided |        |
| API style (sync / async)                          | Undecided |        |
| `CancellationToken` support (yes / no)            | Undecided |        |
| Service lifetime (singleton / scoped / transient) | Undecided |        |
| Errors (exceptions / result objects)              | Undecided |        |
| Logging (`ILogger<T>` / none)                     | Undecided |        |
| xUnit and Moq versions                            | Undecided |        |
| Consumer README at the end (yes / no)             | Undecided |        |

## 8. Out of scope

<!-- What the library must not do, including things people might assume it does. -->

- [TODO: Excluded feature or behaviour.]

## 9. Constraints

<!-- Rules from outside the design: company policy, approved packages, licensing, existing code it must fit. -->

- [TODO: Constraint.]

## 10. Open doubts

<!-- Anything you're unsure about or want Claude to challenge. -->

- [TODO: Doubt or question.]
