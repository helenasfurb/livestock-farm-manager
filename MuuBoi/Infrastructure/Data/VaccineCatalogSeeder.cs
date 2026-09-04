using Microsoft.EntityFrameworkCore;
using MuuBoi.Domain.Models;

namespace MuuBoi.Infrastructure.Data
{
    /// <summary>
    /// Seeds the default vaccine catalog for a property (the catalog is per-farm). Idempotent by
    /// name: only the vaccines missing for that property are inserted. PropertyId is set explicitly
    /// (registration/startup run without a tenant), so the DbContext auto-stamp is bypassed.
    /// RequiresBooster defaults to false — purely informational, adjust per vaccine as needed.
    /// </summary>
    public static class VaccineCatalogSeeder
    {
        private static readonly string[] DefaultVaccineNames =
        {
            "BVD/IBR/Leptospirose",
            "Clostridioses",
            "Brucelose",
            "Raiva"
        };

        public static async Task SeedForPropertyAsync(ApplicationDbContext db, Guid propertyId)
        {
            var existingNames = await db.Vaccines
                .IgnoreQueryFilters()
                .Where(v => v.PropertyId == propertyId)
                .Select(v => v.Name)
                .ToListAsync();

            var toAdd = DefaultVaccineNames
                .Where(name => !existingNames.Contains(name))
                .Select(name => new Vaccine
                {
                    Name = name,
                    RequiresBooster = true,
                    PropertyId = propertyId,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                })
                .ToList();

            if (toAdd.Count == 0)
                return;

            db.Vaccines.AddRange(toAdd);
            await db.SaveChangesAsync();
        }
    }
}
