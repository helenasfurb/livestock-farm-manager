using MuuBoi.Domain.Models;

namespace MuuBoi.Application.Helpers
{
    public static class SyncTimestampResolver
    {
        public static DateTime ResolveEditedAt(DateTime? clientUpdatedAt, DateTime now)
        {
            if (!clientUpdatedAt.HasValue)
                return now;

            var utc = clientUpdatedAt.Value.Kind switch
            {
                DateTimeKind.Local => clientUpdatedAt.Value.ToUniversalTime(),
                DateTimeKind.Unspecified => DateTime.SpecifyKind(clientUpdatedAt.Value, DateTimeKind.Utc),
                _ => clientUpdatedAt.Value
            };

            return utc < now ? utc : now;
        }

        public static bool IsOutdated(DateTime editedAt, BaseEntity entity)
        {
            return editedAt < (entity.UpdatedAt ?? entity.CreatedAt);
        }
    }
}
