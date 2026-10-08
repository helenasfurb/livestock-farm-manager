namespace MuuBoi.Domain.Models
{
    public interface ISyncable
    {
        Guid SyncId { get; set; }

        byte[] RowVersion { get; set; }
    }
}
