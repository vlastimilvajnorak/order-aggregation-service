# AGENTS.md

Working agreements for AI coding agents in the `order-aggregation-service`
repository. These rules apply to the whole repository.

## Project

A .NET 10 service that accepts batches of order lines over HTTP and accumulates the
ordered quantity per product. One host serves both the REST API and a Blazor Web App
dashboard.

```text
OrderAggregationService.slnx
src/OrderAggregationService/          Components/ Endpoints/ Models/ Services/ wwwroot/
tests/OrderAggregationService.Tests/
```

The flat two-project layout is deliberate for the current scope. Do not split it
into `Domain` / `Application` / `Infrastructure` projects unless the task asks for
it. If that split ever happens, project references must point inward only.

## Skills

Detailed working procedures live as skills in `.agents/skills/`. Load the skill that
matches the task instead of guessing; do not load all of them for every change.

| Skill | Load it when |
| --- | --- |
| `git-workflow` | Staging, branching, checkout, pull, rebase, merge, conflicts, push, worktrees |
| `conventional-commits` | Writing or fixing a commit message or a pull request title |
| `dotnet-10-development` | Changing `.cs`, `.csproj`, `.slnx`, `Directory.*.props`, `Program.cs`, services, packages |
| `rest-api-design` | Adding or changing a route, verb, status code, error response, API contract or the OpenAPI document |
| `dotnet-naming-conventions` | Creating or renaming any C# symbol, Razor component, file or project |
| `blazor-development` | Changing `.razor` files, `Components/`, UI state, `wwwroot/` |
| `object-oriented-design` | Introducing or reshaping a type, assigning responsibilities, refactoring |
| `dotnet-testing` | Adding, changing or debugging tests, or deciding what level to test at |

In the Codex CLI or IDE extension, run `/skills` or type `$` to mention a skill
explicitly. In ChatGPT, type `@`.

A documentation-only change needs none of the development skills.

## Commands

Run from the repository root.

```bash
dotnet restore
dotnet build --configuration Release --no-restore
dotnet test --configuration Release --no-build
dotnet format --verify-no-changes
python3 scripts/validate-agent-skills.py
```

A Release build must produce zero warnings; warnings are errors in this repository.

## Before and after every task

1. Run `git status` before changing anything and report what you find.
2. Treat uncommitted changes you did not make as the user's work. Do not stage,
   revert, stash or commit them.
3. Run `git status` again when you finish and report the final state.

## Git rules

- `develop` is the default branch, `master` is the stable branch, and `main` is
  prohibited by a repository ruleset.
- Both `develop` and `master` require a pull request and accept no direct push. PRs
  into `master` are only accepted from `develop`.
- Never run `git reset --hard`, `git clean -fd`, `git commit --amend`, an
  interactive rebase, any history rewrite, or `git push --force`.
- Commit, push, merge, tag, release and pull-request creation happen only when the
  user's task calls for them. Writing code is not permission to commit it.
- Stage explicitly by path. Review `git diff` before every commit.
- Never commit build output, coverage, IDE state, `.env` files, secrets,
  certificates or absolute local paths.

## Workspace rules

- Do not create Git worktrees unless the task explicitly asks for one.
- Do not create scratch, backup or output directories inside the repository or next
  to it. Use the system temporary directory and clean up afterwards.
- Do not add a NuGet or JavaScript dependency without a stated reason.

## Quality bar

- Add or update tests for the behaviour you changed. A bug fix needs a test that
  fails without it.
- Update `README.md` when public behaviour, endpoints, configuration or architecture
  change.
- Keep changes small and single-purpose.
- Fix warnings rather than suppressing them; if a suppression is unavoidable, say
  why in a comment.

## Commits

When the task asks for a commit, follow Conventional Commits as defined in the
`conventional-commits` skill. Every claim in the message must be supported by the
diff.

## Agent configuration files

`.claude/skills/` is generated from `.agents/skills/`. Edit only the canonical files
under `.agents/skills/` and run `python3 scripts/validate-agent-skills.py --sync`.
See `docs/ai-agent-development.md`.
