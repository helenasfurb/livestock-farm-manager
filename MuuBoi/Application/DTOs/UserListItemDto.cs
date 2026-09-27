namespace MuuBoi.Application.DTOs
{
    public class UserListItemDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public EnumValueDto Role { get; set; } = new();
        public bool IsActive { get; set; }
    }
}
