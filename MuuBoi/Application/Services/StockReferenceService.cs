using AutoMapper;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Interfaces;

namespace MuuBoi.Application.Services
{
    public class StockReferenceService : IStockReferenceService
    {
        private readonly IStockReferenceRepository _repository;
        private readonly IMapper _mapper;

        public StockReferenceService(IStockReferenceRepository repository, IMapper mapper)
        {
            _repository = repository;
            _mapper = mapper;
        }

        public async Task<IEnumerable<StockCategoryDto>> GetCategoriesAsync()
        {
            var categories = await _repository.GetCategoriesAsync();
            return _mapper.Map<IEnumerable<StockCategoryDto>>(categories);
        }

        public async Task<IEnumerable<UnitOfMeasureDto>> GetUnitsAsync()
        {
            var units = await _repository.GetUnitsAsync();
            return _mapper.Map<IEnumerable<UnitOfMeasureDto>>(units);
        }
    }
}
