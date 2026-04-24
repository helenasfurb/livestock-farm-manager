using Microsoft.EntityFrameworkCore;
using MuuBoi.Data;
using MuuBoi.Interfaces;
using MuuBoi.Models;

namespace MuuBoi.Repositories
{
    public class BreedRepository : IBreedRepository
    {
        private readonly ApplicationDbContext _context;

        public BreedRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Breed> CreateBreedAsync(Breed breed)
        {
            _context.Breeds.Add(breed);
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

        public async Task<IEnumerable<Breed>> GetAllBreedsAsync(string userId)
        {
            return await _context.Breeds.Where(b => b.UserId == userId).ToListAsync();
        }

        public async Task<Breed?> GetBreedByIdAsync(int id)
        {
            return await _context.Breeds.FindAsync(id);
        }

        public async Task<Breed?> UpdateBreedAsync(Breed breed)
        {
            _context.Breeds.Update(breed);
            await _context.SaveChangesAsync();
            return breed;
        }
    }
}
