namespace TodoAppRoll.Api.Services
{
    public interface ICloudinaryService
    {
        Task<(string url, string publicId)> UploadFileAsync(IFormFile file, string folder);
        Task<bool> DeleteFileAsync(string publicId);
    }

    public class CloudinaryService : ICloudinaryService
    {
        private readonly IConfiguration _configuration;

        public CloudinaryService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<(string url, string publicId)> UploadFileAsync(IFormFile file, string folder)
        {
            // Aquí iría la implementación real con el SDK de Cloudinary:
            // var uploadParams = new ImageUploadParams() { ... };
            // var uploadResult = await _cloudinary.UploadAsync(uploadParams);
            
            // Simulamos una respuesta exitosa por ahora:
            return ("https://res.cloudinary.com/demo/image/upload/sample.jpg", "sample_public_id");
        }

        public async Task<bool> DeleteFileAsync(string publicId)
        {
            // Lógica de eliminación en Cloudinary
            return true;
        }
    }
}
