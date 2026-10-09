using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Security.Cryptography;
using TodoAppRoll.Api.Data;
using TodoAppRoll.Api.Models.DTOs;
using TodoAppRoll.Api.Models.Entities;
using TodoAppRoll.Api.Services;

namespace TodoAppRoll.Api.Controllers
{
    // Solo con la sesión de la app (JWT): un token de dispositivo no puede crear, listar ni
    // revocar otros tokens.
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [ApiController]
    [Route("api/[controller]")]
    public class DeviceTokenController : ControllerBase
    {
        private readonly AppDbContext _context;

        public DeviceTokenController(AppDbContext context)
        {
            _context = context;
        }

        private Guid GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            return userIdClaim != null ? Guid.Parse(userIdClaim.Value) : Guid.Empty;
        }

        private static DeviceTokenDto ToDto(DeviceToken d) => new DeviceTokenDto
        {
            Id = d.Id,
            Name = d.Name,
            CreatedAt = d.CreatedAt,
            LastUsedAt = d.LastUsedAt,
            RevokedAt = d.RevokedAt
        };

        // GET: api/DeviceToken
        [HttpGet]
        public async Task<ActionResult<IEnumerable<DeviceTokenDto>>> GetDeviceTokens()
        {
            var userId = GetUserId();
            var devices = await _context.DeviceTokens
                .Where(d => d.UserId == userId)
                .OrderByDescending(d => d.CreatedAt)
                .ToListAsync();

            return Ok(devices.Select(ToDto));
        }

        // POST: api/DeviceToken — devuelve el token en claro una sola vez; solo se guarda su hash.
        [HttpPost]
        public async Task<ActionResult<DeviceTokenCreatedDto>> CreateDeviceToken(DeviceTokenCreateDto createDto)
        {
            var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                .Replace('+', '-').Replace('/', '_').TrimEnd('=');

            var device = new DeviceToken
            {
                Id = Guid.NewGuid(),
                UserId = GetUserId(),
                Name = createDto.Name.Trim(),
                TokenHash = DeviceTokenAuthenticationHandler.Hash(token)
            };

            _context.DeviceTokens.Add(device);
            await _context.SaveChangesAsync();

            return Ok(new DeviceTokenCreatedDto { Device = ToDto(device), Token = token });
        }

        // DELETE: api/DeviceToken/{id} — revoca (no borra), para conservar el historial.
        [HttpDelete("{id}")]
        public async Task<IActionResult> RevokeDeviceToken(Guid id)
        {
            var userId = GetUserId();
            var device = await _context.DeviceTokens.FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId);
            if (device == null)
            {
                return NotFound(new { message = "Dispositivo no encontrado" });
            }

            device.RevokedAt ??= DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
