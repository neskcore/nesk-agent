using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System;

namespace NeskAgent.Middlewares
{
    public class AuthMiddleware
    {
        private readonly RequestDelegate _next;

        public AuthMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Lê a API Key direto do ambiente a cada requisição ou armazena
            string apiKey = Environment.GetEnvironmentVariable("AGENT_API_KEY");
            if (context.Request.Path == "/" || context.Request.Path == "/health")
            {
                await _next(context);
                return;
            }

            // CDN paths are public (as in the original Node.js app)
            if (context.Request.Path.StartsWithSegments("/attachments"))
            {
                await _next(context);
                return;
            }

            string authHeader = context.Request.Headers["Authorization"];

            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            {
                context.Response.StatusCode = 401;
                await context.Response.WriteAsJsonAsync(new { success = false, error = "Não autorizado: Token ausente ou formato inválido" });
                return;
            }

            string token = authHeader.Substring("Bearer ".Length).Trim();

            if (token != apiKey)
            {
                context.Response.StatusCode = 403;
                await context.Response.WriteAsJsonAsync(new { success = false, error = "Proibido: API Key inválida" });
                return;
            }

            await _next(context);
        }
    }
}
