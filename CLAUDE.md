@AGENTS.md

## Claude Code

The rules above are shared with Codex and apply unchanged. This section covers what
is specific to Claude Code.

### Skills

Project skills live in `.claude/skills/`, generated from the canonical
`.agents/skills/`. Claude Code loads them automatically when a task matches a
skill's `description`, and you can invoke one directly:

```text
/git-workflow
/conventional-commits
/dotnet-10-development
/rest-api-design
/dotnet-naming-conventions
/blazor-development
/object-oriented-design
/dotnet-testing
```

Run `/skills` to list what is loaded. Load only the skill the task needs.

### Editing skills

`.claude/skills/` is generated. Every file there carries a do-not-edit banner. Edit
the canonical copy under `.agents/skills/<name>/SKILL.md`, then run:

```bash
python3 scripts/validate-agent-skills.py --sync
```

CI fails if the two trees drift apart. See `docs/ai-agent-development.md`.

### Reminders that matter most here

- Read `git status` before you touch the working tree, and report uncommitted
  changes rather than acting on them.
- Do not commit or push unless the task asks for it. `develop` and `master` reject
  direct pushes, so a change lands through a topic branch and a pull request.
- Never use `git push --force`, `git reset --hard` or `git clean -fd`.
- All paths in this repository's configuration are relative to the repository root.
  Never write an absolute local path into a committed file.
