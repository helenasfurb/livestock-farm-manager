using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MuuBoi.Domain.Models;

namespace MuuBoi.Infrastructure.Data
{
    public static class DatabaseSeeder
    {
        private const string SystemUserEmail = "admin@muuboi.com.br";
        private const string SystemUserPassword = "Senha@123";
        public static readonly Guid SystemPropertyId = Guid.Parse("00000000-0000-0000-0000-000000000001");

        public static async Task SeedAsync(IServiceProvider services)
        {
            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
            var db = services.GetRequiredService<ApplicationDbContext>();

            var systemProperty = await db.Properties.FindAsync(SystemPropertyId);
            if (systemProperty == null)
            {
                systemProperty = new Property
                {
                    Id = SystemPropertyId,
                    Name = "Sistema MuuBoi",
                    CreatedAt = DateTime.UtcNow
                };
                db.Properties.Add(systemProperty);
                await db.SaveChangesAsync();
            }

            var systemUser = await userManager.FindByEmailAsync(SystemUserEmail);
            if (systemUser == null)
            {
                systemUser = new ApplicationUser
                {
                    UserName = SystemUserEmail,
                    Email = SystemUserEmail,
                    Name = "Sistema MuuBoi",
                    EmailConfirmed = true,
                    PropertyId = SystemPropertyId,
                    IsActive = true
                };
                await userManager.CreateAsync(systemUser, SystemUserPassword);
            }
            else if (systemUser.PropertyId != SystemPropertyId)
            {
                systemUser.PropertyId = SystemPropertyId;
                await userManager.UpdateAsync(systemUser);
            }

            // Backfill the default vaccine catalog for every existing property (idempotent).
            var propertyIds = await db.Properties.Select(p => p.Id).ToListAsync();
            foreach (var propertyId in propertyIds)
                await VaccineCatalogSeeder.SeedForPropertyAsync(db, propertyId);
        }
    }
}
