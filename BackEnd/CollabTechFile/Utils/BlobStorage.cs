using Azure.Storage.Blobs;

namespace WebAPI.Utils.BlobStorage
{
    // Define uma classe estática chamada AzureBlobStorageHelper
    public static class AzureBlobStorage
    {

        // Método para upload de imagem que recebe a imagem, a string de conexão do blob e o nome do container
        public static async Task<string> UploadArquivoBlobAsync(IFormFile arquivo, string connectionString, string containerName)
        {
            try
            {
                // Verifica se o arquivo foi realmente enviado
                if (arquivo == null || arquivo.Length == 0)
                    throw new ArgumentException("Nenhum arquivo foi enviado.");

                // Gera um nome único para o blob com extensão original
                var blobName = $"{Guid.NewGuid()}{Path.GetExtension(arquivo.FileName)}";

                // Cria o cliente principal do serviço de Blob Storage
                var blobServiceClient = new BlobServiceClient(connectionString);

                // Garante que o container existe (opcional, mas seguro)
                var blobContainerClient = blobServiceClient.GetBlobContainerClient(containerName);
                await blobContainerClient.CreateIfNotExistsAsync();

                // Obtém o cliente para o blob específico
                var blobClient = blobContainerClient.GetBlobClient(blobName);

                // Faz o upload do arquivo
                using (var stream = arquivo.OpenReadStream())
                {
                    await blobClient.UploadAsync(stream, overwrite: true);
                }

                // Retorna o link completo do arquivo no Azure Blob
                return blobClient.Uri.ToString();
            }
            catch (Exception ex)
            {
                // Em vez de retornar mensagem de erro como string (difícil de tratar),
                // é melhor lançar uma exceção — o controller pode capturar e lidar
                throw new ApplicationException($"Erro ao enviar arquivo para o Blob Storage: {ex.Message}", ex);
            }
        }

        // Método para excluir a imagem do Blob Storage
        public static async Task DeleteArquivoFromBlobStorage(string imageUrl, string connectionString, string containerName)
        {
            var blobServiceClient = new BlobServiceClient(connectionString);

            var containerClient = blobServiceClient.GetBlobContainerClient(containerName);

            // Obtém o nome do blob a partir do URL
            var blobName = new Uri(imageUrl).Segments.Last();

            var blobClient = containerClient.GetBlobClient(blobName);

            // Exclui o blob
            await blobClient.DeleteIfExistsAsync();
        }


    }
}