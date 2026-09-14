using BudgetApp.Models;
using Dapper;

namespace BudgetApp.Data.Repositories
{
    public class BudgetRepository<T> : BaseRepository, IBudgetRepository<T>
        where T : BudgetModel
    {
        public BudgetRepository(DapperContext context)
            : base(context) { }

        private const string SelectColumns =
            @"
                   [Id]
                  ,[Name]
                  ,[Description]
                  ,[StartDate]
                  ,[EndDate]
                  ,[MainLeader]
                  ,[ParticipantsCount_fc]
                  ,[js_PersonsCount_fc]
                  ,[LeadersTeamCount_fc]
                  ,[ParticipantsCount_rl]
                  ,[js_PersonsCount_rl]
                  ,[LeadersTeamCount_rl]
                  ,[CreatedByUserId]
            ";

        public async Task<IEnumerable<T>> GetAll()
        {
            using var conn = _context.CreateConnection();
            var sql = $"SELECT {SelectColumns} FROM [dbo].[Budget]";
            return await conn.QueryAsync<T>(sql);
        }

        public async Task<IEnumerable<T>> GetAllForUser(int userId)
        {
            using var conn = _context.CreateConnection();
            var sql =
                $@"
            SELECT {SelectColumns}
              FROM [dbo].[Budget] b
              WHERE b.[CreatedByUserId] = @UserId
                 OR EXISTS (
                     SELECT 1 FROM [dbo].[BudgetUser] bu
                     WHERE bu.[BudgetId] = b.[Id] AND bu.[UserId] = @UserId AND bu.[IsActive] = 1
                 )
            ";
            return await conn.QueryAsync<T>(sql, new { UserId = userId });
        }

        public async Task<T?> GetById(int id)
        {
            ValidateId(id);
            using var conn = _context.CreateConnection();
            var sql = $"SELECT {SelectColumns} FROM [dbo].[Budget] WHERE Id = @Id";
            return await conn.QuerySingleOrDefaultAsync<T>(sql, new { Id = id });
        }

        public async Task<int> Create(T budget)
        {
            using var conn = _context.CreateConnection();
            var sql =
                @"
            INSERT INTO [dbo].[Budget]
                       ([Name]
                       ,[Description]
                       ,[StartDate]
                       ,[EndDate]
                       ,[MainLeader]
                       ,[ParticipantsCount_fc]
                       ,[js_PersonsCount_fc]
                       ,[LeadersTeamCount_fc]
                       ,[ParticipantsCount_rl]
                       ,[js_PersonsCount_rl]
                       ,[LeadersTeamCount_rl]
                       ,[CreatedByUserId])
                 VALUES
                       (@Name
                       ,@Description
                       ,@StartDate
                       ,@EndDate
                       ,@MainLeader
                       ,@ParticipantsCount_fc
                       ,@js_PersonsCount_fc
                       ,@LeadersTeamCount_fc
                       ,@ParticipantsCount_rl
                       ,@js_PersonsCount_rl
                       ,@LeadersTeamCount_rl
                       ,@CreatedByUserId);
                    SELECT CAST(SCOPE_IDENTITY() as int);
            ";
            return await conn.ExecuteScalarAsync<int>(sql, budget);
        }

        public async Task<int> Update(T budget)
        {
            using var conn = _context.CreateConnection();
            var sql =
                @"
            UPDATE [dbo].[Budget]
               SET [Name] = @Name
                  ,[Description] = @Description
                  ,[StartDate] = @StartDate
                  ,[EndDate] = @EndDate
                  ,[MainLeader] = @MainLeader
                  ,[ParticipantsCount_fc] = @ParticipantsCount_fc
                  ,[js_PersonsCount_fc] = @js_PersonsCount_fc
                  ,[LeadersTeamCount_fc] = @LeadersTeamCount_fc
                  ,[ParticipantsCount_rl] = @ParticipantsCount_rl
                  ,[js_PersonsCount_rl] = @js_PersonsCount_rl
                  ,[LeadersTeamCount_rl] = @LeadersTeamCount_rl
                WHERE Id = @Id
            ";
            return await conn.ExecuteAsync(sql, budget);
        }

        public async Task<int> Delete(int id)
        {
            ValidateId(id);
            using var conn = _context.CreateConnection();
            var sql = "DELETE FROM [dbo].[Budget] WHERE Id = @Id";
            return await conn.ExecuteAsync(sql, new { Id = id });
        }
    }
}
