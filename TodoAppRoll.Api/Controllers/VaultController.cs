using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using TodoAppRoll.Api.Models.DTOs;

namespace TodoAppRoll.Api.Controllers
{
    // Proxy hacia el cerebro de proyectos (BrainObsidian) que maneja AsistenteArtemis.Brain.
    // Los clientes (app, mascota) hablan solo con la Api; el Brain valida que el usuario sea el
    // dueño del vault (VAULT_OWNER_USER_ID) y hace la escritura acotada del contrato con Artemis.
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class VaultController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        public VaultController(IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        private Guid GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            return userIdClaim != null ? Guid.Parse(userIdClaim.Value) : Guid.Empty;
        }

        private HttpClient BrainClient()
        {
            var client = _httpClientFactory.CreateClient("Brain");
            client.DefaultRequestHeaders.Add("X-Internal-Key", _configuration["Brain:InternalKey"]);
            return client;
        }

        // GET: api/Vault/projects — frontmatter de las fichas de proyecto.
        [HttpGet("projects")]
        public Task<IActionResult> GetProjects() =>
            ForwardAsync(client => client.GetAsync($"/internal/vault/projects?user_id={GetUserId()}"));

        // POST: api/Vault/pendiente — agrega un pendiente "(artemis)" al proyecto.
        [HttpPost("pendiente")]
        public Task<IActionResult> AddPendiente(VaultNoteDto dto) =>
            ForwardAsync(client => client.PostAsJsonAsync("/internal/vault/pendiente",
                new { user_id = GetUserId(), project = dto.Project, text = dto.Text }));

        // POST: api/Vault/comentario — agrega una entrada "(artemis)" a la bitácora del proyecto.
        [HttpPost("comentario")]
        public Task<IActionResult> AddComentario(VaultNoteDto dto) =>
            ForwardAsync(client => client.PostAsJsonAsync("/internal/vault/comentario",
                new { user_id = GetUserId(), project = dto.Project, text = dto.Text }));

        // Reenvía la respuesta del Brain tal cual. Los errores de validación (400) y de dueño
        // (403) llegan con su "detail" convertido a "message", como el resto de la Api.
        private async Task<IActionResult> ForwardAsync(Func<HttpClient, Task<HttpResponseMessage>> call)
        {
            HttpResponseMessage response;
            try
            {
                response = await call(BrainClient());
            }
            catch (HttpRequestException e)
            {
                return StatusCode(502, new { message = $"No se pudo contactar al Brain: {e.Message}" });
            }

            var body = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode)
            {
                return Content(body, "application/json");
            }

            var status = (int)response.StatusCode;
            var message = $"El Brain respondió con error {status}";
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String)
                    message = detail.GetString()!;
            }
            catch (JsonException)
            {
                // Cuerpo no JSON: queda el mensaje genérico.
            }
            return StatusCode(status is 400 or 403 or 409 ? status : 502, new { message });
        }
    }
}
