# Issue tracker: GitHub

Issues and PRDs for this repo live as GitHub issues. Use the `gh` CLI for all operations.

## Conventions

- **Create an issue**: `gh issue create --title "..." --body "..."`. Use a heredoc for multi-line bodies.
- **Read an issue**: `gh issue view <number> --comments`, filtering comments by `jq` and also fetching labels.
- **List issues**: `gh issue list --state open --json number,title,body,labels,comments --jq '[.[] | {number, title, body, labels: [.labels[].name], comments: [.comments[].body]}]'` with appropriate `--label` and `--state` filters.
- **Comment on an issue**: `gh issue comment <number> --body "..."`
- **Apply / remove labels**: `gh issue edit <number> --add-label "..."` / `--remove-label "..."`
- **Close**: `gh issue close <number> --comment "..."`

Infer the repo from `git remote -v` — `gh` does this automatically when run inside a clone.

## Pull requests as a triage surface

**PRs as a request surface: no.** External pull requests are not triaged as feature requests in this repo.

## When a skill says "publish to the issue tracker"

Create a GitHub issue.

## When a skill says "fetch the relevant ticket"

Run `gh issue view <number> --comments`.

## Wayfinding operations

Used by `/wayfinder`. The **map** is a single issue labelled `wayfinder:map`, holding the Notes / Decisions-so-far / Fog body. Child tickets are GitHub sub-issues where available; otherwise list them in the map and add `Part of #<map>` to each child.

Use `wayfinder:<type>` labels (`research`, `prototype`, `grilling`, `task`). Once claimed, assign the ticket to the driving developer. Use GitHub's native issue dependencies for blockers where available; otherwise record `Blocked by: #<n>` in the issue body.

The frontier query considers open map children, excluding tickets with open blockers or an assignee. Claim with `gh issue edit <n> --add-assignee @me`. Resolve by commenting with the answer, closing the issue, and appending a context pointer to the map's Decisions-so-far.
