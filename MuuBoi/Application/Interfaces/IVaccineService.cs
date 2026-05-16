using MuuBoi.Application.DTOs;
using MuuBoi.DTOs;

namespace MuuBoi.Application.Interfaces
{
    public interface IVaccineService
    {
        Task<IEnumerable<VaccineDto>> GetAllVaccinesAsync(string userId);
        Task<VaccineDto?> GetVaccineByIdAsync(int id, string userId);
        Task<VaccineDto> CreateVaccineAsync(VaccineCreateDto dto, string userId);
        Task<VaccineDto?> UpdateVaccineAsync(int id, VaccineUpdateDto dto, string userId);
        Task<VaccineDto?> DeleteVaccineAsync(int id, string userId);
    }
}
