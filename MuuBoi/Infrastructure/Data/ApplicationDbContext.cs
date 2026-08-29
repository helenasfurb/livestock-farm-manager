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
        public DbSet<SemenSampleMovement> SemenSampleMovements { get; set; }
        public DbSet<BreedingEvent> BreedingEvents { get; set; }
        public DbSet<AnimalPregnancy> AnimalPregnancies { get; set; }
        public DbSet<AnimalCalving> AnimalCalvings { get; set; }
        public DbSet<AnimalCalvingCalf> AnimalCalvingCalves { get; set; }

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

            builder.Entity<SemenSampleMovement>().HasQueryFilter(m => m.PropertyId == _propertyId);
            builder.Entity<SemenSampleMovement>()
                .HasOne(m => m.SemenSample)
                .WithMany(s => s.Movements)
                .HasForeignKey(m => m.SemenSampleId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.Entity<SemenSampleMovement>()
                .HasOne(m => m.BreedingEvent)
                .WithMany()
                .HasForeignKey(m => m.BreedingEventId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.Entity<SemenSampleMovement>()
                .HasIndex(m => new { m.SemenSampleId, m.MovementType, m.IsActive })
                .HasDatabaseName("IX_SemenSampleMovements_SemenSampleId_MovementType_IsActive");
            builder.Entity<SemenSampleMovement>()
                .HasIndex(m => m.BreedingEventId)
                .HasDatabaseName("IX_SemenSampleMovements_BreedingEventId");
            builder.Entity<SemenSampleMovement>()
                .HasIndex(m => new { m.PropertyId, m.IsActive })
                .HasDatabaseName("IX_SemenSampleMovements_PropertyId_IsActive");

            builder.Entity<BreedingEvent>().HasQueryFilter(e => e.PropertyId == _propertyId);

            builder.Entity<BreedingEvent>()
                .HasOne(e => e.Animal)
                .WithMany(a => a.BreedingEvents)
                .HasForeignKey(e => e.AnimalId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<BreedingEvent>()
                .HasOne(e => e.SireAnimal)
                .WithMany()
                .HasForeignKey(e => e.SireAnimalId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<BreedingEvent>()
                .HasIndex(e => new { e.AnimalId, e.BreedingDate })
                .HasDatabaseName("IX_BreedingEvents_AnimalId_BreedingDate");

            builder.Entity<BreedingEvent>()
                .HasIndex(e => new { e.AnimalId, e.Status, e.IsActive })
                .HasDatabaseName("IX_BreedingEvents_AnimalId_Status_IsActive");

            builder.Entity<BreedingEvent>()
                .HasIndex(e => e.SemenSampleId)
                .HasDatabaseName("IX_BreedingEvents_SemenSampleId");

            builder.Entity<AnimalPregnancy>().HasQueryFilter(p => p.PropertyId == _propertyId);

            builder.Entity<AnimalPregnancy>()
                .HasOne(p => p.Animal)
                .WithMany(a => a.Pregnancies)
                .HasForeignKey(p => p.AnimalId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<AnimalPregnancy>()
                .HasOne(p => p.BreedingEvent)
                .WithOne(e => e.Pregnancy)
                .HasForeignKey<AnimalPregnancy>(p => p.BreedingEventId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<AnimalPregnancy>()
                .HasIndex(p => p.BreedingEventId)
                .IsUnique()
                .HasDatabaseName("IX_AnimalPregnancies_BreedingEventId");

            builder.Entity<AnimalPregnancy>()
                .HasIndex(p => new { p.AnimalId, p.Status, p.IsActive })
                .HasDatabaseName("IX_AnimalPregnancies_AnimalId_Status_IsActive");

            builder.Entity<AnimalPregnancy>()
                .HasIndex(p => new { p.PropertyId, p.IsActive })
                .HasDatabaseName("IX_AnimalPregnancies_PropertyId_IsActive");

            builder.Entity<AnimalCalving>().HasQueryFilter(c => c.PropertyId == _propertyId);

            builder.Entity<AnimalCalving>()
                .HasOne(c => c.AnimalPregnancy)
                .WithMany(p => p.Calvings)
                .HasForeignKey(c => c.AnimalPregnancyId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<AnimalCalving>()
                .HasOne(c => c.Animal)
                .WithMany(a => a.Calvings)
                .HasForeignKey(c => c.AnimalId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<AnimalCalving>()
                .HasIndex(c => c.AnimalPregnancyId)
                .HasDatabaseName("IX_AnimalCalvings_AnimalPregnancyId");

            builder.Entity<AnimalCalving>()
                .HasIndex(c => new { c.AnimalId, c.CalvingDate })
                .HasDatabaseName("IX_AnimalCalvings_AnimalId_CalvingDate");

            builder.Entity<AnimalCalvingCalf>().HasQueryFilter(cf => cf.PropertyId == _propertyId);

            builder.Entity<AnimalCalvingCalf>()
                .HasOne(cf => cf.Calving)
                .WithMany(c => c.Calves)
                .HasForeignKey(cf => cf.CalvingId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<AnimalCalvingCalf>()
                .HasOne(cf => cf.Animal)
                .WithOne()
                .HasForeignKey<AnimalCalvingCalf>(cf => cf.AnimalId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<AnimalCalvingCalf>()
                .Property(cf => cf.WeightKg)
                .HasPrecision(6, 2);

            builder.Entity<AnimalCalvingCalf>()
                .HasIndex(cf => cf.CalvingId)
                .HasDatabaseName("IX_AnimalCalvingCalves_CalvingId");
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
