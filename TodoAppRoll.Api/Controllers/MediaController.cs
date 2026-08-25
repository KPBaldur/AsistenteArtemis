using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TodoAppRoll.Api.Data;
using TodoAppRoll.Api.Models.DTOs;
using TodoAppRoll.Api.Models.Entities;
using TodoAppRoll.Api.Services;

namespace TodoAppRoll.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class MediaController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ICloudinaryService _cloudinaryService;

        public MediaController(AppDbContext context, ICloudinaryService cloudinaryService)
        {
            _context = context;
            _cloudinaryService = cloudinaryService;
        }

        private Guid GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            return userIdClaim != null ? Guid.Parse(userIdClaim.Value) : Guid.Empty;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<MediaDto>>> GetMyMedia()
        {
            var userId = GetUserId();
            var media = await _context.MediaFiles
                .Where(m => m.UserId == userId)
                .OrderByDescending(m => m.CreatedAt)
                .Select(m => new MediaDto
                {
                    Id = m.Id,
                    Name = m.Name,
                    Url = m.Url,
                    FileType = m.FileType,
                    CreatedAt = m.CreatedAt
                })
                .ToListAsync();

            return Ok(media);
        }

        [HttpPost("upload")]
        public async Task<ActionResult<MediaDto>> Upload(IFormFile file, [FromForm] string type)
        {
            var userId = GetUserId();

            if (file == null || file.Length == 0)
                return BadRequest("No se proporcionó ningún archivo");

            if (type != "audio" && type != "image")
                return BadRequest(new { message = "Tipo inválido. Usa 'image' o 'audio'." });

            // Subir a Cloudinary (vía servicio)
            var folder = type == "audio" ? "TodoAppRoll/Audios" : "TodoAppRoll/Images";
            var (url, publicId) = await _cloudinaryService.UploadFileAsync(file, folder, type);

            var media = new Media
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Name = file.FileName,
                Url = url,
                PublicId = publicId,
                FileType = type,
                CreatedAt = DateTime.UtcNow
            };

            _context.MediaFiles.Add(media);
            await _context.SaveChangesAsync();

            return Ok(new MediaDto
            {
                Id = media.Id,
                Name = media.Name,
                Url = media.Url,
                FileType = media.FileType,
                CreatedAt = media.CreatedAt
            });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteMedia(Guid id)
        {
            var userId = GetUserId();
            var media = await _context.MediaFiles.FirstOrDefaultAsync(m => m.Id == id && m.UserId == userId);

            if (media == null) return NotFound();

            // Eliminar de Cloudinary
            await _cloudinaryService.DeleteFileAsync(media.PublicId, media.FileType);

            _context.MediaFiles.Remove(media);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
