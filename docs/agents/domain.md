# Domain Docs

How the engineering skills should consume this repo's domain documentation.

## Before exploring, read these

- `CONTEXT.md` at the repo root.
- Relevant ADRs in `docs/adr/`.

If these files do not exist, proceed without flagging their absence.

## File structure

This is a single-context repo:

```text
/
├── CONTEXT.md
└── docs/
    └── adr/
```

## Use the glossary's vocabulary

Use terms as defined in `CONTEXT.md` in issue titles, refactor proposals, hypotheses, and tests. If a concept is missing, note the gap for domain modeling.

## Flag ADR conflicts

If work contradicts an existing ADR, call out the conflict explicitly rather than silently overriding the decision.
