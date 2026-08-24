using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using TodoAppRoll.Api.Data;
using TodoAppRoll.Api.Hubs;

using TodoAppRoll.Api.Services;

namespace TodoAppRoll.Api
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddControllers();
            
            // Configuración de Entity Framework Core con PostgreSQL
            builder.Services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

            // Registro de Servicios Personalizados
            builder.Services.AddScoped<ICloudinaryService, CloudinaryService>();

            // CORS: en Development el puerto de Vite es impredecible (varía según qué otros
            // proyectos locales ya estén corriendo), así que se permite cualquier localhost.
            // En producción se reemplaza por una lista blanca de dominios reales.
            const string DevCorsPolicy = "DevCors";
            builder.Services.AddCors(options =>
            {
                options.AddPolicy(DevCorsPolicy, policy =>
                {
                    policy.SetIsOriginAllowed(origin =>
                        Uri.TryCreate(origin, UriKind.Absolute, out var uri) &&
                        (uri.Host == "localhost" || uri.Host == "127.0.0.1"))
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
                });
            });

            // Configuración de SignalR
            builder.Services.AddSignalR();

            // Configuración de Autenticación JWT
            var key = builder.Configuration["Jwt:Key"]
                ?? throw new InvalidOperationException("Falta configurar Jwt:Key (dotnet user-secrets set \"Jwt:Key\" \"...\" en local, o la variable de entorno Jwt__Key en despliegue).");
            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                        ValidateIssuer = true,
                        ValidIssuer = builder.Configuration["Jwt:Issuer"],
                        ValidateAudience = true,
                        ValidAudience = builder.Configuration["Jwt:Audience"],
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.Zero
                    };

                    // Permitir que el token JWT se pase vía query string para SignalR
                    options.Events = new JwtBearerEvents
                    {
                        OnMessageReceived = context =>
                        {
                            var accessToken = context.Request.Query["access_token"];
                            var path = context.HttpContext.Request.Path;
                            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                            {
                                context.Token = accessToken;
                            }
                            return Task.CompletedTask;
                        }
                    };
                });

            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();

            if (app.Environment.IsDevelopment())
            {
                app.UseCors(DevCorsPolicy);
            }

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();
            
            // Mapeo de Hubs de SignalR
            app.MapHub<AlarmHub>("/hubs/alarms");

            app.Run();
        }
    }
}
