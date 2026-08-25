using CloudinaryDotNet;
using CloudinaryDotNet.Actions;

namespace TodoAppRoll.Api.Services
{
    public interface ICloudinaryService
    {
        Task<(string url, string publicId)> UploadFileAsync(IFormFile file, string folder, string mediaType);
        Task<bool> DeleteFileAsync(string publicId, string mediaType);
    }

    public class CloudinaryService : ICloudinaryService
    {
        private readonly Cloudinary _cloudinary;

        public CloudinaryService(IConfiguration configuration)
        {
            var cloudName = configuration["Cloudinary:CloudName"]
                ?? throw new InvalidOperationException("Falta configurar Cloudinary:CloudName.");
            var apiKey = configuration["Cloudinary:ApiKey"]
                ?? throw new InvalidOperationException("Falta configurar Cloudinary:ApiKey.");
            var apiSecret = configuration["Cloudinary:ApiSecret"]
                ?? throw new InvalidOperationException("Falta configurar Cloudinary:ApiSecret.");

            _cloudinary = new Cloudinary(new Account(cloudName, apiKey, apiSecret));
            _cloudinary.Api.Secure = true;
        }

        // Cloudinary no tiene un resource_type "audio" propio: el audio se sube y se borra
        // como "video" (su convención), las imágenes como "image". El resource_type queda
        // fijado por la clase de params usada (ImageUploadParams/VideoUploadParams), no es
        // una propiedad asignable.
        private static ResourceType ToResourceType(string mediaType) =>
            mediaType == "audio" ? ResourceType.Video : ResourceType.Image;

        public async Task<(string url, string publicId)> UploadFileAsync(IFormFile file, string folder, string mediaType)
        {
            await using var stream = file.OpenReadStream();
            var fileDescription = new FileDescription(file.FileName, stream);

            RawUploadParams uploadParams = mediaType == "audio"
                ? new VideoUploadParams { File = fileDescription, Folder = folder }
                : new ImageUploadParams { File = fileDescription, Folder = folder };

            var result = await _cloudinary.UploadAsync(uploadParams);

            if (result.Error != null)
                throw new InvalidOperationException($"Error subiendo a Cloudinary: {result.Error.Message}");

            return (result.SecureUrl.ToString(), result.PublicId);
        }

        public async Task<bool> DeleteFileAsync(string publicId, string mediaType)
        {
            var deleteParams = new DeletionParams(publicId)
            {
                ResourceType = ToResourceType(mediaType)
            };

            var result = await _cloudinary.DestroyAsync(deleteParams);
            return result.Result == "ok" || result.Result == "not found";
        }
    }
}
