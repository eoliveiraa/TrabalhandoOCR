using CollabTechFile.Services;
using Microsoft.AspNetCore.Mvc;

namespace CollabTechFile.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OCRController : ControllerBase
    {
        private readonly OCRService _ocrService;

        public OCRController(OCRService ocrService)
        {
            _ocrService = ocrService;
        }

        [HttpPost("analisar")]
        public async Task<IActionResult> Analisar(IFormFile arquivo)
        {
            if (arquivo == null || arquivo.Length == 0)
                return BadRequest("Nenhum arquivo foi enviado.");

            using var ms = new MemoryStream();
            await arquivo.CopyToAsync(ms);
            var textoExtraido = await _ocrService.ExtrairTextoAsync(ms.ToArray());

            return Ok(new { Texto = textoExtraido });
        }
    }
}
