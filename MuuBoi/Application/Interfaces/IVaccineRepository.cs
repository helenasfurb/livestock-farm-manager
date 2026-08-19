using MuuBoi.Domain.Models;

namespace MuuBoi.Application.Interfaces
{
    public interface IVaccineRepository
    {
        Task<IEnumerable<Vaccine>> GetAllVaccinesAsync();
        Task<Vaccine?> GetVaccineByIdAsync(int id);
        Task<Vaccine> CreateVaccineAsync(Vaccine vaccine);
        Task<Vaccine?> UpdateVaccineAsync(Vaccine vaccine);
        Task<Vaccine?> DeleteVaccineAsync(int id);
    }
}
