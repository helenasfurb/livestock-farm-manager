using Microsoft.EntityFrameworkCore;
using MuuBoi.Application.Interfaces;
using MuuBoi.Data;
using MuuBoi.Models;

namespace MuuBoi.Infrastructure.Repositories
{
    public class AnimalVaccinationRepository : IAnimalVaccinationRepository
    {
        private readonly ApplicationDbContext _context;

        public AnimalVaccinationRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<AnimalVaccination>> GetAllAnimalVaccinationsAsync(int animalId)
        {
            return await _context.AnimalVaccinations
                .Include(av => av.Vaccine)
                .Where(av => av.AnimalId == animalId)
                .OrderByDescending(av => av.ApplicationDate)
                .ToListAsync();
        }

        public async Task<AnimalVaccination?> GetAnimalVaccinationByIdAsync(int id, int animalId)
        {
            return await _context.AnimalVaccinations
                .Include(av => av.Vaccine)
                .FirstOrDefaultAsync(av => av.Id == id && av.AnimalId == animalId);
        }

        public async Task<AnimalVaccination> CreateAnimalVaccinationAsync(AnimalVaccination vaccination)
        {
            _context.AnimalVaccinations.Add(vaccination);
            await _context.SaveChangesAsync();
            return vaccination;
        }

        public async Task<AnimalVaccination?> UpdateAnimalVaccinationAsync(AnimalVaccination vaccination)
        {
            _context.AnimalVaccinations.Update(vaccination);
            await _context.SaveChangesAsync();
            return vaccination;
        }

        public async Task<AnimalVaccination?> DeleteAnimalVaccinationAsync(int id, int animalId)
        {
            var vaccination = await _context.AnimalVaccinations
                .FirstOrDefaultAsync(av => av.Id == id && av.AnimalId == animalId);
            if (vaccination == null) return null;

            _context.AnimalVaccinations.Remove(vaccination);
            await _context.SaveChangesAsync();
            return vaccination;
        }
    }
}
