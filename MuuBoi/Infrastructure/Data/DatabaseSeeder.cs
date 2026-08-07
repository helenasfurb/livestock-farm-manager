using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MuuBoi.Models;

namespace MuuBoi.Data
{
    public static class DatabaseSeeder
    {
        private const string SystemUserEmail = "admin@muuboi.com.br";
        private const string SystemUserPassword = "Senha@123";
        public static readonly Guid SystemPropertyId = Guid.Parse("00000000-0000-0000-0000-000000000001");

        private static readonly (string Name, string Description)[] DefaultBreeds =
        [
            ("Nelore", "Raça bovina de corte mais popular do Brasil."),
            ("Angus", "Raça bovina de origem escocesa, valorizada pela qualidade da carne."),
            ("Brahman", "Raça zebuína adaptada ao clima tropical."),
            ("Hereford", "Raça bovina de corte de origem inglesa."),
            ("Girolando", "Raça leiteira desenvolvida no Brasil."),
            ("Holandesa", "Raça leiteira de alta produção."),
            ("Senepol", "Raça bovina sem chifres adaptada ao calor."),
            ("Simmental", "Raça de dupla aptidão, corte e leite."),
        ];

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

            var hasBreeds = await db.Breeds.IgnoreQueryFilters()
                .AnyAsync(b => b.PropertyId == SystemPropertyId);

            if (!hasBreeds)
                await CopyDefaultBreeds(db, SystemPropertyId);
        }

        public static async Task CopyDefaultBreeds(ApplicationDbContext db, Guid targetPropertyId)
        {
            var breeds = DefaultBreeds.Select(b => new Breed
            {
                Name = b.Name,
                Description = b.Description,
                PropertyId = targetPropertyId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });

            await db.Breeds.AddRangeAsync(breeds);
            await db.SaveChangesAsync();
        }
    }
}
