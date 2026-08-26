---
name: conventional-commits
description: Write Conventional Commit messages for this repository - header format, allowed types, scopes drawn from the actual project layout, imperative subject, body and footer rules, BREAKING CHANGE, and issue references. Use when composing, reviewing, amending or fixing a commit message, when preparing a pull request title, or when the user asks what a change should be committed as. Do not use for the mechanics of staging, branching or pushing; that is git-workflow.
---

# Conventional Commits

## Inspect first

1. `git diff --staged` (or `git diff` when nothing is staged) - the message must
   describe **this** diff and nothing else
2. `git log --oneline -10` - match the existing style
3. `git status --short` - confirm nothing unrelated is staged

## Header

```text
<type>(<scope>)!: <subject>
```

- `type` is required and lowercase.
- `scope` is optional. Include it only when it tells the reader something the
  subject does not. Omit it for repository-wide changes.
- `!` marks a breaking change and requires a `BREAKING CHANGE:` footer.
- `subject` is imperative mood, lowercase start, no trailing period.
- Keep the header at 72 characters or fewer.

## Types

| Type | Use for |
| --- | --- |
| `feat` | New capability visible to a user or caller |
| `fix` | Corrected defect in existing behaviour |
| `docs` | Documentation only |
| `style` | Formatting only, no behaviour change |
| `refactor` | Restructuring with no behaviour and no API change |
| `perf` | Change made for performance |
| `test` | Adding or correcting tests only |
| `build` | Build system, project files, packages, Dockerfile |
| `ci` | Workflows and pipeline configuration |
| `chore` | Housekeeping that fits nothing above |
| `revert` | Reverting a previous commit |

## Scopes used in this repository

Derive the scope from the area actually touched:

`api`, `aggregation`, `dispatch`, `dashboard`, `health`, `openapi`, `config`,
`tests`, `docker`, `agents`

## Subject

- Imperative: "add", "fix", "remove" - not "added", "adds", "adding".
- Concrete: name the thing that changed.
- One logical purpose per commit. If the subject needs "and", split the commit.

## Body

Add a body when the header cannot carry the reasoning. Wrap at 72 characters,
separate from the header with a blank line, and explain **why**, not a restatement
of the diff. Skip the body for genuinely self-evident changes.

## Footer

- `BREAKING CHANGE: <what broke and what callers must do>` - required whenever the
  header carries `!`.
- Issue references: `Refs: #12`, `Closes: #12`.
- One footer token per line.

## Deriving the message from the diff

1. Read the full staged diff.
2. Decide what observable behaviour changed. That decides the type.
3. Find the narrowest area that covers every hunk. That decides the scope, or tells
   you the commit should be split.
4. Write the subject as the single sentence that completes "This commit will ...".
5. Re-read the diff and delete any claim it does not support.

Never state something the diff does not prove. Do not claim a fix works, that
performance improved, that tests pass, or that a bug is resolved unless the diff
itself contains the evidence.

## Good examples

```text
feat(api): add batch order endpoint
fix(aggregation): preserve orders after dispatch failure
test(aggregation): cover concurrent batch submission
docs(architecture): document dispatch guarantees
build(docker): run the container as a non-root user
ci: validate agent skills before the build
refactor(dispatch): extract the drain cycle into its own method
```

Breaking change:

```text
feat(api)!: return 202 with a receipt from POST /api/orders

The endpoint previously answered 200 with an empty body. Callers that
asserted on 200 must be updated.

BREAKING CHANGE: POST /api/orders now answers 202 Accepted and includes an
OrderBatchReceipt body.
```

## Bad examples and why

| Message | Problem |
| --- | --- |
| `update code` | No type, no information |
| `fix: bug` | Says nothing about what was wrong |
| `feat(aggregation): added new endpoint and fixed logging and cleaned up tests` | Three purposes, past tense |
| `fix(api): fix everything, all tests now pass` | Claims the diff does not prove |
| `chore: wip` | Not a reviewable unit |
| `Feat(API): Add Endpoint.` | Wrong case, trailing period |
| `refactor(aggregation): rename field and change return type` | An API change is not a refactor |

## Verify

- Header matches `<type>(<scope>)!: <subject>`, 72 characters or fewer
- Type is from the table and reflects the real nature of the change
- Subject is imperative, lowercase, no trailing period
- Every claim is supported by the diff
- `!` and `BREAKING CHANGE:` are either both present or both absent
- The commit has exactly one logical purpose
