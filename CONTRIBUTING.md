# Contributing

Read this before opening an issue or a PR, it keeps things moving for everyone.

Português: [CONTRIBUTING.pt-BR.md](CONTRIBUTING.pt-BR.md)

## Opening an issue

Search existing issues first. If nothing matches, open one with enough context to act on: what happened, what you expected, how to reproduce (for bugs) or why it's needed (for features).

## Picking up an issue

Comment asking to be assigned before you start working. Wait for confirmation. Only take one open issue at a time, finish or drop it before picking up another.

## Submitting a PR

- Reference the issue it closes (`closes #123`)
- Never force-push to a PR branch, it breaks re-review. Push new commits instead, they get squashed on merge.
- Run `dotnet test` before opening the PR. The schedule logic is the part most likely to break quietly, so changes there need a test.
- No code comments. Names and small functions carry the intent.

## Reviews

Be direct, not harsh. Use inline comments for specific suggestions. If feedback was already given, don't repeat it, a thumbs-up is enough to agree.
