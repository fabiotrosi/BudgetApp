using BudgetApp.Models;

namespace BudgetApp.Data.Repositories
{
    public interface IBudgetUserRepository<T>
        where T : BudgetUserModel
    {
        Task<IEnumerable<T>> GetByBudgetId(int budgetId, bool includeInactive = false);
        Task<IEnumerable<T>> GetByUserId(int userId);
        Task<int> Create(T budgetUser);
        Task<int> Deactivate(int id, int deactivatedByUserId);
        Task<int> Reactivate(int id, int reactivatedByUserId);
    }
}
