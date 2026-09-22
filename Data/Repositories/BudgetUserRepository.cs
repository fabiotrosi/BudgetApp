using BudgetApp.Models;
using Dapper;

namespace BudgetApp.Data.Repositories
{
    public class BudgetUserRepository<T> : BaseRepository, IBudgetUserRepository<T>
        where T : BudgetUserModel
    {
        public BudgetUserRepository(DapperContext context)
            : base(context) { }

        public async Task<IEnumerable<T>> GetByBudgetId(int budgetId, bool includeInactive = false)
        {
            using var conn = _context.CreateConnection();
            var sql =
                $@"
            SELECT bu.[Id]
                  ,bu.[BudgetId]
                  ,bu.[UserId]
                  ,bu.[IsMainLeader]
                  ,bu.[IsActive]
                  ,bu.[DeactivatedAt]
                  ,bu.[DeactivatedByUserId]
                  ,bu.[ReactivatedAt]
                  ,bu.[ReactivatedByUserId]
                  ,u.[DisplayName]
                  ,u.[Email]
              FROM [dbo].[BudgetUser] bu
              INNER JOIN [dbo].[User] u ON bu.[UserId] = u.[Id]
              WHERE bu.[BudgetId] = @BudgetId
                {(includeInactive ? "" : "AND bu.[IsActive] = 1")}
              ORDER BY bu.[IsMainLeader] DESC, u.[DisplayName]
            ";
            return await conn.QueryAsync<T>(sql, new { BudgetId = budgetId });
        }

        public async Task<IEnumerable<T>> GetByUserId(int userId)
        {
            using var conn = _context.CreateConnection();
            var sql =
                @"
            SELECT bu.[Id]
                  ,bu.[BudgetId]
                  ,bu.[UserId]
                  ,bu.[IsMainLeader]
                  ,bu.[IsActive]
                  ,bu.[DeactivatedAt]
                  ,bu.[DeactivatedByUserId]
                  ,bu.[ReactivatedAt]
                  ,bu.[ReactivatedByUserId]
                  ,u.[DisplayName]
                  ,u.[Email]
              FROM [dbo].[BudgetUser] bu
              INNER JOIN [dbo].[User] u ON bu.[UserId] = u.[Id]
              WHERE bu.[UserId] = @UserId
                AND bu.[IsActive] = 1
            ";
            return await conn.QueryAsync<T>(sql, new { UserId = userId });
        }

        public async Task<int> Create(T budgetUser)
        {
            using var conn = _context.CreateConnection();
            var sql =
                @"
            INSERT INTO [dbo].[BudgetUser]
                       ([BudgetId]
                       ,[UserId]
                       ,[IsMainLeader])
                 VALUES
                       (@BudgetId
                       ,@UserId
                       ,@IsMainLeader);
                    SELECT CAST(SCOPE_IDENTITY() as int);
            ";
            return await conn.ExecuteScalarAsync<int>(sql, budgetUser);
        }

        public async Task<int> Deactivate(int id, int deactivatedByUserId)
        {
            ValidateId(id);
            using var conn = _context.CreateConnection();
            var sql =
                @"
            UPDATE [dbo].[BudgetUser]
               SET [IsActive] = 0
                  ,[DeactivatedAt] = GETDATE()
                  ,[DeactivatedByUserId] = @DeactivatedByUserId
                  ,[ReactivatedAt] = NULL
                  ,[ReactivatedByUserId] = NULL
             WHERE [Id] = @Id
            ";
            return await conn.ExecuteAsync(
                sql,
                new { Id = id, DeactivatedByUserId = deactivatedByUserId }
            );
        }

        public async Task<int> Reactivate(int id, int reactivatedByUserId)
        {
            ValidateId(id);
            using var conn = _context.CreateConnection();
            var sql =
                @"
            UPDATE [dbo].[BudgetUser]
               SET [IsActive] = 1
                  ,[ReactivatedAt] = GETDATE()
                  ,[ReactivatedByUserId] = @ReactivatedByUserId
                  ,[DeactivatedAt] = NULL
                  ,[DeactivatedByUserId] = NULL
             WHERE [Id] = @Id
            ";
            return await conn.ExecuteAsync(
                sql,
                new { Id = id, ReactivatedByUserId = reactivatedByUserId }
            );
        }
    }
}
