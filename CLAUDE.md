# Memory

## Preferences

### Git naming convention

- Branch format: `<type>/<ticket>-<short-kebab-description>`.
- Use lowercase kebab-case and keep branch names short and descriptive.
- Branch types: `feature`, `fix`, `hotfix`, `refactor`, `docs`, `test`, `chore`.
- Include the ticket ID when one is available; otherwise omit the ticket segment.
- Do not include a developer's name in a branch name.
- Commit format: `<type>(<scope>): <short imperative description>`.
- Conventional Commit types: `feat`, `fix`, `refactor`, `docs`, `test`, `chore`.
- Keep commit subjects under approximately 72 characters.
- When a ticket exists, add `Refs: <ticket>` in the commit footer.
- For breaking changes, add `!` after the type or scope and include a `BREAKING CHANGE:` footer.

Example:

```text
Branch: feature/MB-142-add-board-filters

Commit:
feat(board): add card filters

Refs: MB-142
```
