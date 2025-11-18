using CollabTechFile.Models;
using CollabTechFile.Interfaces;
using CollabTechFile.Services;
using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Threading.Tasks;
using CollabTechFile.DTO;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using System.Text;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using Azure;


namespace CollabTechFile.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DocumentosController : ControllerBase
    {
        private readonly IDocumentoRepository _documentoRepository;
        private readonly OCRService _ocrService;

        public DocumentosController(IDocumentoRepository documentoRepository, OCRService ocrService)
        {
            _documentoRepository = documentoRepository;
            _ocrService = ocrService;
        }

        // LISTAR ATIVOS
        [HttpGet]
        public IActionResult Get()
        {
            var documentos = _documentoRepository.Listar()
                .Where(d => d.Status == true);

            return Ok(documentos);
        }

        [HttpGet("Lixeira")]
        public IActionResult ListarLixeira()
        {
            var documentos = _documentoRepository.Listar()
                .Where(d => d.Status == false);

            return Ok(documentos);
        }

        [HttpPut("Inativar/{id}")]
        public IActionResult Inativar(int id)
        {
            var documento = _documentoRepository.BuscarPorId(id);
            if (documento == null)
                return NotFound("Documento não encontrado.");

            documento.Status = false;
            _documentoRepository.Editar(id, documento);

            return Ok("Documento movido para a lixeira com sucesso.");
        }

        // 
        [HttpPut("Restaurar/{id}")]
        public IActionResult Restaurar(int id)
        {
            var documento = _documentoRepository.BuscarPorId(id);
            if (documento == null)
                return NotFound("Documento não encontrado.");

            documento.Status = true;
            _documentoRepository.Editar(id, documento);

            return Ok("Documento restaurado com sucesso.");
        }

        [HttpDelete("Excluir/{id}")]
        public IActionResult Excluir(int id)
        {
            var documento = _documentoRepository.BuscarPorId(id);
            if (documento == null)
                return NotFound("Documento não encontrado.");

            _documentoRepository.Deletar(id);
            return Ok("Documento excluído permanentemente.");
        }

        [HttpPost("analisar")]
        public async Task<IActionResult> Analisar(IFormFile arquivo,
    [FromServices] IConfiguration _config)
        {
            if (arquivo == null || arquivo.Length == 0)
                return BadRequest("Nenhum arquivo foi enviado.");

            try
            {
                string endpoint = _config["AzureDocIntelligence:Endpoint"];
                string apiKey = _config["AzureDocIntelligence:Key"];
                string modelId = _config["AzureDocIntelligence:ModelId"]; // modelo treinado

                var client = new DocumentAnalysisClient(
                    new Uri(endpoint),
                    new AzureKeyCredential(apiKey)
                );

                using var stream = arquivo.OpenReadStream();

                // executa a IA
                AnalyzeDocumentOperation operation =
                    await client.AnalyzeDocumentAsync(WaitUntil.Completed, modelId, stream);

                AnalyzeResult result = operation.Value;

                var documentos = new List<object>();

                foreach (var document in result.Documents)
                {
                    var campos = new Dictionary<string, object>();

                    foreach (var campo in document.Fields)
                    {
                        campos.Add(campo.Key, new
                        {
                            Conteudo = campo.Value.Content,
                            Confianca = campo.Value.Confidence
                        });
                    }

                    documentos.Add(new
                    {
                        Tipo = document.DocumentType,
                        Campos = campos
                    });
                }

                return Ok(new
                {
                    Modelo = result.ModelId,
                    Documentos = documentos
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erro ao analisar documento: {ex.Message}");
            }
        }


        [HttpPost("upload-ocr")]
        public async Task<IActionResult> UploadOCR([FromForm] DocumentoDTO request)
        {

            // Adjusting the code to handle the correct type of `camposExtraidos`

            if (request.Arquivo == null || request.Arquivo.Length == 0)
                return BadRequest("Nenhum arquivo enviado.");

            try
            {
                // 1. Ler PDF em memória
                var ms = new MemoryStream();
                request.Arquivo.CopyTo(ms);
                var fileBytes = ms.ToArray();

                // 2. OCR direto dos bytes (sem arquivo temporário)
                string modelId = "modelov3.1";
                var textoExtraido = await _ocrService.ExtrairTextoAsync(fileBytes, modelId);

                // 3. Criar comentários OCR
                var comentarios = new List<Comentario>();

                Documento novoDocumento = new Documento
                {
                    Nome = request.Nome,
                    IdUsuario = request.IdUsuario,
                    CriadoEm = DateTime.Now,
                    NovoStatus = "Pendente"
                };

                

                if (string.IsNullOrWhiteSpace(request.Nome))
                    return BadRequest("O campo 'Titulo' do documento é obrigatório.");

                // 4. Salvar no BD
                try
                {
                    novoDocumento.Comentarios = comentarios;
                    _documentoRepository.Cadastrar(novoDocumento);
                }
                catch (Exception dbEx)
                {
                    var mensagemErro = dbEx.InnerException?.Message ?? dbEx.Message;
                    return StatusCode(500, $"Erro ao salvar no banco: {mensagemErro}");
                }

                return StatusCode(201, novoDocumento);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erro ao processar documento: {ex.Message}");
            }
        }

        [HttpGet("{id}/pdf")]
        public IActionResult DownloadPdf(int id)
        {
            var documento = _documentoRepository.BuscarPorId(id);

            if (documento == null || documento.Arquivo == null)
                return NotFound("PDF não encontrado.");

            return File(
                fileContents: documento.Arquivo,
                contentType: documento.MimeType ?? "application/pdf",
                fileDownloadName: documento.Nome + ".pdf"
            );
        }

        [HttpPut("{id}/upload")]
        public async Task<IActionResult> AtualizarArquivo(int id, IFormFile arquivo)
        {
            if (arquivo == null || arquivo.Length == 0)
                return BadRequest("Nenhum arquivo enviado.");

            var documento = _documentoRepository.BuscarPorId(id);

            if (documento == null)
                return NotFound("Documento não encontrado.");

            using var ms = new MemoryStream();
            await arquivo.CopyToAsync(ms);

            documento.Arquivo = ms.ToArray();
            documento.MimeType = arquivo.ContentType;

            try
            {
                _documentoRepository.Editar(id, documento);
                return Ok("Arquivo atualizado com sucesso.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erro ao atualizar arquivo: {ex.Message}");
            }
        }
    }
}
