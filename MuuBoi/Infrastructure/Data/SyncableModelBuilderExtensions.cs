using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MuuBoi.Domain.Models;

namespace MuuBoi.Infrastructure.Data
{
    public static class SyncableModelBuilderExtensions
    {
        public static EntityTypeBuilder<T> ConfigureSyncable<T>(this EntityTypeBuilder<T> builder)
            where T : class, ISyncable, ITenantEntity
        {
            var tableName = builder.Metadata.GetTableName();

            builder.Property(nameof(ISyncable.SyncId))
                .HasDefaultValueSql("NEWID()");

            builder.HasIndex(nameof(ISyncable.SyncId))
                .IsUnique()
                .HasDatabaseName($"UX_{tableName}_SyncId");

            builder.Property(nameof(ISyncable.RowVersion))
                .IsRowVersion();

            builder.HasIndex(nameof(ITenantEntity.PropertyId), nameof(ISyncable.RowVersion))
                .HasDatabaseName($"IX_{tableName}_PropertyId_RowVersion");

            return builder;
        }
    }
}
