# Contributing

Thanks for your interest in contributing to BudgetApp! Whether it's a bug report, a feature idea, or a pull request, contributions are welcome.

## Code of Conduct

Be respectful and constructive. Assume good intent, and keep discussion focused on the project.

## Getting started

See [README.md](README.md#getting-started) for prerequisites and how to run the app locally.

## Branching

Branch off `dev` using one of the following prefixes, and open pull requests against `dev`:
- `feature/<short-description>` — new functionality (e.g. `feature/deactivate-budget-leaders`)
- `fix/<short-description>` — bug fixes
- `chore/<short-description>` — maintenance work with no user-facing behavior change (tooling, dependencies, config)
- `docs/<short-description>` — documentation-only changes
- `refactor/<short-description>` — code changes that don't change behavior

## Commit messages

- Write a concise, single-line summary in the imperative present tense (e.g. "Fix invite email field never binding on submit", not "Fixed" or "Fixes").
- Capitalize the first word; no trailing period.
- Don't use a Conventional Commits type prefix (no `feat:`, `fix:`, etc.) — this repo doesn't use that style.
- Add a body with bullet points only when the summary alone doesn't cover the details worth calling out.

Examples from the project history:
- "Use a shared Razor layout for all outgoing emails"
- "Fix invite email field never binding on submit"
- "Add LICENSE, CONTRIBUTING.md, and restructure README"

## Before submitting a pull request

- [ ] Code formatted with `dotnet csharpier format .`, and `dotnet csharpier check .` passes
- [ ] `dotnet build` succeeds
- [ ] The change was manually verified by running the app (`dotnet run` / `dotnet watch run`)
- [ ] New DB columns/tables follow the naming conventions in [CodingConventions.md](CodingConventions.md) and use idempotent `IF NOT EXISTS` guards in `docker/init-db.sql`
- [ ] New validation messages were added to `Resources/DataAnnotations.resx` **and** the corresponding property was hand-added to `DataAnnotations.Designer.cs` (the build won't catch a missing one)
- [ ] No secrets or connection strings are committed
- [ ] CI checks are green

## Pull requests

- Title: same style as a commit message — imperative, capitalized, no trailing period. It usually mirrors the summary of the main commit. (e.g. "Add LICENSE, CONTRIBUTING.md, and restructure README")
- Description: use a `## Summary` section with a bullet list of what changed and why, followed by a `## Test plan` section with a checklist (`- [x]` / `- [ ]`) of how the change was verified (e.g. `dotnet build`, `dotnet csharpier check .`, manual verification steps).
- Add `Closes #<issue>` at the end of the description when the PR closes an issue.

## Coding conventions

Full conventions, including model/repository naming and database rules, are in [CodingConventions.md](CodingConventions.md).

## Database / dev data

`docker compose up -d` starts a local SQL Server container. `docker/init-db.sql` is idempotent and creates the schema on first run; it's safe to re-run on every container start. The SA password and connection string are in `appsettings.Development.json` (see [README.md](README.md#database) for details).

## License

By contributing, you agree that your contributions will be licensed under this project's [AGPL-3.0-or-later](LICENSE) license.
