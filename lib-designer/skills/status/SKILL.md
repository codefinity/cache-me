---
name: status
description: Read-only report of the lib-designer workflow for a C#/.NET library. Shows each stage's latest version, approved version, state, what it is waiting for and the exact next command. Use when the user asks "where are we?" or about design progress, or what to do next.
---

# Workflow Status Report

**Purpose:** report where the lib-designer workflow stands. This skill never creates or changes files.

Paths are relative to the project folder Claude Code is opened in. Use Claude Code's file tools, not shell commands.

## Stages

| Stage              | Folder                             | Document pattern               | Command                          |
|--------------------|------------------------------------|--------------------------------|----------------------------------|
| 0 Analysis         | `design-claude/0-analysis`         | `analysis-v<N>.md`             | `/lib-designer:analysis`         |
| 1 Configuration    | `design-claude/1-configuration`    | `configuration-v<N>.md`        | `/lib-designer:configuration`    |
| 2 API methods      | `design-claude/2-api-methods`      | `api-methods-v<N>.md`          | `/lib-designer:api-design`       |
| 3 Tests            | `design-claude/3-tests`            | `test-plan-v<N>.md`            | `/lib-designer:tests`            |
| 4 Component design | `design-claude/4-component-design` | `component-design-v<N>.md`     | `/lib-designer:component-design` |
| 5 Implementation   | `design-claude/5-implementation`   | `implementation-notes-v<N>.md` | `/lib-designer:implement`        |

States: Not started, Awaiting answers, Awaiting approval, Approved, Needs re-review.

## Steps

1. Read `design-claude/workflow-status.md`. If it is missing, check `design-claude/lib-requirements.md` and the stage folders:
   - If stage documents exist, report the missing status file as a discrepancy, and report what is on disk.
   - Otherwise say the workflow has not started. If the requirements file is missing or empty, tell the user to write it first. Give `/lib-designer:analysis` as the next command.
2. For each stage, list its folder and find the highest document version, comparing N **as a number** (v10 is newer than v9).
3. Read the header of the latest document and of the version recorded as approved. In the latest document, count the open questions by priority.
4. Compare the status file with the files on disk and note every discrepancy, including:
   - `Latest` differs from the highest version on disk.
   - A recorded version file does not exist.
   - `Approved` names a version whose header does not say Approved, or a stage with an Approved header has no matching entry in `Approved`.
   - `State` does not fit the latest document's `Status` (Needs re-review is expected to differ).
   - Stage 4 has a document version without an SVG of the same version, or an SVG without a document.
   - A stage has documents although an earlier stage has never been approved.
   - A stage's latest `Based on` line names an upstream version older than the approved one.
5. Determine what each stage is waiting for:
   - **Not started:** the previous stage's approval, or the user running the command if that stage is approved.
   - **Awaiting answers:** the user answering the questions in the latest file, then running the command with `answered`.
   - **Awaiting approval:** the user reviewing the latest file, then running the command with `approved` or with a change request.
   - **Approved:** nothing. If a newer unapproved file exists, say so.
   - **Needs re-review:** the user running the command so the stage re-checks itself against the new upstream documents.
6. Report a table with one row per stage: stage, latest version, approved version, state, waiting for.
7. List any discrepancies under a separate heading. Do not fix them, and do not choose a side silently; say what the status file records and what is on disk.
8. End with the exact next command to run: the earliest stage that is not Approved, with `answered`, `approved` or nothing after it as its state requires. If every stage is Approved, say the workflow is complete.
