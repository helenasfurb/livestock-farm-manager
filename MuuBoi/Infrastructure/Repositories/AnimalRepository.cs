using Microsoft.EntityFrameworkCore;
using MuuBoi.Data;
using MuuBoi.Interfaces;
using MuuBoi.Models;

namespace MuuBoi.Repositories
{
    public class AnimalRepository : IAnimalRepository
    {
        private readonly ApplicationDbContext _context;

        public AnimalRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Animal> CreateAnimalAsync(Animal animal)
        {
            _context.Animals.Add(animal);
            await _context.SaveChangesAsync();
            return animal;
        }

        public async Task<Animal?> DeleteAnimalAsync(int id)
        {
            var animal = await _context.Animals.FindAsync(id);
            if (animal == null) return null;

            _context.Animals.Remove(animal);
            await _context.SaveChangesAsync();
            return animal;
        }

        public async Task<IEnumerable<Animal>> GetAllAnimalsAsync()
        {
            return await _context.Animals.Include(a => a.Breed).ToListAsync();
        }

        public async Task<Animal?> GetAnimalByIdAsync(int id)
        {
            return await _context.Animals
                .Include(a => a.Breed)
                .Include(a => a.WeightRecords!.OrderBy(w => w.RecordedAt))
                .FirstOrDefaultAsync(a => a.Id == id);
        }

        public async Task<Animal?> UpdateAnimalAsync(Animal animal)
        {
            _context.Animals.Update(animal);
            await _context.SaveChangesAsync();
            return animal;
        }
    }
}
