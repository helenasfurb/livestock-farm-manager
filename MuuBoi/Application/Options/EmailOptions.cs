namespace MuuBoi.Application.Options
{
    public class EmailOptions
    {
        public string SmtpHost { get; set; } = string.Empty;
        public int SmtpPort { get; set; } = 587;
        public string FromEmail { get; set; } = string.Empty;
        public string FromName { get; set; } = "MuuBoi";
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
