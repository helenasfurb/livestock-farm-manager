using Microsoft.EntityFrameworkCore;
using MuuBoi.Infrastructure.Data;
using MuuBoi.Application.Interfaces;
using MuuBoi.Domain.Models;

namespace MuuBoi.Infrastructure.Repositories
{
    public class BreedRepository : IBreedRepository
    {
        private readonly ApplicationDbContext _context;

        public BreedRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Breed>> GetAllBreedsAsync()
        {
            return await _context.Breeds.OrderBy(b => b.Name).ToListAsync();
        }

        public async Task<Breed?> GetBreedByIdAsync(int id)
        {
            return await _context.Breeds.FindAsync(id);
        }

        public async Task<Breed> CreateBreedAsync(Breed breed)
        {
            _context.Breeds.Add(breed);
            await _context.SaveChangesAsync();
            return breed;
        }

        public async Task<Breed?> UpdateBreedAsync(Breed breed)
        {
            _context.Breeds.Update(breed);
            await _context.SaveChangesAsync();
            return breed;
        }

        public async Task<Breed?> DeleteBreedAsync(int id)
        {
            var breed = await _context.Breeds.FindAsync(id);
            if (breed == null) return null;

            _context.Breeds.Remove(breed);
            await _context.SaveChangesAsync();
            return breed;
        }
    }
}
