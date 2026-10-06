using System.Security.Claims;
using BudgetApp.Data.Repositories;
using BudgetApp.Extensions;
using BudgetApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;

namespace BudgetApp.Authorization
{
    /// <summary>
    /// Succeeds only for the main leader of the budget the request targets.
    /// The budget id is resolved from the "budgetId" route/query/form value, falling back
    /// to "id". Any action under this policy whose "id" is not a budget id must also send
    /// "budgetId".
    /// </summary>
    public class MainLeaderHandler : AuthorizationHandler<MainLeaderRequirement>
    {
        private readonly IBudgetUserRepository<BudgetUserModel> _budgetUserRepo;

        public MainLeaderHandler(IBudgetUserRepository<BudgetUserModel> budgetUserRepo)
        {
            _budgetUserRepo = budgetUserRepo;
        }

        protected override async Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            MainLeaderRequirement requirement
        )
        {
            // MVC controller actions route authorization through AuthorizeFilter, which passes
            // an AuthorizationFilterContext as the resource, not a bare HttpContext.
            HttpContext? httpContext = context.Resource switch
            {
                HttpContext hc => hc,
                AuthorizationFilterContext filterContext => filterContext.HttpContext,
                _ => null,
            };
            if (httpContext == null)
                return;

            int? budgetId = await ResolveBudgetIdAsync(httpContext);
            if (budgetId == null)
                return;

            string? userIdValue = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdValue, out int userId))
                return;

            var budgetUsers = await _budgetUserRepo.GetByBudgetId(budgetId.Value);
            if (budgetUsers.IsMainLeaderFor(userId))
                context.Succeed(requirement);
        }

        private static async Task<int?> ResolveBudgetIdAsync(HttpContext httpContext)
        {
            foreach (string key in new[] { "budgetId", "id" })
            {
                if (TryGetInt(httpContext.Request.RouteValues, key, out int fromRoute))
                    return fromRoute;
                if (TryGetInt(httpContext.Request.Query, key, out int fromQuery))
                    return fromQuery;
            }

            if (httpContext.Request.HasFormContentType)
            {
                var form = await httpContext.Request.ReadFormAsync();
                foreach (string key in new[] { "budgetId", "id" })
                {
                    if (TryGetInt(form, key, out int fromForm))
                        return fromForm;
                }
            }

            return null;
        }

        private static bool TryGetInt(RouteValueDictionary routeValues, string key, out int value)
        {
            foreach (var kvp in routeValues)
            {
                if (
                    string.Equals(kvp.Key, key, StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(kvp.Value?.ToString(), out value)
                )
                    return true;
            }
            value = 0;
            return false;
        }

        private static bool TryGetInt(IQueryCollection query, string key, out int value)
        {
            foreach (string queryKey in query.Keys)
            {
                if (
                    string.Equals(queryKey, key, StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(query[queryKey], out value)
                )
                    return true;
            }
            value = 0;
            return false;
        }

        private static bool TryGetInt(IFormCollection form, string key, out int value)
        {
            foreach (string formKey in form.Keys)
            {
                if (
                    string.Equals(formKey, key, StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(form[formKey], out value)
                )
                    return true;
            }
            value = 0;
            return false;
        }
    }
}
