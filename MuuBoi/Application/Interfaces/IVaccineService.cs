using MuuBoi.Application.DTOs;

namespace MuuBoi.Application.Interfaces
{
    public interface IVaccineService
    {
        Task<IEnumerable<VaccineDto>> GetAllVaccinesAsync();
        Task<VaccineDto?> GetVaccineByIdAsync(int id);
        Task<VaccineDto> CreateVaccineAsync(VaccineCreateDto dto);
        Task<VaccineDto?> UpdateVaccineAsync(int id, VaccineUpdateDto dto);
        Task<VaccineDto?> DeleteVaccineAsync(int id);
    }
}
