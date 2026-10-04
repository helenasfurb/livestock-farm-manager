namespace MuuBoi.Application.DTOs
{
    public class SyncPageDto<T>
    {
        public IEnumerable<T> Items { get; set; } = new List<T>();
        public string NextCursor { get; set; } = string.Empty;
        public bool HasMore { get; set; }
    }
}
