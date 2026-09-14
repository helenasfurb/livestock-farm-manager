namespace MuuBoi.Application.DTOs
{
    public class GenealogyFatherDto
    {
        public string Type { get; set; } = string.Empty;
        public AnimalRefDto? Bull { get; set; }
        public SemenSireRefDto? Semen { get; set; }
    }
}
