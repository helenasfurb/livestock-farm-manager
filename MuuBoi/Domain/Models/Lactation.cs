using System.ComponentModel.DataAnnotations;
using MuuBoi.Domain.Enums;

namespace MuuBoi.Domain.Models
{
    public class Lactation : BaseEntity, ITenantEntity, ISyncable
    {
        public int AnimalId { get; set; }

        public DateTime StartDate { get; set; }          // data do parto — abre a lactação

        public DateTime? EndDate { get; set; }           // data da secagem; null = em lactação

        public int? CalvingId { get; set; }              // elo ao parto (D10); null em InitialSeed (D11)

        public LactationOrigin Origin { get; set; }      // Calving | InitialSeed

        [MaxLength(500)]
        public string? DryOffNotes { get; set; }         // observações da secagem (D16)

        public Guid PropertyId { get; set; }

        public Guid SyncId { get; set; }
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        public Animal? Animal { get; set; }
        public AnimalCalving? Calving { get; set; }
    }
}
