using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using TodoAppRoll.Api.Data;
using TodoAppRoll.Api.Models.DTOs;
using TodoAppRoll.Api.Models.Entities;

namespace TodoAppRoll.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class AiChatController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        public AiChatController(AppDbContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        private Guid GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            return userIdClaim != null ? Guid.Parse(userIdClaim.Value) : Guid.Empty;
        }

        private static ChatSessionDto ToDto(AIChatSession s) => new ChatSessionDto
        {
            Id = s.Id,
            Title = s.Title,
            CreatedAt = s.CreatedAt
        };

        private static AIMessageDto ToDto(AIMessage m) => new AIMessageDto
        {
            Id = m.Id,
            Role = m.Role,
            Content = m.Content,
            CreatedAt = m.CreatedAt
        };

        // GET: api/AiChat/sessions
        [HttpGet("sessions")]
        public async Task<ActionResult<IEnumerable<ChatSessionDto>>> GetSessions()
        {
            var userId = GetUserId();
            var sessions = await _context.AIChatSessions
                .Where(s => s.UserId == userId)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();

            return Ok(sessions.Select(ToDto));
        }

        // POST: api/AiChat/sessions
        [HttpPost("sessions")]
        public async Task<ActionResult<ChatSessionDto>> CreateSession(ChatSessionCreateDto createDto)
        {
            var userId = GetUserId();

            var session = new AIChatSession
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Title = string.IsNullOrWhiteSpace(createDto.Title) ? "Nueva Conversación" : createDto.Title.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            _context.AIChatSessions.Add(session);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetMessages), new { id = session.Id }, ToDto(session));
        }

        // GET: api/AiChat/sessions/{id}/messages
        [HttpGet("sessions/{id}/messages")]
        public async Task<ActionResult<IEnumerable<AIMessageDto>>> GetMessages(Guid id)
        {
            var userId = GetUserId();
            var session = await _context.AIChatSessions.FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);
            if (session == null) return NotFound();

            var messages = await _context.AIMessages
                .Where(m => m.SessionId == id)
                .OrderBy(m => m.CreatedAt)
                .ToListAsync();

            return Ok(messages.Select(ToDto));
        }

        // POST: api/AiChat/sessions/{id}/messages
        [HttpPost("sessions/{id}/messages")]
        public async Task<ActionResult<SendMessageResponseDto>> SendMessage(Guid id, SendMessageDto sendDto)
        {
            var userId = GetUserId();
            var session = await _context.AIChatSessions.FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);
            if (session == null) return NotFound();

            if (string.IsNullOrWhiteSpace(sendDto.Content))
                return BadRequest(new { message = "El mensaje no puede estar vacío" });

            var userMessage = new AIMessage
            {
                Id = Guid.NewGuid(),
                SessionId = id,
                Role = "user",
                Content = sendDto.Content.Trim(),
                CreatedAt = DateTime.UtcNow
            };
            _context.AIMessages.Add(userMessage);
            await _context.SaveChangesAsync();

            string replyText;
            try
            {
                var client = _httpClientFactory.CreateClient("Brain");
                client.DefaultRequestHeaders.Add("X-Internal-Key", _configuration["Brain:InternalKey"]);

                var response = await client.PostAsJsonAsync("/internal/chat", new
                {
                    user_id = userId,
                    session_id = id,
                    message = userMessage.Content
                });

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    return StatusCode(502, new { message = $"El Brain respondió con error: {errorBody}" });
                }

                var brainResponse = await response.Content.ReadFromJsonAsync<BrainChatResponse>(
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                replyText = brainResponse?.Reply ?? string.Empty;
            }
            catch (Exception e)
            {
                return StatusCode(502, new { message = $"No se pudo contactar al Brain: {e.Message}" });
            }

            var assistantMessage = new AIMessage
            {
                Id = Guid.NewGuid(),
                SessionId = id,
                Role = "assistant",
                Content = replyText,
                CreatedAt = DateTime.UtcNow
            };
            _context.AIMessages.Add(assistantMessage);
            await _context.SaveChangesAsync();

            return Ok(new SendMessageResponseDto
            {
                UserMessage = ToDto(userMessage),
                AssistantMessage = ToDto(assistantMessage)
            });
        }

        private class BrainChatResponse
        {
            public string Reply { get; set; } = string.Empty;
        }
    }
}
