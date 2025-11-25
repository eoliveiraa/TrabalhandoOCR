using CollabTechFile.Models;
using CollabTechFile.Interfaces;
using CollabTechFile.Services;
using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Threading.Tasks;
using CollabTechFile.DTO;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using Azure;
using System.Globalization;

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



        [HttpGet]
        public IActionResult Get()
        {
            var documentos = _documentoRepository.Listar()
                .Where(d => d.Status == true);

            return Ok(documentos);
        }


        [HttpPost]
        public IActionResult Post([FromBody] Documento documento)
        {
            try
            {
                if (documento == null)
                    return BadRequest("Os dados do documento não foram enviados.");
                documento.CriadoEm = DateTime.Now;
                documento.Status = true;
                _documentoRepository.Cadastrar(documento);
                return StatusCode(201, documento);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erro ao cadastrar documento: {ex.Message}");
            }
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


        [HttpPost("upload-ocr")]
        public async Task<IActionResult> UploadOCR([FromForm] DocumentoDTO request)
        {
            if (request.Arquivo == null || request.Arquivo.Length == 0)
                return BadRequest("Nenhum arquivo enviado.");

            try
            {
                using var ms = new MemoryStream();
                await request.Arquivo.CopyToAsync(ms);
                var bytes = ms.ToArray();

                // extrair texto
                string modelId = "modelov3.1";
                string textoExtraido = await _ocrService.ExtrairTextoAsync(bytes, modelId);

                Documento doc = new Documento
                {
                    Nome = request.Nome,
                    IdUsuario = request.IdUsuario,
                    CriadoEm = DateTime.Now,
                    Arquivo = bytes,
                    MimeType = request.Arquivo.ContentType,
                    TextoOcr = textoExtraido,
                    Status = true
                };

                _documentoRepository.Cadastrar(doc);

                return StatusCode(201, doc);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erro ao processar documento: {ex.Message}");
            }
        }


        [HttpGet("{id}/pdf")]
        public IActionResult ObterPdf(int id)
        {
            var doc = _documentoRepository.BuscarPorId(id);

            if (doc == null || doc.Arquivo == null)
                return NotFound("PDF não encontrado.");

            return File(doc.Arquivo, doc.MimeType ?? "application/pdf");
        }


        [HttpGet("{id}/ocr")]
        public IActionResult ObterTextoOcr(int id)
        {
            var doc = _documentoRepository.BuscarPorId(id);

            if (doc == null)
                return NotFound("Documento não encontrado.");

            return Ok(new
            {
                nome = doc.Nome,
                texto = doc.TextoOcr ?? ""
            });
        }


        [HttpPost("analisar")]
        public async Task<IActionResult> Analisar(IFormFile arquivo, [FromServices] IConfiguration _config)
        {
            if (arquivo == null || arquivo.Length == 0) return BadRequest("Nenhum arquivo foi enviado.");
            try
            {
                string endpoint = _config["AzureDocIntelligence:Endpoint"];
                string apiKey = _config["AzureDocIntelligence:Key"];
                string modelId = _config["AzureDocIntelligence:ModelId"];
                //    // modelo treinado
                //    var client = new DocumentAnalysisClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
                //    using var stream = arquivo.OpenReadStream();
                //    // executa a IA
                //    AnalyzeDocumentOperation operation = await client.AnalyzeDocumentAsync(WaitUntil.Completed, modelId, stream);
                //    AnalyzeResult result = operation.Value; var documentos = new List<object>();
                //    foreach (var document in result.Documents)
                //    {
                //        var campos = new Dictionary<string, object>();
                //        foreach (var campo in document.Fields)
                //        { campos.Add(campo.Key, new { Conteudo = campo.Value.Content, Confianca = campo.Value.Confidence }); }
                //        documentos.Add(new { Tipo = document.DocumentType, Campos = campos });
                //    }
                //    return Ok(new { Modelo = result.ModelId, Documentos = documentos });
                //}
                var client = new DocumentAnalysisClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
                using var stream = arquivo.OpenReadStream();

                AnalyzeDocumentOperation operation = await client.AnalyzeDocumentAsync(WaitUntil.Completed, modelId, stream);
                AnalyzeResult result = operation.Value;

                var doc = result.Documents.First();

                // --- 2. EXTRAIR CAMPOS ---
                string Nome = doc.Fields["Nome"]?.Content?.Replace("Nome:", "").Trim();
                string Empresa = doc.Fields["Empresa"]?.Content?.Replace("Empresa:", "").Trim();
                string Data = doc.Fields["Data"]?.Content?.Replace("Data:", "").Trim();
                string Versao = doc.Fields["Versao"]?.Content?.Replace("Versão:", "").Trim();

                // textos longos:
                string Descricao = doc.Fields.ContainsKey("Descriçao") ? doc.Fields["Descriçao"].Content : "";
                string Funcoes = doc.Fields.ContainsKey("Funçoes") ? doc.Fields["Funçoes"].Content : "";
                string ReqFunc = doc.Fields.ContainsKey("Requisitos Funcionais") ? doc.Fields["Requisitos Funcionais"].Content : "";
                string ReqNaoFunc = doc.Fields.ContainsKey("Requisitos nao funcionais") ? doc.Fields["Requisitos nao funcionais"].Content : "";
                string Regras = doc.Fields.ContainsKey("Regras de Negocio") ? doc.Fields["Regras de Negocio"].Content : "";

                // OCR completo
                string textoOcr = $"{Descricao}\n{Funcoes}\n{ReqFunc}\n{ReqNaoFunc}\n{Regras}";

                // parse da data
                DateOnly prazo = DateOnly.FromDateTime(
     DateTime.TryParse(Data, out DateTime temp) ? temp : DateTime.Now
 );


                // --- 3. Converter arquivo para base64 ---
                byte[] bytesArquivo;
                using (var ms = new MemoryStream())
                {
                    await arquivo.CopyToAsync(ms);
                    bytesArquivo = ms.ToArray();
                }

                // --- 4. Criar documento para cadastrar ---
                var documento = new Documento
                {
                    IdEmpresa = 1, // você deve ajustar
                    IdUsuario = 1, // você deve ajustar
                    Nome = Nome,
                    Prazo = prazo,
                    Status = true,
                    Versao = int.TryParse(Versao, out int v) ? v : 1,
                    Arquivo = bytesArquivo,
                    TextoOcr = textoOcr,
                    MimeType = arquivo.ContentType,
                    VersaoAtual = int.TryParse(Versao, out int va) ? va : 1,
                    CriadoEm = DateTime.Now
                };

                // --- 5. Salvar no banco ---
                _documentoRepository.Cadastrar(documento);

                return Ok(new
                {
                    mensagem = "Documento analisado e cadastrado com sucesso!",
                    documento = documento
                });
            }
            catch (Exception ex)

            {
                   return StatusCode(500,
        $"Erro ao analisar/cadastrar documento: {ex.Message} | INNER: {ex.InnerException?.Message}");
            }
        }

        public class EditarTextoDTO
        {
            public string? Texto { get; set; }
        }

        [HttpPut("{id}/editar-ocr")]
        public IActionResult EditarOcr(int id, [FromBody] EditarTextoDTO dto)
        {
            var doc = _documentoRepository.BuscarPorId(id);
            if (doc == null)
                return NotFound("Documento não encontrado.");

            doc.TextoOcr = dto.Texto;

            try
            {
                _documentoRepository.Editar(id, doc);
                return Ok("Texto atualizado com sucesso!");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erro ao salvar edição: {ex.Message}");
            }
        }





        [HttpPut("{id}/upload")]
        public async Task<IActionResult> AtualizarArquivo(int id, IFormFile arquivo)
        {
            if (arquivo == null || arquivo.Length == 0)
                return BadRequest("Nenhum arquivo enviado.");

            var doc = _documentoRepository.BuscarPorId(id);
            if (doc == null)
                return NotFound("Documento não encontrado.");

            using var ms = new MemoryStream();
            await arquivo.CopyToAsync(ms);

            doc.Arquivo = ms.ToArray();
            doc.MimeType = arquivo.ContentType;

            _documentoRepository.Editar(id, doc);

            return Ok("Arquivo atualizado.");
        }


        [HttpPost("cadastrar-com-ocr")]
        public async Task<IActionResult> CadastrarComOcr(
          IFormFile arquivo,
          [FromForm] int IdUsuario,
          [FromForm] string Nome,
          [FromForm] int IdEmpresa,
          [FromServices] IConfiguration _config)
            {
                try
                {
                    if (arquivo == null || arquivo.Length == 0)
                        return BadRequest("Nenhum arquivo foi enviado.");

                    // ============================
                    // 1) CARREGAR ARQUIVO PDF
                    // ============================
                    using var memoryStream = new MemoryStream();
                    await arquivo.CopyToAsync(memoryStream);

                    var documento = new Documento
                    {
                        IdUsuario = IdUsuario,
                        Nome = Nome,
                        IdEmpresa = IdEmpresa,
                        Arquivo = memoryStream.ToArray(),
                        MimeType = arquivo.ContentType,
                        CriadoEm = DateTime.Now,
                        Status = true,
                        Versao =1
                   
                    };

                    // ============================
                    // 2) CONFIGURAR IA DA AZURE
                    // ============================
                    string endpoint = _config["AzureDocIntelligence:Endpoint"];
                    string apiKey = _config["AzureDocIntelligence:Key"];
                    string modelId = _config["AzureDocIntelligence:ModelId"];

                    var client = new DocumentAnalysisClient(new Uri(endpoint), new AzureKeyCredential(apiKey));

                    memoryStream.Position = 0;
                    AnalyzeDocumentOperation operation =
                        await client.AnalyzeDocumentAsync(WaitUntil.Completed, modelId, memoryStream);

                    AnalyzeResult result = operation.Value;

                    // ============================
                    // 3) SALVAR O TEXTO COMPLETO DO OCR
                    // ============================
                    documento.TextoOcr = result.Content;

                    // ============================
                    // 4) MAPEAR TODOS OS CAMPOS DA OCR
                    // ============================
                    foreach (var doc in result.Documents)
                    {
                        foreach (var campo in doc.Fields)
                        {
                            var nomeCampo = campo.Key;
                            var valorCampo = campo.Value?.Content ?? "";

                            var prop = documento.GetType().GetProperty(nomeCampo);
                            if (prop != null && prop.CanWrite)
                            {
                                object valorConvertido = valorCampo;

                                try
                                {
                                    var tipo = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

                                    if (tipo == typeof(int))
                                        valorConvertido = int.Parse(valorCampo);

                                    else if (tipo == typeof(double))
                                        valorConvertido = double.Parse(valorCampo.Replace(",", "."));

                                    else if (tipo == typeof(float)) // SUPORTE ADICIONADO
                                        valorConvertido = float.Parse(valorCampo.Replace(",", "."));

                                else if (tipo == typeof(decimal))
                                {
                                    if (decimal.TryParse(valorCampo,
                                        NumberStyles.Any,
                                        new CultureInfo("pt-BR"),
                                        out decimal resultado))
                                    {
                                        valorConvertido = resultado;
                                    }
                                    else
                                    {
                                        // Valor inválido vindo do OCR → usar default
                                        valorConvertido = 1;
                                    }
                                }



                                else if (tipo == typeof(bool))
                                        valorConvertido = valorCampo.ToLower() == "true"
                                                        || valorCampo == "1"
                                                        || valorCampo.ToLower() == "sim";

                                    else if (tipo == typeof(DateTime))
                                        valorConvertido = DateTime.Parse(valorCampo);

                                    else if (tipo == typeof(DateOnly))
                                    {
                                        var dt = DateTime.Parse(valorCampo);
                                        valorConvertido = DateOnly.FromDateTime(dt);
                                    }
                                }
                                catch
                                {
                                    // Se não converter, grava como string
                                    valorConvertido = valorCampo;
                                }

                                prop.SetValue(documento, valorConvertido);
                            }
                        }
                    }

                    // ============================
                    // 5) SALVAR NO BANCO
                    // ============================
                    _documentoRepository.Cadastrar(documento);

                    return StatusCode(201, new
                    {
                        mensagem = "Documento cadastrado com sucesso!",
                        documento
                    });
                }
                catch (Exception ex)
                {
                    return StatusCode(500, $"Erro ao cadastrar documento com OCR: {ex.Message}");
                }
        }


    }
}
