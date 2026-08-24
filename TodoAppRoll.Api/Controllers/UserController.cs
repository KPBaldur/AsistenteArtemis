using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TodoAppRoll.Api.Data;
using TodoAppRoll.Api.Models.DTOs;
using TodoAppRoll.Api.Models.Entities;

namespace TodoAppRoll.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserController : ControllerBase
    {
        private readonly AppDbContext _context;

        public UserController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/User/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<UserDto>> GetProfile(Guid id)
        {
            var user = await _context.Users.FindAsync(id);

            if (user == null)
            {
                return NotFound(new { message = "Usuario no encontrado" });
            }

            return new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                Theme = user.Theme,
                NotificationsEnabled = user.NotificationsEnabled,
                CreatedAt = user.CreatedAt
            };
        }

        // PUT: api/User/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateProfile(Guid id, UserUpdateDto updateDto)
        {
            var user = await _context.Users.FindAsync(id);

            if (user == null)
            {
                return NotFound(new { message = "Usuario no encontrado" });
            }

            if (updateDto.Email != null) user.Email = updateDto.Email;
            if (updateDto.Theme != null) user.Theme = updateDto.Theme;
            if (updateDto.NotificationsEnabled.HasValue) 
                user.NotificationsEnabled = updateDto.NotificationsEnabled.Value;

            user.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                return StatusCode(500, new { message = "Error al actualizar el perfil" });
            }

            return NoContent();
        }
    }
}
