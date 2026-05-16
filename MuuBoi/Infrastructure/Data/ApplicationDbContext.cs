using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MuuBoi.Models;

namespace MuuBoi.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Animal> Animals { get; set; }
        public DbSet<Breed> Breeds { get; set; }
        public DbSet<WeightRecord> WeightRecords { get; set; }
        public DbSet<Vaccine> Vaccines { get; set; }
        public DbSet<AnimalVaccination> AnimalVaccinations { get; set; }
        public DbSet<Medication> Medications { get; set; }
        public DbSet<AnimalMedication> AnimalMedications { get; set; }
    }
}
