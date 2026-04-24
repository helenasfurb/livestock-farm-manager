using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MuuBoi.Models;

namespace MuuBoi.Data
{
    public static class DatabaseSeeder
    {
        private const string SystemUserEmail = "admin@muuboi.com.br";
        private const string SystemUserPassword = "Senha@123";

        public static async Task SeedAsync(IServiceProvider services)
        {
            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
            var db = services.GetRequiredService<ApplicationDbContext>();

            var systemUser = await userManager.FindByEmailAsync(SystemUserEmail);

            if (systemUser == null)
            {
                systemUser = new ApplicationUser
                {
                    UserName = SystemUserEmail,
                    Email = SystemUserEmail,
                    FullName = "Sistema MuuBoi",
                    EmailConfirmed = true
                };

                await userManager.CreateAsync(systemUser, SystemUserPassword);
            }

            var hasBreeds = await db.Breeds.AnyAsync();
            if (hasBreeds) return;

            var breeds = new List<Breed>
            {
                new() { Name = "Nelore", Description = "Raça bovina de corte mais popular do Brasil.", UserId = systemUser.Id },
                new() { Name = "Angus", Description = "Raça bovina de origem escocesa, valorizada pela qualidade da carne.", UserId = systemUser.Id },
                new() { Name = "Brahman", Description = "Raça zebuína adaptada ao clima tropical.", UserId = systemUser.Id },
                new() { Name = "Hereford", Description = "Raça bovina de corte de origem inglesa.", UserId = systemUser.Id },
                new() { Name = "Girolando", Description = "Raça leiteira desenvolvida no Brasil.", UserId = systemUser.Id },
                new() { Name = "Holandesa", Description = "Raça leiteira de alta produção.", UserId = systemUser.Id },
                new() { Name = "Senepol", Description = "Raça bovina sem chifres adaptada ao calor.", UserId = systemUser.Id },
                new() { Name = "Simmental", Description = "Raça de dupla aptidão, corte e leite.", UserId = systemUser.Id },
            };

            await db.Breeds.AddRangeAsync(breeds);
            await db.SaveChangesAsync();
        }
    }
}
