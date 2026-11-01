# lib-designer

A Claude Code plugin that guides you, one stage at a time, through designing and building a C#/.NET class library, from requirements analysis to working, tested code.

Each stage is a skill you start yourself. It writes versioned Markdown documents for you to review, and it never moves to the next stage until you approve. The plugin contains only skills: no hooks, agents or MCP servers.

## Prerequisites

- [Claude Code](https://code.claude.com/docs)
- [.NET SDK](https://dotnet.microsoft.com/download), with `dotnet` on your `PATH`

The skills are written for Windows and work from both PowerShell and Git Bash.

## Load the plugin for testing

From the project folder that contains `lib-designer/`, run:

```text
claude --plugin-dir ./lib-designer
```

After editing the plugin, run `/reload-plugins` inside Claude Code to pick up the changes.

## Workflow

Before you start, write your requirements in `design-claude/lib-requirements.md`. The plugin reads this file but never changes it.

| Stage | Command | Output folder (under `design-claude/`) | What you do |
|-------|---------|----------------------------------------|-------------|
| 0 Analysis | `/lib-designer:analysis` | `0-analysis/` | Answer the questions that challenge your requirements, then approve the Library Design Brief. |
| 1 Configuration | `/lib-designer:configuration` | `1-configuration/` | Review how client applications will configure the library, then approve. |
| 2 API design | `/lib-designer:api-design` | `2-api-methods/` | Review the public API, then approve. Approval creates compiling stubs in your project. |
| 3 Tests | `/lib-designer:tests` | `3-tests/` | Review the test plan and the failing tests, then approve. |
| 4 Component design | `/lib-designer:component-design` | `4-component-design/` | Review the component diagram and internal design, then approve. |
| 5 Implementation | `/lib-designer:implement` | `5-implementation/` | Review the finished code and notes, then approve to complete the workflow. |

To see where things stand at any point, run `/lib-designer:status` or ask "where are we?". It reports each stage and the next command, and it never changes any files.

## Working with a stage

Run a stage's command again, with one of these after it:

| Command | What it does |
|---------|--------------|
| `/lib-designer:analysis` | Starts the stage, or summarizes where it stands and what it is waiting for. |
| `/lib-designer:analysis answered` | Processes the answers you wrote in the latest document. |
| `/lib-designer:analysis approved` | Approves the latest version. |
| `/lib-designer:analysis approve v3` | Approves that version, even if a newer file exists. The newer file stays unapproved. |
| `/lib-designer:analysis <your change>` | Treats the text as a change request and writes a new version. |

**Answer questions** in the document, under each question's `**Answer:**` line, then run the command with `answered`. You can also answer in chat, and your words are recorded verbatim in the next version. Questions are tagged [Blocking], [Important] or [Minor]. A version can't be approved while a [Blocking] or [Important] question is open.

**Resume** by running the stage's command. Each skill works out the current state from the status file and the latest document, so you can stop and come back in a new session.

**Change an approved stage** by running that stage's command with your change. When the new version is approved, later stages that were already approved are marked **Needs re-review**. Run each one in order: it re-checks itself against the new documents and writes a new version if anything changes. If nothing changes, it asks you to reconfirm with `approved`. A later stage that is still in progress keeps its state and uses the new documents in its next version.

## Complete flow

Every stage follows the same loop, and only your approval ends it:

```text
Run the stage command
        │
        ▼
Claude writes <doc-name>-v<N>.md and stops  ◄───────────────┐
        │                                                   │
        ├─ Questions to answer: answer, then `answered` ────┤
        ├─ Something to change: run with your change ───────┘
        │
        └─ Happy with it: run with `approved`
                │
                ▼
Claude records the approval and gives you the next command
```

The only stages that change your code are 2 (stubs, after you approve), 3 (tests) and 5 (implementation).

## Example: building a small cache library

This walkthrough builds an imaginary library called `TinyCache`. The names, questions and test counts are illustrative; yours depend on your requirements and answers.

### Before you start

Create `design-claude/lib-requirements.md` in your project folder and describe what you need in your own words:

```markdown
# Requirements
An in-memory cache library for our .NET services.
Values are stored by key, and entries expire. It should be fast and simple.
```

Then start Claude Code from the project folder with the plugin loaded:

```text
claude --plugin-dir ./lib-designer
```

### Stage 0: Analysis

Run:

```text
/lib-designer:analysis
```

Claude reads your requirements, checks the repository and writes `design-claude/0-analysis/analysis-v1.md`. It restates the requirements and challenges vague words such as "fast". Then it stops and tells you how many questions are open.

Open the file and write your answer on the line under each `**Answer:**`:

```markdown
### Q-003 [Blocking] What does "fast" mean?
**Why it matters:** decides the data structures and whether contention needs special handling.
**Options:** a) under 1 ms per read at 1,000 reads/sec  b) no measurable target
**Suggested:** a — gives the tests a limit to check
**Answer:**
> a, but measured on our production VM size.
```

Then run:

```text
/lib-designer:analysis answered
```

Claude writes `analysis-v2.md`. Resolved questions move to `## Decisions` with your words unchanged, and anything unclear becomes a follow-up such as `Q-018 (follow-up to Q-003)`. Repeat until no [Blocking] or [Important] question is open.

Claude then writes the final version, here `analysis-v3.md`, containing the **Library Design Brief**: requirements with IDs (`FR-001`, `NFR-001`), technical decisions (`TD-001`), constraints, out-of-scope items and a glossary. Review it and run one of these:

```text
/lib-designer:analysis Entries also need a per-item expiry, not just a global one.
/lib-designer:analysis approved
```

The first writes `analysis-v4.md` with your change. The second approves the latest version and tells you the next command.

### Stage 1: Configuration

```text
/lib-designer:configuration
```

Claude writes `configuration-v1.md`, with a table of every option, an example `appsettings.json` section, the client registration code and the validation rules:

```json
{
  "TinyCache": {
    "DefaultTtlSeconds": 300,
    "MaxEntries": 10000
  }
}
```

If there are no questions, the document goes straight to review. When you're happy with it, run:

```text
/lib-designer:configuration approved
```

### Stage 2: API design

```text
/lib-designer:api-design
```

Claude writes `api-methods-v1.md`, which covers every public member, its exceptions and the requirements it serves. It also lists the interfaces the tests will mock, such as `IClock`. A coverage table flags any requirement with no API member, and any member with no requirement.

```text
/lib-designer:api-design approved
```

Approval also creates the code: the solution and projects if they don't exist, plus every approved type as a stub. Claude runs `dotnet build`, which must succeed, and lists the files it created.

```csharp
public bool TryGet(string key, out string? value)
{
    throw new NotImplementedException();
}
```

### Stage 3: Tests

```text
/lib-designer:tests
```

Claude creates the test project and writes xUnit tests against the real class, mocking only its dependencies with Moq. Each test is tagged with the requirement it covers:

```csharp
[Fact]
[Trait("Requirement", "FR-002")]
public void TryGet_ExpiredEntry_ReturnsFalse()
```

**The tests are supposed to fail at this stage**, because nothing is implemented yet. `test-plan-v1.md` records the baseline and explains any test that already passes:

| Result | Count |
|--------|-------|
| Total | 24 |
| Failing as expected: `NotImplementedException` | 22 |
| Failing as expected: stub constructor has no logic | 2 |
| Passing | 0 |
| Unexpected failures (must be 0) | 0 |

```text
/lib-designer:tests approved
```

### Stage 4: Component design

```text
/lib-designer:component-design
```

Claude writes `component-design-v1.md` and `component-diagram-v1.svg`. Together they show the internal components, how each public operation flows through them, and which classes and folders each component maps to. Open the document in a Markdown preview to see the diagram.

```text
/lib-designer:component-design approved
```

### Stage 5: Implementation

```text
/lib-designer:implement
```

Claude implements the design and loops on `dotnet build` and `dotnet test` until every test passes. It can't change the public API or add features. If a test looks wrong, it stops and asks you instead of changing the test.

Review the code and `implementation-notes-v1.md`, which records the final test results, any deviations from the design and known limitations. Then run:

```text
/lib-designer:implement approved
```

This approval completes the workflow.

### Checking progress

Run `/lib-designer:status`, or ask "where are we?", at any point:

```text
| Stage              | Latest | Approved | State             | Waiting for             |
|--------------------|--------|----------|-------------------|-------------------------|
| 0 Analysis         | v4     | v4       | Approved          | Nothing                 |
| 1 Configuration    | v1     | v1       | Approved          | Nothing                 |
| 2 API methods      | v2     | —        | Awaiting approval | Your review of v2       |
| 3 Tests            | —      | —        | Not started       | Stage 2 approval        |
| 4 Component design | —      | —        | Not started       | Stage 3 approval        |
| 5 Implementation   | —      | —        | Not started       | Stage 4 approval        |

Next command: /lib-designer:api-design approved
```

### Changing your mind later

Suppose Stage 3 is approved and you decide the cache must report hit and miss counts. Go back to the stage that owns the decision:

```text
/lib-designer:analysis Add a requirement: the cache must expose hit and miss counts.
/lib-designer:analysis approved
```

Approving the new version marks Stages 1, 2 and 3 as **Needs re-review**. Run them in order, because each one needs the previous stage to be approved:

```text
/lib-designer:configuration
/lib-designer:api-design
/lib-designer:tests
```

Each stage re-checks itself against the new documents:

- **Something changes:** it writes a new version for you to review and approve. Approving a new API version also updates the stubs in your code.
- **Nothing changes:** it writes no new version and asks you to reconfirm with `approved`.

Once Stage 3 is approved again, continue with Stage 4 as usual.

## Versioning

Documents are named `<doc-name>-v<N>.md`. Every change produces a new, complete file, and existing versions are never overwritten or deleted. The only edit to an existing version is setting its `Status` and `Approved on` header lines when you approve it. Your answers and comments are carried into new versions unchanged.

## `workflow-status.md`

The skills maintain `design-claude/workflow-status.md`, which has one row per stage and a dated log:

| Column | Meaning |
|--------|---------|
| Latest | Highest version on disk. |
| Approved | The version later stages build on. It isn't always the latest. |
| State | Not started, Awaiting answers, Awaiting approval, Approved or Needs re-review. |
| Updated | Date of the last change to that stage. |

A stage starts only when the previous stage's state is Approved. If the status file and the files on disk disagree, `/lib-designer:status` reports the difference and doesn't fix it.
