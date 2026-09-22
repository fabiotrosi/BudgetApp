# Coding Conventions for BudgetApp 

This document outlines the coding conventions to be followed when developing the Budget App. Adhering to these conventions will ensure code consistency, readability, and maintainability across the project.

*Last updated: 22.09.2026*

# Database Conventions
### Table Names
- Example table names:
	- `Camp`
	- `Budget`
	- `PositionType`
- Use singular nouns.
- Use PascalCase.
- Use English for all table names and attributes.

### Attribute Names
- Example attribute names:
	- `CampId`
	- `SubCategoryId`
	- `LeadersTeamCount_rl`
	- `js_PersonCount_fc`
- Primary keys are named with `Id`.
- The `Id` suffix is used for foreign keys. (e.g. `TemplateBudgetId`)
- Prefixes and suffixes are written in lowercase and separated with an underscore.
- Prefix descriptions:
	- `js_` => J+S (Jugend und Sport)
- Suffix descriptions:
	- `_fc` => forecasted data
	- `_rl` => real data
- Use PascalCase.
- Use singular nouns.

### Standard Attributes
Every table must have the following standard attributes:
- `Id` (int, primary key, auto-incremented): Identifier for each record.
- `CreateDate` (DateTime, nullable): Timestamp of when the record was created.
- `ChangeDate` (DateTime, nullable): Timestamp of the last update to the record.
- TODO: `CreateBy`, `ChangeBy`

The `CreateDate` is set with the default constraint `GETDATE()` in the database and the `ChangeDate` is updated with a trigger on every update.
These values are never set by the application.


# General C# Conventions

### Naming
- Private fields use `_camelCase` and are `readonly` unless they must be mutated after construction.
- Constants use PascalCase (not `UPPER_SNAKE_CASE`).
- Enums use PascalCase and never get an `Enum` suffix on the type name.
	- Enums that mirror a lookup table in the database use explicit numeric values starting at `1`, matching the table's `Id` values.
	- Enums that only exist in the app (not backed by a database table) may start at `0` implicitly.
- Async method naming (see Controller and Data Access below): public repository methods and controller actions omit the `Async` suffix; private helper/service async methods include it.

### Namespaces & Using Directives
- Namespaces mirror the folder path 1:1. (e.g. a class in `Data/Repositories/` is `namespace BudgetApp.Data.Repositories`)
- Use block-scoped `namespace X { ... }`, not file-scoped `namespace X;`.
- Order using directives with `System.*` namespaces first (alphabetical), followed by all remaining namespaces as one alphabetically-sorted group.

### Comments
- No XML doc comments (`///`) in hand-written code.
- Use brief `//` comments only to explain non-obvious *why* (a business rule, a workaround) — never to narrate *what* the following line does.

### Nullable Reference Types & Collections
- Nullable reference types are enabled project-wide. Prefer the `required` modifier on non-nullable string properties over suppressing the warning (see Models above).
- Default collection properties with the collection-expression syntax `= [];`, not `= new List<T>();`.

### Dependency Injection
- Register everything `AddScoped` — this app does not use `AddSingleton` or `AddTransient`.
- Register repositories against their interface using the generic pattern: `AddScoped<IXxxRepository<XxxModel>, XxxRepository<XxxModel>>()`.


# MVC

## M - Models

### Database Models
Database models are used to transfer data between the application and the database.

- Use the database naming conventions for model classes and properties.
- Extend the table names with the `Model` suffix. (e.g. `CampModel`, `BudgetModel`, `PositionModel`)
- Always name properties according to the database attribute names. => Allows automatic mapping with Dapper.
- Use `DataAnnotations` for every model property.
	- Use `[Required]` for non-nullable fields.
	- Use `[Range()]` for numeric ranges.
	- Match `[StringLength()]` with database constraints.
	- Use `[DataType(DataType.DataTypeName)]` for data that requires extra validation (e.g. Email, Phone, Password).
- Always match data types with the database.
- Add the `required` modifier to non-nullable string properties to avoid nullable warnings.

### View Models 
View models are used to transfer data between the controller and the view.
- Use the `ViewModel` suffix for view model classes. (e.g. `CampViewModel`, `BudgetViewModel`, `PositionViewModel`)
- Use the same property names as in the database models, but adapt them to the view's needs.
- Use `DataAnnotations` for validation.
	- Use `[Required]` for mandatory fields.
	- Use `[StringLength()]` to limit string lengths.
	- Use `[Range()]` for numeric ranges.
	- Use `[Display(Name = "Display Name")]` for user-friendly names. The display name is shown in the UI.
	- Use `[DataType(DataType.DataTypeName)]` for data that requires extra validation (e.g. Email, Phone, Password).
- Add the `required` modifier to non-nullable string properties to avoid nullable warnings.

## V - View

- Use **Tag Helpers** (`asp-for`, `asp-validation-for`, `asp-action`, `asp-controller`, etc.) for all new or edited views — this is the required style going forward.
	- Some older views (e.g. `_BudgetForm.cshtml`) still use classic HtmlHelper methods (`Html.TextBoxFor`, ...). These don't need to be migrated, but any new or changed markup should use Tag Helpers.
- Common usings and `@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers` are set once in `_ViewImports.cshtml` — don't repeat them in individual views.
- Partial views:
	- Prefix the file name with `_` and use PascalCase. (e.g. `_BudgetForm.cshtml`, `_ToastNotification.cshtml`, `_GoBackButton.cshtml`)
	- Partials rendered inside a Bootstrap modal are suffixed with `Modal`. (e.g. `_UpsertCategoryModal.cshtml`, `_CreateModal.cshtml`)
	- Non-modal partials are named after their content/purpose.
- A controller action passes a single, concrete `ViewModel` to `View()` / `PartialView()`. Passing a raw database model directly (e.g. a bare `CategoryModel`) is only acceptable for small, standalone modals that need no additional view-only data.
- Use `[Display(Name = "...", ResourceType = typeof(DataAnnotations))]` on the Model/ViewModel property (see Models above) for user-facing labels; resolve labels through the localization resource rather than hardcoding text in the view.
- Toast notifications travel from controller to view under the exact `TempData` key `"ToastMsg"`, read exactly once by `_ToastNotification.cshtml`. Don't introduce a different key or read it more than once.

## C - Controller

- Add `[Authorize]` at the class level.
- Use constructor injection only — no primary constructors, no field initializers.
	- Declare dependencies as `private readonly` fields.
	- `ILogger<TController>` is always the first constructor parameter.
	- Declare fields in the same order as the constructor parameters they're assigned from.
	- Abbreviate repository fields to `...Repo`. (e.g. `_categoryRepo` for `ICategoryRepository<CategoryModel>`)
- Action method naming follows the same rule as repositories (see Data Access below): async action methods do **not** get an `Async` suffix. (e.g. `Index()`, `UpsertCategory()`)
	- Private helper methods on a controller **do** get the `Async` suffix. (e.g. `HasAccessAsync`, `SaveAttachmentsAsync`)
- Decorate actions with `[HttpGet]` / `[HttpPost]`, and add `[ValidateAntiForgeryToken]` to every mutating `[HttpPost]` action.
- Error handling happens in controller actions only — repositories never catch exceptions (see Data Access below).
	- Full-page and redirect actions: wrap the body in `try`/`catch`, log with `_logger.LogError(ex, "Error in <ActionName>")` (literal template, only the action name substituted), then build a `ToastMessageViewModel` (`Title` = "Erfolg" / "Fehler", `Message` in German, `Type` from `ToastType`) and store it with `TempData.Put("ToastMsg", toast)`.
	- AJAX / partial (modal) actions: on failure, use `ModelState.AddModelError(string.Empty, "Ein unerwarteter Fehler ist aufgetreten.")` and re-render the partial instead of showing a toast.
	- Always pass the exception object to `LogError`. Use `LogWarning` for recoverable skip-and-continue cases, with structured placeholders — not string interpolation — in the message template. (e.g. `_logger.LogWarning("Skipping file {FileName}: unsupported content type {ContentType}", file.FileName, file.ContentType)`)
- When `ModelState.IsValid` is `false`, repopulate any dropdown/lookup collections on the ViewModel before returning `View(vm)` / `PartialView(..., vm)` — they won't survive the postback otherwise.
- Validation: field-level rules live on the Model/ViewModel as stock `DataAnnotations` (see Models above). Cross-field or business-rule validation that can't be expressed that way is added manually in the controller via `ModelState.AddModelError`. Don't write custom `ValidationAttribute` or `IValidatableObject` implementations.

*Note: the codebase does not currently use `CancellationToken` in async action or helper methods. This isn't a required convention — just the current state.*

# Data Access
The database is accessed using the micro-ORM `Dapper`. (https://www.learndapper.com/)

All data access is done asynchronously.
For each table, a repository class is created to handle all database operations related to that table.
These repositories are then injected into the services that require database access.
Each repository has at least the CRUD operations implemented.

- Create => `Create(Model model)`
- Read => `GetAll()`, `GetById(int id)`
- Update => `Update(Model model)`
- Delete => `Delete(int id)`

The error handling is done in the controllers and not in the repositories.

### Dapper / SQL Conventions
- Write raw SQL as a verbatim string literal (`@"..."`) assigned to a local `sql` variable before calling Dapper.
- Bracket every identifier: `[dbo].[TableName]`, `[ColumnName]`.
- Name SQL parameters to match the C# property name exactly. (e.g. `@Id`, `@BudgetId`)
- Extract reusable column lists into a `private const string SelectColumns` field to avoid duplicating the column list across methods in the same repository.
- `INSERT` statements end with `SELECT CAST(SCOPE_IDENTITY() as int);` and are executed with `ExecuteScalarAsync<int>` to return the new row's `Id`.
- Call the `ValidateId(int id)` guard (from `BaseRepository`) at the top of `GetById` and `Delete`.
- Optional/conditional `WHERE` fragments may use string interpolation only for **static boolean fragments** known at compile time. (e.g. `{(includeInactive ? "" : "AND bu.[IsActive] = 1")}`) Never interpolate raw user input into SQL — always pass user-supplied values through a `@Param`.