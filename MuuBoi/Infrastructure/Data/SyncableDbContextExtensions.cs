using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MuuBoi.Application.Helpers;
using MuuBoi.Domain.Models;

namespace MuuBoi.Infrastructure.Data
{
    public static class SyncableDbContextExtensions
    {
        public static async Task<T?> FindBySyncIdAsync<T>(this ApplicationDbContext context, Guid syncId)
            where T : class, ISyncable
        {
            return await context.Set<T>().FirstOrDefaultAsync(e => e.SyncId == syncId);
        }

        public static async Task<T> AddSyncableAsync<T>(this ApplicationDbContext context, T entity)
            where T : class, ISyncable
        {
            context.Set<T>().Add(entity);

            try
            {
                await context.SaveChangesAsync();
                return entity;
            }
            catch (DbUpdateException ex) when (IsSyncIdUniqueViolation<T>(context, ex))
            {
                context.Entry(entity).State = EntityState.Detached;
                var existing = await context.FindBySyncIdAsync<T>(entity.SyncId);
                if (existing == null)
                    throw;

                return existing;
            }
        }

        public static async Task<List<T>> GetChangesSinceAsync<T>(this ApplicationDbContext context, ulong since, int take)
            where T : class, ISyncable
        {
            var entityType = context.Model.FindEntityType(typeof(T))!;
            var schema = entityType.GetSchema();
            var table = schema == null ? $"[{entityType.GetTableName()}]" : $"[{schema}].[{entityType.GetTableName()}]";
            var sql = "SELECT * FROM " + table + " WHERE [RowVersion] > {0} AND [RowVersion] < MIN_ACTIVE_ROWVERSION()";

            return await context.Set<T>()
                .FromSqlRaw(sql, SyncPaging.ToRowVersionBytes(since))
                .OrderBy(e => e.RowVersion)
                .Take(take)
                .AsNoTracking()
                .ToListAsync();
        }

        private static bool IsSyncIdUniqueViolation<T>(ApplicationDbContext context, DbUpdateException ex)
            where T : class
        {
            var tableName = context.Model.FindEntityType(typeof(T))?.GetTableName();

            return ex.InnerException is SqlException sql
                && (sql.Number == 2601 || sql.Number == 2627)
                && sql.Message.Contains($"UX_{tableName}_SyncId");
        }
    }
}
