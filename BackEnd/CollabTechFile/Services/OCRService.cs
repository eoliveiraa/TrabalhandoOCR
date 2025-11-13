using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;

namespace CollabTechFile.Services
{
    public class OCRService
    {
        private readonly string _endpoint;
        private readonly string _apiKey;

        public OCRService(IConfiguration configuration)
        {
            _endpoint = configuration["AzureFormRecognizer:Endpoint"];
            _apiKey = configuration["AzureFormRecognizer:ApiKey"];
        }

        public async Task<string> ExtrairTextoAsync(byte[] arquivoBytes, string modelId = "prebuilt-document")
        {
            var client = new DocumentAnalysisClient(
                new Uri(_endpoint),
                new AzureKeyCredential(_apiKey)
            );

            using var stream = new MemoryStream(arquivoBytes);

            var operation = await client.AnalyzeDocumentAsync(
                WaitUntil.Completed,
                modelId,
                stream
            );

            var result = operation.Value;

            // Se tiver campos (formulário)
            if (result.Documents.Count > 0)
            {
                return string.Join("\n", result.Documents
                    .SelectMany(d => d.Fields.Select(f => $"{f.Key}: {f.Value?.Content}")));
            }

            // Caso contrário, retorna o texto puro (OCR)
            var texto = string.Join("\n",
                result.Pages.SelectMany(p => p.Lines.Select(l => l.Content))
            );

            return texto;
        }
    }
}
