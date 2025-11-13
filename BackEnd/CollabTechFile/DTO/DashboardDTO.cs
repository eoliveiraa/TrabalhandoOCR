namespace CollabTechFile.DTO
{
    public class DashBoardDTO
    {
        public int Pendentes { get; set; }
        public int Assinados { get; set; }
        public int Finalizados { get; set; }
        public List<ProximaEntregaDTO> ProximasEntregas { get; set; } = new();
    }
}
