using Microsoft.EntityFrameworkCore;
using MuuBoi.Application.Interfaces;
using MuuBoi.Data;
using MuuBoi.Models;

namespace MuuBoi.Infrastructure.Repositories
{
    public class AnimalMedicationRepository : IAnimalMedicationRepository
    {
        private readonly ApplicationDbContext _context;

        public AnimalMedicationRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<AnimalMedication>> GetAllAnimalMedicationsAsync(int animalId)
        {
            return await _context.AnimalMedications
                .Include(am => am.Medication)
                .Where(am => am.AnimalId == animalId)
                .OrderByDescending(am => am.StartDate)
                .ToListAsync();
        }

        public async Task<AnimalMedication?> GetAnimalMedicationByIdAsync(int id, int animalId)
        {
            return await _context.AnimalMedications
                .Include(am => am.Medication)
                .FirstOrDefaultAsync(am => am.Id == id && am.AnimalId == animalId);
        }

        public async Task<AnimalMedication> CreateAnimalMedicationAsync(AnimalMedication medication)
        {
            _context.AnimalMedications.Add(medication);
            await _context.SaveChangesAsync();
            return medication;
        }

        public async Task<AnimalMedication?> UpdateAnimalMedicationAsync(AnimalMedication medication)
        {
            _context.AnimalMedications.Update(medication);
            await _context.SaveChangesAsync();
            return medication;
        }

        public async Task<AnimalMedication?> DeleteAnimalMedicationAsync(int id, int animalId)
        {
            var animalMedication = await _context.AnimalMedications
                .FirstOrDefaultAsync(am => am.Id == id && am.AnimalId == animalId);
            if (animalMedication == null) return null;

            _context.AnimalMedications.Remove(animalMedication);
            await _context.SaveChangesAsync();
            return animalMedication;
        }
    }
}
