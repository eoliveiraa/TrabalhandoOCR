

namespace CollabTechFile.DTO
{
    public class ProximaEntregaDTO
    {
        public int IdDocumento { get; set; }
        public string? Nome { get; set; }
        public DateOnly? Prazo { get; set; }
        public string? NovoStatus { get; set; }
    }
}
