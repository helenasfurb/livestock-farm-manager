using Microsoft.EntityFrameworkCore;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Interfaces;
using MuuBoi.Infrastructure.Data;
using MuuBoi.Domain.Models;

namespace MuuBoi.Infrastructure.Repositories
{
    public class VaccineRepository : IVaccineRepository
    {
        private readonly ApplicationDbContext _context;

        public VaccineRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Vaccine>> GetAllVaccinesAsync(VaccineFilterDto filter)
        {
            var query = _context.Vaccines.AsQueryable();

            if (!string.IsNullOrWhiteSpace(filter.Name))
                query = query.Where(v => v.Name.Contains(filter.Name));

            if (filter.IsActive.HasValue)
                query = query.Where(v => v.IsActive == filter.IsActive.Value);

            return await query.OrderBy(v => v.Name).ToListAsync();
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

            // Soft delete: keep the row, flag it inactive (padrão do projeto).
            vaccine.IsActive = false;
            vaccine.UpdatedAt = DateTime.UtcNow;
            _context.Vaccines.Update(vaccine);
            await _context.SaveChangesAsync();
            return vaccine;
        }
    }
}
