
namespace CollabTechFile.DTO
{
    public class DocumentoDTO
    {
        public string? Nome { get; set; }
        public int IdUsuario { get; set; }
        public int Prazo { get; set; }
        public bool Status { get; set; }
        public decimal Versao { get; set; }
        public DateTime CriadoEm { get; set; }
        public int IdEmpresa { get; set; }

      public IFormFile? Arquivo { get; set; }

    }
}
