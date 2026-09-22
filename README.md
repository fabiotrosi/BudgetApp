# BudgetApp

[![.NET Build](https://github.com/fabiotrosi/BudgetApp/actions/workflows/dotnet.yml/badge.svg)](https://github.com/fabiotrosi/BudgetApp/actions/workflows/dotnet.yml)
[![CSharpier](https://github.com/fabiotrosi/BudgetApp/actions/workflows/csharpier-format-check.yml/badge.svg)](https://github.com/fabiotrosi/BudgetApp/actions/workflows/csharpier-format-check.yml)
[![License: AGPL v3](https://img.shields.io/badge/license-AGPL--3.0--or--later-blue.svg)](LICENSE)

BudgetApp is a .NET 10 ASP.NET Core MVC web application for event-based budget management, designed for youth camps and similar events. It supports a two-phase budget workflow — **Provisorisch** (forecast) and **Definitiv** (real amounts) — and allows budgets to be generated from reusable templates.

The data model can be viewed here:
[https://dbdiagram.io/d/BudgetApp-69172fff6735e11170d8cafe](https://dbdiagram.io/d/BudgetApp-69172fff6735e11170d8cafe)

## Features

- **Template budgets** — define reusable position sets (e.g. *House Camp*, *Tent Camp*) that can be used to generate a budget draft with a single action
- **Two-phase amounts** — each position carries both a forecast (_fc) and a real (_rl) amount; totals are calculated and displayed side by side
- **Variable quantities** — position quantities can be bound to a camp variable (participants, J+S persons, leadership team), so totals update automatically when camp data changes
- **Camp data** — each budget is linked to a camp record containing dates, participant counts, and leadership team sizes for both the forecast and real phases
- **Categories & subcategories** — positions are grouped hierarchically for a clear table layout with subtotals per group
- **Position types** — each position is classified as income or expense; signed totals and a balance are shown throughout
- **Per-row edit modal** — all position fields (name, type, category, amounts) are editable in a Bootstrap modal without leaving the detail page

## Tech Stack

- **Framework:** .NET 10, ASP.NET Core MVC, Razor Views, Tag Helpers
- **Database:** SQL Server 2022 (Docker), accessed via Dapper
- **Architecture:** Repository pattern — one repository per table, all async, injected via interfaces
- **Frontend:** Bootstrap 5, Bootstrap Icons
- **Localization:** Fixed to `de-CH`

### How can I help?

- **Report a bug or suggest a feature** by opening a [GitHub issue](https://github.com/fabiotrosi/BudgetApp/issues)
- **Pick up an open issue** and submit a pull request
- Before opening a pull request, please read [CONTRIBUTING.md](CONTRIBUTING.md)

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop) (must be running)

### Run locally

```bash
dotnet run
```

Building the project (`dotnet build`/`dotnet run`/`dotnet watch run`, or F5 in your IDE) automatically
starts the SQL Server container via `docker compose up -d` if it isn't already running, so there's no
manual step. This only runs for Debug builds; if Docker Desktop isn't running, the build still succeeds
with a warning, and you'll need to start Docker Desktop before the app can reach the database. To start
the container yourself instead, e.g. to view init output before running the app:

```bash
docker compose up -d
```

The app is available at `http://localhost:5272`.

Use `dotnet watch run` for hot reload during development.

### Database

The SQL Server container is initialised by `docker/init-db.sql` on first startup. The script is idempotent and runs on every container start, so schema changes (new columns etc.) are applied automatically via `IF NOT EXISTS` guards.

```bash
docker compose down        # Stop (data volume persists)
docker compose down -v     # Stop and delete all data
docker compose logs sqlserver  # View init output
```

Connection string and SA password (`BudgetApp_Dev@2024`) are in `appsettings.Development.json`.

## Maintainer

[Fabio Trosi](https://github.com/fabiotrosi)

## License

[AGPL-3.0-or-later](LICENSE)
