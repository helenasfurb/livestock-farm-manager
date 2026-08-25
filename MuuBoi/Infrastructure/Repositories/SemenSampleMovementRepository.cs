using Microsoft.EntityFrameworkCore;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Interfaces;
using MuuBoi.Domain.Models;
using MuuBoi.Infrastructure.Data;

namespace MuuBoi.Infrastructure.Repositories
{
    public class SemenSampleMovementRepository : ISemenSampleMovementRepository
    {
        private readonly ApplicationDbContext _context;

        public SemenSampleMovementRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<SemenSampleMovement>> GetBySemenSampleIdAsync(int semenSampleId, SemenSampleMovementFilterDto filter)
        {
            var query = _context.SemenSampleMovements
                .Where(m => m.SemenSampleId == semenSampleId);

            if (filter.MovementType.HasValue)
                query = query.Where(m => m.MovementType == filter.MovementType);

            return await query.OrderByDescending(m => m.MovementDate).ToListAsync();
        }

        public async Task<SemenSampleMovement?> GetByIdAsync(int id)
        {
            return await _context.SemenSampleMovements
                .Include(m => m.SemenSample)
                .FirstOrDefaultAsync(m => m.Id == id);
        }

        public async Task<SemenSampleMovement?> GetByBreedingEventIdAsync(int breedingEventId)
        {
            return await _context.SemenSampleMovements
                .FirstOrDefaultAsync(m => m.BreedingEventId == breedingEventId);
        }

        public async Task<SemenSampleMovement> CreateAsync(SemenSampleMovement movement)
        {
            _context.SemenSampleMovements.Add(movement);
            await _context.SaveChangesAsync();
            return movement;
        }

        public async Task<SemenSampleMovement> UpdateAsync(SemenSampleMovement movement)
        {
            _context.SemenSampleMovements.Update(movement);
            await _context.SaveChangesAsync();
            return movement;
        }
    }
}
