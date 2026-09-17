using AutoMapper;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Helpers;
using MuuBoi.Domain.Enums;
using MuuBoi.Domain.Models;

namespace MuuBoi.Application.Mappings
{
    public class StockProfile : Profile
    {
        public StockProfile()
        {
            CreateMap<StockCategory, StockCategoryDto>();
            CreateMap<UnitOfMeasure, UnitOfMeasureDto>();

            CreateMap<StockItemCreateDto, StockItem>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.IsActive, opt => opt.MapFrom(_ => true))
                .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(_ => DateTime.UtcNow))
                .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.PropertyId, opt => opt.Ignore())
                .ForMember(dest => dest.StockCategory, opt => opt.Ignore())
                .ForMember(dest => dest.UnitOfMeasure, opt => opt.Ignore())
                .ForMember(dest => dest.Movements, opt => opt.Ignore());

            CreateMap<StockItemUpdateDto, StockItem>()
                .ForAllMembers(opt => opt.Condition((src, dest, srcMember) => srcMember != null));

            CreateMap<StockItem, StockItemDto>()
                .ForMember(dest => dest.CurrentBalance, opt => opt.Ignore())
                .ForMember(dest => dest.StockValue, opt => opt.Ignore())
                .ForMember(dest => dest.AverageUnitCost, opt => opt.Ignore())
                .ForMember(dest => dest.DaysOfCoverage, opt => opt.Ignore())
                .ForMember(dest => dest.EstimatedRunOutDate, opt => opt.Ignore())
                .ForMember(dest => dest.AlertSeverity, opt => opt.Ignore());

            CreateMap<StockItem, StockItemListItemDto>()
                .ForMember(dest => dest.CategoryName,
                    opt => opt.MapFrom(src => src.StockCategory != null ? src.StockCategory.Name : string.Empty))
                .ForMember(dest => dest.UnitAbbreviation,
                    opt => opt.MapFrom(src => src.UnitOfMeasure != null ? src.UnitOfMeasure.Abbreviation : null))
                .ForMember(dest => dest.CurrentBalance, opt => opt.Ignore())
                .ForMember(dest => dest.StockValue, opt => opt.Ignore())
                .ForMember(dest => dest.AlertSeverity, opt => opt.Ignore());

            CreateMap<StockMovementCreateDto, StockMovement>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.IsActive, opt => opt.MapFrom(_ => true))
                .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(_ => DateTime.UtcNow))
                .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.PropertyId, opt => opt.Ignore())
                .ForMember(dest => dest.StockItemId, opt => opt.Ignore())
                .ForMember(dest => dest.StockItem, opt => opt.Ignore())
                .ForMember(dest => dest.TotalValue, opt => opt.Ignore())
                .ForMember(dest => dest.ValueEntryMode, opt => opt.Ignore())
                .ForMember(dest => dest.UnitCostSnapshot, opt => opt.Ignore());

            CreateMap<StockMovement, StockMovementDto>()
                .ForMember(dest => dest.StockItemName,
                    opt => opt.MapFrom(src => src.StockItem != null ? src.StockItem.Name : string.Empty))
                .ForMember(dest => dest.UnitAbbreviation,
                    opt => opt.MapFrom(src => src.StockItem != null && src.StockItem.UnitOfMeasure != null
                        ? src.StockItem.UnitOfMeasure.Abbreviation
                        : null))
                .ForMember(dest => dest.MovementType,
                    opt => opt.MapFrom(src => src.MovementType.ToEnumValue()))
                .ForMember(dest => dest.MovementReason,
                    opt => opt.MapFrom(src => src.MovementReason.ToEnumValue()))
                .ForMember(dest => dest.UnitCost,
                    opt => opt.MapFrom(src => src.MovementType == StockMovementType.Output
                        ? src.UnitCostSnapshot
                        : (src.Quantity != 0 && src.TotalValue != null
                            ? src.TotalValue / src.Quantity
                            : (decimal?)null)));

            CreateMap<StockMovement, StockMovementListItemDto>()
                .ForMember(dest => dest.UnitAbbreviation,
                    opt => opt.MapFrom(src => src.StockItem != null && src.StockItem.UnitOfMeasure != null
                        ? src.StockItem.UnitOfMeasure.Abbreviation
                        : null))
                .ForMember(dest => dest.MovementType,
                    opt => opt.MapFrom(src => src.MovementType.ToEnumValue()))
                .ForMember(dest => dest.MovementReason,
                    opt => opt.MapFrom(src => src.MovementReason.ToEnumValue()));
        }
    }
}
