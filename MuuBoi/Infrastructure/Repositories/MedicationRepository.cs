using Microsoft.EntityFrameworkCore;
using MuuBoi.Application.Interfaces;
using MuuBoi.Data;
using MuuBoi.Models;

namespace MuuBoi.Infrastructure.Repositories
{
    public class MedicationRepository : IMedicationRepository
    {
        private readonly ApplicationDbContext _context;

        public MedicationRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Medication>> GetAllMedicationsAsync(string userId)
        {
            return await _context.Medications
                .Where(m => m.UserId == userId)
                .OrderBy(m => m.Name)
                .ToListAsync();
        }

        public async Task<Medication?> GetMedicationByIdAsync(int id)
        {
            return await _context.Medications.FirstOrDefaultAsync(m => m.Id == id);
        }

        public async Task<Medication> CreateMedicationAsync(Medication medication)
        {
            _context.Medications.Add(medication);
            await _context.SaveChangesAsync();
            return medication;
        }

        public async Task<Medication?> UpdateMedicationAsync(Medication medication)
        {
            _context.Medications.Update(medication);
            await _context.SaveChangesAsync();
            return medication;
        }

        public async Task<Medication?> DeleteMedicationAsync(int id)
        {
            var medication = await GetMedicationByIdAsync(id);
            if (medication == null) return null;

            _context.Medications.Remove(medication);
            await _context.SaveChangesAsync();
            return medication;
        }
    }
}
