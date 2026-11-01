---
name: component-design
description: Stage 4 of the lib-designer workflow. Shows the internal structure of the C#/.NET library as a simple SVG component diagram and versioned design document that serve as the blueprint for implementation. Use after the tests are approved, or to answer, approve or change the component design.
disable-model-invocation: true
argument-hint: "[answered | approved | <change request>]"
---

# Stage 4 — Component Design

**Arguments typed after the command:** `$ARGUMENTS` (blank means none)

**Purpose:** show the internal structure of the library as a simple component diagram that serves as the blueprint for implementation.

- **Prerequisite:** Stage 3 approved.
- **Folder:** `design-claude/4-component-design/`
- **Documents:** `component-design-v<N>.md`, from the skeleton in [template.md](template.md), and `component-diagram-v<N>.svg`
- **Next command after approval:** `/lib-designer:implement`

## Procedure

Follow these steps on every invocation.

1. Run the prerequisite check (3.6).
2. Combine the arguments with the current state and take that action (3.8).
3. When the action starts or continues the stage work, follow the stage steps below.

## Stage steps

1. Create `design-claude/4-component-design/` if it does not exist.
2. Ask questions in the document if needed (3.4).
3. Create `component-diagram-v<N>.svg` (hand-written SVG) and `component-design-v<N>.md`, which embeds it with `![Component diagram](component-diagram-v<N>.svg)`. The SVG and the document always share the same version number; every new document version gets its own SVG file, copied unchanged if the diagram did not change.
4. Follow these diagram rules (keep it simple):
   - Use at most about 10 components, one box each. Draw the library boundary as a container; place the client application and external systems outside it.
   - Arrows show call / dependency direction, each with a short label. Avoid crossing lines.
   - Use plain shapes and text only: readable font size (12 px or larger), white background, `viewBox` set, no scripts, no external images or fonts, no gradients or shadows.
   - Add a legend if more than one box or arrow style is used.
   - Verify the SVG is well-formed XML before finishing, by running this command, which works from both PowerShell and Git Bash and exits with an error if the XML is malformed:

     ```text
     powershell -NoProfile -Command '[xml](Get-Content -Raw -Encoding UTF8 -LiteralPath design-claude/4-component-design/component-diagram-v<N>.svg) | Out-Null'
     ```
5. The document contains:
   - The embedded diagram.
   - Component table: name, responsibility (one sentence), public / internal, depends on, requirement IDs.
   - Main flows: numbered steps showing how each key public operation passes through the components.
   - Mapping of each component to its planned classes, namespaces and folders (Stage 5 follows this).
   - How configuration, logging, errors and threading are handled across components, per the TDs.
6. Do not recommend new features and do not add components the approved requirements, API and tests don't need. Internal components must fit the approved constructors, because Stage 5 cannot change the public API or the tests. Put any concern in Open Questions.
7. Tell the user the file paths and what you need from them. **STOP and wait for the user.** Each change request produces a new version of both files. Approval per 3.5.

## Shared Conventions

These conventions apply to every stage of the workflow. Section numbers are referred to throughout this skill as (3.1) to (3.10).

### Context

- **Stack:** C# / .NET, built and tested with the `dotnet` CLI. Tests use **xUnit** and **Moq**.
- **OS:** Windows. Every command must work in both PowerShell and Git Bash. Use the `dotnet` CLI for builds and tests and Claude Code's file tools for reading, searching and writing files. Do not use Unix-only tools such as `grep`, `sed`, `awk`, `find` or `cat`. Run one command per call; do not chain with `&&`, which Windows PowerShell 5.1 rejects. Write paths with forward slashes in documents.
- **Root:** all paths are relative to the project folder Claude Code is opened in.
- **Design workspace:** `design-claude/`. The user writes the requirements in `design-claude/lib-requirements.md`. Read this file; never modify it.
- **Source code** goes where Stage 0 decides, never inside `design-claude/`.

### 3.1 Workspace layout

```text
design-claude/
├── lib-requirements.md             ← written by the user; read-only for Claude
├── workflow-status.md              ← maintained by the skills
├── 0-analysis/          analysis-v1.md, analysis-v2.md, …
├── 1-configuration/     configuration-v1.md, …
├── 2-api-methods/       api-methods-v1.md, …
├── 3-tests/             test-plan-v1.md, …
├── 4-component-design/  component-design-v1.md + component-diagram-v1.svg, …
└── 5-implementation/    implementation-notes-v1.md, …
```

Create a stage's folder when the stage starts, if it does not exist.

### 3.2 Versioning

- Name files `<doc-name>-v<N>.md` with N = 1, 2, 3 … The latest version is the highest N compared **as a number** (v10 is newer than v9).
- Never overwrite or delete an existing version. Every change (a modification request, new follow-up questions, incorporating answers) produces a new, complete file `v<N+1>`, not a diff.
- **Only exception:** when the user approves a version, update just its `Status` and `Approved on` header lines.
- Never alter text the user wrote (answers, comments) when carrying it into a new version. Carry answers not yet processed into the new version unchanged.

### 3.3 Document header (every document)

```markdown
# <Title>

| Field       | Value                                                    |
|-------------|----------------------------------------------------------|
| Stage       | 0 – Analysis                                             |
| Version     | v3                                                       |
| Date        | YYYY-MM-DD                                               |
| Status      | Draft / Awaiting answers / Awaiting approval / Approved  |
| Approved on | —                                                        |
| Based on    | analysis-v4.md (Approved), configuration-v2.md (Approved)|
| Supersedes  | v2                                                       |

## Changelog
- v3: <what changed compared with v2, and why>
```

Set `Status` to **Awaiting answers** when the document has an open [Blocking] or [Important] question, otherwise to **Awaiting approval**. Use **Draft** only while a file is still being written.

### 3.4 Questions and answers

Ask every design question **inside the document**, in an `## Open Questions` section, not as a chat or interactive prompt. Use this format:

```markdown
### Q-012 [Blocking] Target framework
**Why it matters:** decides which APIs and language features are available.
**Options:** a) net10.0  b) netstandard2.0 (widest compatibility)  c) multi-target both
**Suggested:** a — <one-line reason>   (include only when there is a sensible default)
**Answer:**
>
```

Rules:

- Tag each question with a priority: **[Blocking]** (cannot proceed without it), **[Important]** or **[Minor]**.
- Keep question IDs unique within a stage and never reuse them. Follow-ups name their parent: `Q-015 (follow-up to Q-012)`.
- Ask one decision per question. Do not ask anything the requirements or earlier approved documents already answer.
- After writing questions, count the `### Q-` headings under `## Open Questions`, then tell the user the file path and how many questions there are, and that they should answer in the file and then run the same command with `answered`. **STOP and wait for the user.**
- The user may also answer in chat. Record chat answers verbatim in the next version, marked "(answered in chat)".
- When the user says they have answered, read the answers in the latest version and classify each as **resolved**, **unclear** (ask a follow-up) or **unanswered** (keep open). Create `v<N+1>` that:
  - moves resolved items to `## Decisions` (question, the user's answer verbatim, your one-line interpretation),
  - keeps unanswered questions open,
  - adds follow-up questions.
- Repeat until no [Blocking] or [Important] question is open.
- Close an open [Minor] question with its suggested default only if the user agrees, and record it under `## Assumptions` in a new version.

### 3.5 Approval gate

- A stage is complete only when the user explicitly approves a specific version: "approved", "approve v3", or running the command with `approved`. `approved` on its own means the latest version. "approve v<N>" means that version, even if it is not the newest; later files stay unapproved and the status file shows both, for example Latest v4, Approved v3.
- If the reply is ambiguous (for example "looks fine, but …"), ask whether to mark it approved. **STOP and wait for the user.**
- Do not approve a version that has an open [Blocking] or [Important] question. Say which questions are open. **STOP and wait for the user.**
- Do not approve a version whose `Based on` line names an outdated upstream version. Produce `v<N+1>` against the current approved upstream documents, say why, and **STOP and wait for the user.**
- On approval:
  1. Update the header of that version (3.2 exception).
  2. Update `workflow-status.md` (3.7). When this is a newly approved version, apply 3.9.
  3. Tell the user the exact next command.
- Never start the next stage automatically.

### 3.6 Prerequisite check (first step of every stage skill)

1. Read `design-claude/workflow-status.md`. If it is missing, create it with every stage "Not started".
2. Confirm the previous stage is **Approved** (Stage 0 has no previous stage, so skip this step there). If not, tell the user which stage is pending and which command to run. **STOP and wait for the user.**
3. Read `design-claude/lib-requirements.md` and the **approved** version of every earlier stage document (the version recorded as approved in the status file, which is not necessarily the newest file).
4. Determine this stage's current state from the status file and its latest document, and resume from there (3.8) instead of starting over.

### 3.7 `workflow-status.md` format

```markdown
# Workflow Status

| Stage              | Folder              | Latest | Approved | State            | Updated    |
|--------------------|---------------------|--------|----------|------------------|------------|
| 0 Analysis         | 0-analysis          | v3     | v3       | Approved         | 2026-11-02 |
| 1 Configuration    | 1-configuration     | v1     | —        | Awaiting answers | 2026-11-02 |
| 2 API methods      | 2-api-methods       | —      | —        | Not started      | —          |
| 3 Tests            | 3-tests             | —      | —        | Not started      | —          |
| 4 Component design | 4-component-design  | —      | —        | Not started      | —          |
| 5 Implementation   | 5-implementation    | —      | —        | Not started      | —          |

## Log
- 2026-11-02 Stage 0: v3 approved.
```

States: **Not started**, **Awaiting answers**, **Awaiting approval**, **Approved**, **Needs re-review**.

Each time you write a document version or approve one, update that stage's row and add a log line. While a new version of an approved stage is under review, its state follows the new version and `Approved` keeps the previously approved version.

Commands and documents per stage:

| Stage | Command                        | Document                                                |
|-------|--------------------------------|---------------------------------------------------------|
| 0     | `/lib-designer:analysis`         | `0-analysis/analysis-v<N>.md`                           |
| 1     | `/lib-designer:configuration`    | `1-configuration/configuration-v<N>.md`                 |
| 2     | `/lib-designer:api-design`       | `2-api-methods/api-methods-v<N>.md`                     |
| 3     | `/lib-designer:tests`            | `3-tests/test-plan-v<N>.md`                             |
| 4     | `/lib-designer:component-design` | `4-component-design/component-design-v<N>.md`           |
| 5     | `/lib-designer:implement`        | `5-implementation/implementation-notes-v<N>.md`         |

### 3.8 Resuming and arguments

On every invocation, combine the arguments with the current state:

| Arguments                 | Action                                                                                    |
|---------------------------|-------------------------------------------------------------------------------------------|
| empty, Not started        | Start the stage.                                                                          |
| empty, in progress        | Summarize the current state and say what you are waiting for.                             |
| empty, Approved           | Report the approved version and the exact next command.                                   |
| empty, Needs re-review    | Re-check the stage against the new upstream documents (3.9).                              |
| `answered`                | Process the answers (3.4). If the latest version has no open questions, say so and stop.  |
| `approved`                | Approve the latest version (3.5). In Needs re-review, re-check first (3.9).               |
| `approve v<N>`            | Approve that version (3.5). In Needs re-review, re-check first (3.9).                     |
| any other text            | Treat it as a modification request and produce a new version (3.2).                       |

If the stage is Not started and the arguments are `answered`, `approved` or `approve v<N>`, there is nothing to process: say so and stop. If it is Not started and the arguments are any other text, start the stage and take the text into account.

### 3.9 Changes to an earlier, approved stage

- If the user asks this stage to change an earlier approved stage, or you find that this stage cannot be completed without changing an approved decision: explain the conflict and tell the user which stage command to run with which change. **STOP and wait for the user.** Never silently change an earlier decision.
- When an earlier stage gets a newly approved version, mark every later stage that was already approved as **Needs re-review**.
- A later stage that is still in progress (Awaiting answers or Awaiting approval) keeps its state. On its next run, if the `Based on` line of its latest document names an outdated upstream version, tell the user and use the new upstream version in its next document version. Keep pending answers.
- On its next run, a stage in **Needs re-review** re-checks itself against the new upstream documents:
  - If anything changes, produce a new version based on them. The state follows the new version.
  - If nothing changes, produce no new version. Tell the user the approved version still holds and ask them to reconfirm with `approved`. **STOP and wait for the user.** On `approved`, if the re-check still finds nothing to change, set the state back to **Approved** and add a log line naming the upstream versions checked. Leave the document unchanged. A reconfirmation is not a newly approved version, so it does not mark later stages.

### 3.10 Scope discipline and traceability

- Design only what the approved requirements need. Do not propose new features, nice-to-haves or "future" extension points. If something seems missing, ask it as a question; do not add it.
- Stage 0 gives every requirement an ID (FR-001, NFR-001) and every technical decision an ID (TD-001). Every later document references these IDs, so each design element and each test traces back to a requirement.
