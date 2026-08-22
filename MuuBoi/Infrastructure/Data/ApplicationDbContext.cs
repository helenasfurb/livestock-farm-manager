using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MuuBoi.Application.Interfaces;
using MuuBoi.Domain.Models;

namespace MuuBoi.Infrastructure.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        private readonly Guid _propertyId;

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, ITenantProvider tenant)
            : base(options) => _propertyId = tenant.PropertyId;

        public DbSet<Property> Properties { get; set; }
        public DbSet<Animal> Animals { get; set; }
        public DbSet<WeightRecord> WeightRecords { get; set; }
        public DbSet<Vaccine> Vaccines { get; set; }
        public DbSet<AnimalVaccination> AnimalVaccinations { get; set; }
        public DbSet<Medication> Medications { get; set; }
        public DbSet<AnimalMedication> AnimalMedications { get; set; }
        public DbSet<BodyConditionRecord> BodyConditionRecords { get; set; }
        public DbSet<AnimalExitRecord> AnimalExitRecords { get; set; }
        public DbSet<SemenSample> SemenSamples { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<ApplicationUser>()
                .HasOne(u => u.Property)
                .WithMany(p => p.Users)
                .HasForeignKey(u => u.PropertyId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Animal>().HasQueryFilter(a => a.PropertyId == _propertyId);
            builder.Entity<Vaccine>().HasQueryFilter(v => v.PropertyId == _propertyId);
            builder.Entity<Medication>().HasQueryFilter(m => m.PropertyId == _propertyId);
            builder.Entity<WeightRecord>().HasQueryFilter(w => w.PropertyId == _propertyId);
            builder.Entity<AnimalVaccination>().HasQueryFilter(av => av.PropertyId == _propertyId);
            builder.Entity<AnimalMedication>().HasQueryFilter(am => am.PropertyId == _propertyId);

            builder.Entity<Animal>().HasIndex(a => a.PropertyId).HasDatabaseName("IX_Animals_PropertyId");
            builder.Entity<Vaccine>().HasIndex(v => v.PropertyId).HasDatabaseName("IX_Vaccines_PropertyId");
            builder.Entity<Medication>().HasIndex(m => m.PropertyId).HasDatabaseName("IX_Medications_PropertyId");
            builder.Entity<WeightRecord>().HasIndex(w => w.PropertyId).HasDatabaseName("IX_WeightRecords_PropertyId");
            builder.Entity<AnimalVaccination>().HasIndex(av => av.PropertyId).HasDatabaseName("IX_AnimalVaccinations_PropertyId");
            builder.Entity<AnimalMedication>().HasIndex(am => am.PropertyId).HasDatabaseName("IX_AnimalMedications_PropertyId");

            builder.Entity<BodyConditionRecord>()
                .HasIndex(r => new { r.AnimalId, r.RecordedAt })
                .HasDatabaseName("IX_BodyConditionRecords_AnimalId_RecordedAt");

            builder.Entity<AnimalExitRecord>()
                .HasIndex(r => new { r.AnimalId, r.ExitDate })
                .HasDatabaseName("IX_AnimalExitRecords_AnimalId_ExitDate");

            builder.Entity<AnimalExitRecord>()
                .HasIndex(r => new { r.ExitDate, r.ExitReason })
                .HasDatabaseName("IX_AnimalExitRecords_ExitDate_ExitReason");

            builder.Entity<SemenSample>().HasQueryFilter(s => s.PropertyId == _propertyId);
            builder.Entity<SemenSample>()
                .HasIndex(s => new { s.PropertyId, s.IsActive })
                .HasDatabaseName("IX_SemenSamples_PropertyId_IsActive");
        }

        public override Task<int> SaveChangesAsync(CancellationToken ct = default)
        {
            foreach (var entry in ChangeTracker.Entries<ITenantEntity>()
                                               .Where(e => e.State == EntityState.Added))
            {
                if (entry.Entity.PropertyId == Guid.Empty)
                    entry.Entity.PropertyId = _propertyId;
            }

            return base.SaveChangesAsync(ct);
        }
    }
}
