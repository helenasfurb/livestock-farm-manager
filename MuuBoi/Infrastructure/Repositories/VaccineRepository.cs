using Microsoft.EntityFrameworkCore;
using MuuBoi.Application.Interfaces;
using MuuBoi.Data;
using MuuBoi.Models;

namespace MuuBoi.Infrastructure.Repositories
{
    public class VaccineRepository : IVaccineRepository
    {
        private readonly ApplicationDbContext _context;

        public VaccineRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Vaccine>> GetAllVaccinesAsync(string userId)
        {
            return await _context.Vaccines
                .Where(v => v.UserId == userId)
                .OrderBy(v => v.Name)
                .ToListAsync();
        }

        public async Task<Vaccine?> GetVaccineByIdAsync(int id)
        {
            return await _context.Vaccines.FirstOrDefaultAsync(v => v.Id == id);
        }

        public async Task<Vaccine> CreateVaccineAsync(Vaccine vaccine)
        {
            _context.Vaccines.Add(vaccine);
            await _context.SaveChangesAsync();
            return vaccine;
        }

        public async Task<Vaccine?> UpdateVaccineAsync(Vaccine vaccine)
        {
            _context.Vaccines.Update(vaccine);
            await _context.SaveChangesAsync();
            return vaccine;
        }

        public async Task<Vaccine?> DeleteVaccineAsync(int id)
        {
            var vaccine = await GetVaccineByIdAsync(id);
            if (vaccine == null) return null;

            _context.Vaccines.Remove(vaccine);
            await _context.SaveChangesAsync();
            return vaccine;
        }
    }
}
