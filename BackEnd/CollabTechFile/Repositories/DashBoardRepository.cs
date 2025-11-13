using CollabTechFile.DTO;
using CollabTechFile.Interfaces;

public class DashBoardRepository : IDashBoardRepository
{
    private readonly IDocumentoRepository _documentoRepository;

    public DashBoardRepository(IDocumentoRepository documentoRepository)
    {
        _documentoRepository = documentoRepository;
    }

    public DashBoardDTO ObterDashboard()
    {
        var documentos = _documentoRepository.Listar();

        int totalPendentes = documentos.Count(d => d.NovoStatus == "Pendente");
        int totalFinalizados = documentos.Count(d => d.NovoStatus == "Finalizado");
        int totalAssinados = documentos.Count(d => d.NovoStatus == "Assinado");

        var proximasEntregas = documentos
            .Where(d => d.Prazo.HasValue && d.Prazo.Value >= DateOnly.FromDateTime(DateTime.Now))
            .OrderBy(d => d.Prazo)
            .Take(5)
            .Select(d => new ProximaEntregaDTO
            {
                IdDocumento = d.IdDocumento,
                Nome = d.Nome,
                Prazo = d.Prazo,
                NovoStatus = d.NovoStatus
            })
            .ToList();

        return new DashBoardDTO
        {
            Pendentes = totalPendentes,
            Finalizados = totalFinalizados,
            Assinados = totalAssinados,
            ProximasEntregas = proximasEntregas
        };
    }
}

