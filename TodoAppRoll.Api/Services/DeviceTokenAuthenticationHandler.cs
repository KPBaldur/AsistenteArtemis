using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TodoAppRoll.Api.Data;

namespace TodoAppRoll.Api.Services
{
    // Autenticación con "Authorization: Device <token>" para la mascota de escritorio. Emite el
    // mismo claim NameIdentifier que el JWT, así los controladores [Authorize] y GetUserId()
    // funcionan igual sin saber qué tipo de credencial llegó (ver el policy scheme en Program.cs).
    public class DeviceTokenAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string SchemeName = "Device";
        public const string HeaderPrefix = "Device ";

        // LastUsedAt se actualiza como máximo una vez por este intervalo: la mascota consulta la
        // Api seguido y no hace falta una escritura en cada request.
        private static readonly TimeSpan LastUsedResolution = TimeSpan.FromMinutes(10);

        private readonly AppDbContext _context;

        public DeviceTokenAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder,
            AppDbContext context)
            : base(options, logger, encoder)
        {
            _context = context;
        }

        public static string Hash(string token) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            string? header = Request.Headers.Authorization;
            if (header is null || !header.StartsWith(HeaderPrefix, StringComparison.Ordinal))
                return AuthenticateResult.NoResult();

            var token = header[HeaderPrefix.Length..].Trim();
            if (token.Length == 0)
                return AuthenticateResult.Fail("Token de dispositivo vacío");

            var hash = Hash(token);
            var device = await _context.DeviceTokens.FirstOrDefaultAsync(d => d.TokenHash == hash);
            if (device is null || device.RevokedAt is not null)
                return AuthenticateResult.Fail("Token de dispositivo inválido o revocado");

            var now = DateTime.UtcNow;
            if (device.LastUsedAt is null || now - device.LastUsedAt > LastUsedResolution)
            {
                device.LastUsedAt = now;
                await _context.SaveChangesAsync();
            }

            var identity = new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, device.UserId.ToString()), new Claim("device_id", device.Id.ToString())],
                SchemeName);
            return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
        }
    }
}
