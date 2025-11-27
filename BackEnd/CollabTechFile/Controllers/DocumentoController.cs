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
using Microsoft.EntityFrameworkCore;

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
            try
            {
                var documentos = _documentoRepository
                    .Listar()              // já vem com Usuario, ReqDocs e RegrasDocs
                    .Where(d => d.Status); // filtra documentos ativos

                return Ok(documentos);
            }
            catch (Exception ex)
            {
                return BadRequest($"Erro ao listar documentos: {ex.Message}");
            }
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
            if (arquivo == null || arquivo.Length == 0)
                return BadRequest("Nenhum arquivo foi enviado.");

            try
            {
                // ---------- AZURE CONFIG ----------
                string endpoint = _config["AzureDocIntelligence:Endpoint"];
                string apiKey = _config["AzureDocIntelligence:Key"];
                string modelId = _config["AzureDocIntelligence:ModelId"];

                var client = new DocumentAnalysisClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
                using var stream = arquivo.OpenReadStream();

                AnalyzeDocumentOperation operation =
                    await client.AnalyzeDocumentAsync(WaitUntil.Completed, modelId, stream);

                AnalyzeResult result = operation.Value;
                var doc = result.Documents.First();

                // -----------------------
                // 1) EXTRAIR CAMPOS
                // -----------------------
                string Nome = doc.Fields.ContainsKey("Nome") ? doc.Fields["Nome"].Content?.Replace("Nome:", "").Trim() : null;
                string Empresa = doc.Fields.ContainsKey("Empresa") ? doc.Fields["Empresa"].Content?.Trim() : null;
                string Data = doc.Fields.ContainsKey("Data") ? doc.Fields["Data"].Content?.Trim() : null;
                string Versao = doc.Fields.ContainsKey("Versao") ? doc.Fields["Versao"].Content?.Trim() : "1";

                string Descricao = doc.Fields.ContainsKey("Descriçao") ? doc.Fields["Descriçao"].Content : "";
                string Funcoes = doc.Fields.ContainsKey("Funçoes") ? doc.Fields["Funçoes"].Content : "";
                string ReqFunc = doc.Fields.ContainsKey("Requisitos Funcionais") ? doc.Fields["Requisitos Funcionais"].Content : "";
                string ReqNaoFunc = doc.Fields.ContainsKey("Requisitos nao funcionais") ? doc.Fields["Requisitos nao funcionais"].Content : "";
                string Regras = doc.Fields.ContainsKey("Regras de Negocio") ? doc.Fields["Regras de Negocio"].Content : "";

                string textoOcr = $"{Descricao}\n{Funcoes}\n{ReqFunc}\n{ReqNaoFunc}\n{Regras}";

                // -----------------------
                // 2) DATA → DateOnly
                // -----------------------
                DateOnly prazo = DateOnly.FromDateTime(
                    DateTime.TryParse(Data, out DateTime temp) ? temp : DateTime.Now
                );

                // -----------------------
                // 3) ARQUIVO → byte[]
                // -----------------------
                byte[] bytesArquivo;
                using (var ms = new MemoryStream())
                {
                    await arquivo.CopyToAsync(ms);
                    bytesArquivo = ms.ToArray();
                }

                // -----------------------
                // 4) MONTAR DOCUMENTO
                // -----------------------
                var documento = new Documento
                {
                    IdEmpresa = 1,    // Ajuste conforme sua regra (p.ex. procurar Empresa por nome)
                    IdUsuario = 1,    // Ajuste (usuário logado)
                    Nome = Nome,
                    Prazo = prazo,
                    Status = true,
                    Versao = decimal.TryParse(Versao, NumberStyles.Any, new CultureInfo("pt-BR"), out var dv) ? dv : 1,
                    Arquivo = bytesArquivo,
                    TextoOcr = textoOcr,
                    MimeType = arquivo.ContentType,
                    VersaoAtual = decimal.TryParse(Versao, NumberStyles.Any, new CultureInfo("pt-BR"), out var dva) ? dva : 1,
                    CriadoEm = DateTime.Now,
                    // inicializa coleções para adicionar os vínculos
                    ReqDocs = new List<ReqDoc>(),
                    RegrasDocs = new List<RegrasDoc>()
                };

                // -----------------------
                // 5) MONTAR REQUISITOS (REQ_DOC)
                // -----------------------
                // Função utilitária simples para quebrar linhas e bullets
                string[] SplitLines(string text) =>
                    string.IsNullOrWhiteSpace(text)
                        ? Array.Empty<string>()
                        : text
                            .Replace("·", "\n")
                            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                            .Select(l => l.Trim())
                            .Where(l => !string.IsNullOrWhiteSpace(l))
                            .ToArray();

                var linhasReq = SplitLines(ReqFunc).Concat(SplitLines(ReqNaoFunc)).ToArray();

                foreach (var linha in linhasReq)
                {
                    // tenta extrair "RF01: descrição" ou "RF01 - descrição" ou apenas "Descrição"
                    string codigo = null;
                    string texto = linha;

                    if (linha.Contains(":"))
                    {
                        var idx = linha.IndexOf(':');
                        codigo = linha.Substring(0, idx).Trim();
                        texto = linha.Substring(idx + 1).Trim();
                    }
                    else if (linha.Contains("-"))
                    {
                        var idx = linha.IndexOf('-');
                        var possibleCode = linha.Substring(0, idx).Trim();
                        // se possível código parece com RF ou RNF, aceita
                        if (possibleCode.StartsWith("RF", StringComparison.OrdinalIgnoreCase) ||
                            possibleCode.StartsWith("RNF", StringComparison.OrdinalIgnoreCase) ||
                            possibleCode.All(ch => char.IsLetterOrDigit(ch)))
                        {
                            codigo = possibleCode;
                            texto = linha.Substring(idx + 1).Trim();
                        }
                    }
                    // se não há código, deixa tipo genérico
                    if (string.IsNullOrWhiteSpace(codigo))
                        codigo = "RF";

                    // Cria entidade Requisito (PK = 0 para inserir)
                    var requisito = new Requisito
                    {
                        Tipo = codigo,
                        TextoReq = texto
                    };

                    // Cria entidade de ligação ReqDoc; não precisa setar IdDocumento/IdRequisito
                    // basta setar as navegations para que o EF insira tudo
                    var reqDoc = new ReqDoc
                    {
                        IdRequisitoNavigation = requisito,
                        IdDocumentoNavigation = documento
                    };

                    documento.ReqDocs.Add(reqDoc);
                }

                // -----------------------
                // 6) MONTAR REGRAS DE NEGÓCIO (REGRAS_DOC)
                // -----------------------
                var linhasRegras = SplitLines(Regras);

                foreach (var linha in linhasRegras)
                {
                    string codigo = null;
                    string texto = linha;

                    if (linha.Contains(":"))
                    {
                        var idx = linha.IndexOf(':');
                        codigo = linha.Substring(0, idx).Trim();
                        texto = linha.Substring(idx + 1).Trim();
                    }
                    else if (linha.Contains("-"))
                    {
                        var idx = linha.IndexOf('-');
                        codigo = linha.Substring(0, idx).Trim();
                        texto = linha.Substring(idx + 1).Trim();
                    }

                    // Nome da regra pode ser "RB01 - descrição" ou apenas descrição
                    var regra = new Regra
                    {
                        Nome = string.IsNullOrWhiteSpace(codigo) ? texto : $"{codigo} - {texto}"
                    };

                    var regraDoc = new RegrasDoc
                    {
                        IdRegrasNavigation = regra,
                        IdDocumentoNavigation = documento
                    };

                    documento.RegrasDocs.Add(regraDoc);
                }

                // -----------------------
                // 7) SALVAR TUDO VIA REPOSITORY
                // -----------------------
                // seu repository faz: _context.Documentos.Add(documento); _context.SaveChanges();
                // com as navegations preenchidas o EF deve inserir Documento, Requisito, Regra e as tabelas intermediárias.
                _documentoRepository.Cadastrar(documento);

                return Ok(new
                {
                    mensagem = "Documento, requisitos e regras cadastrados com sucesso!",
                    documento
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
