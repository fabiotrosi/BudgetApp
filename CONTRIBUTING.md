# Contributing

Thanks for your interest in contributing to BudgetApp! Whether it's a bug report, a feature idea, or a pull request, contributions are welcome.

## Code of Conduct

Be respectful and constructive. Assume good intent, and keep discussion focused on the project.

## Getting started

See [README.md](README.md#getting-started) for prerequisites and how to run the app locally.

## Branching

Branch off `dev` using `feature/<short-description>` (e.g. `feature/deactivate-budget-leaders`), and open pull requests against `dev`.

## Before submitting a pull request

- [ ] Code formatted with `dotnet csharpier format .`, and `dotnet csharpier check .` passes
- [ ] `dotnet build` succeeds
- [ ] The change was manually verified by running the app (`dotnet run` / `dotnet watch run`)
- [ ] New DB columns/tables follow the naming conventions in [CodingConventions.md](CodingConventions.md) and use idempotent `IF NOT EXISTS` guards in `docker/init-db.sql`
- [ ] New validation messages were added to `Resources/DataAnnotations.resx` **and** the corresponding property was hand-added to `DataAnnotations.Designer.cs` (the build won't catch a missing one)
- [ ] No secrets or connection strings are committed
- [ ] CI checks are green

## Coding conventions

Full conventions, including model/repository naming and database rules, are in [CodingConventions.md](CodingConventions.md).

## Database / dev data

`docker compose up -d` starts a local SQL Server container. `docker/init-db.sql` is idempotent and creates the schema on first run; it's safe to re-run on every container start. The SA password and connection string are in `appsettings.Development.json` (see [README.md](README.md#database) for details).

## License

By contributing, you agree that your contributions will be licensed under this project's [AGPL-3.0-or-later](LICENSE) license.
