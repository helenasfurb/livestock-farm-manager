namespace MuuBoi.Application.DTOs
{
    public class PortalTokenDto
    {
        public string Token { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
    }
}
