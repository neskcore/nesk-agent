using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using DotNetEnv;
using NeskAgent.Data;
using NeskAgent.Services;
using NeskAgent.Middlewares;

// Tenta carregar o .env da pasta onde o executável está, se não encontrar, tenta na pasta atual ou pai
var envPath = Path.Combine(AppContext.BaseDirectory, ".env");
if (!File.Exists(envPath)) envPath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
if (!File.Exists(envPath)) envPath = Path.Combine(Directory.GetCurrentDirectory(), "..", ".env");

if (File.Exists(envPath))
{
    Env.Load(envPath);
}
else
{
    Console.WriteLine("⚠️ Aviso: Arquivo .env não encontrado.");
}

var builder = WebApplication.CreateBuilder(args);

// Silencia os logs padrão do .NET (Microsoft.Hosting.Lifetime)
builder.Logging.AddFilter("Microsoft.Hosting.Lifetime", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);

// Configuration
var portStr = Environment.GetEnvironmentVariable("PORT");
var cdnPortStr = Environment.GetEnvironmentVariable("CDN_PORT");
var dbHost = Environment.GetEnvironmentVariable("AGENT_DB_HOST");
var dbUser = Environment.GetEnvironmentVariable("AGENT_DB_USER");
var dbPass = Environment.GetEnvironmentVariable("AGENT_DB_PASS");
var dbName = Environment.GetEnvironmentVariable("AGENT_DB_NAME");
var apiKey = Environment.GetEnvironmentVariable("AGENT_API_KEY");

// Fallbacks para valores padrão se as variáveis estiverem vazias
var port = int.Parse(string.IsNullOrEmpty(portStr) ? "4000" : portStr);
var cdnPort = int.Parse(string.IsNullOrEmpty(cdnPortStr) ? "4001" : cdnPortStr);
dbHost ??= "localhost";
dbUser ??= "root";
dbPass ??= "";
dbName ??= "nesk_agent";

var connectionString = $"Server={dbHost};User ID={dbUser};Password={dbPass};AllowUserVariables=True";

// Add services
builder.Services.AddControllers()
    .AddNewtonsoftJson()
    .AddApplicationPart(typeof(NeskAgent.Controllers.CdnController).Assembly);
builder.Services.AddCors();

// Configura limites de upload e portas no Kestrel
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 200 * 1024 * 1024; // 200MB
    options.ListenAnyIP(port);
    options.ListenAnyIP(cdnPort);
});

builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.ValueLengthLimit = int.MaxValue;
    options.MultipartBodyLengthLimit = 200 * 1024 * 1024; // 200MB
    options.MemoryBufferThreshold = int.MaxValue;
});

// Configura o Dapper para mapear snake_case para PascalCase
Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;

builder.Services.AddSingleton(new ProxyService($"{connectionString};Database={dbName}"));
builder.Services.AddSingleton<CdnService>();

var app = builder.Build();

// Initialize Database
var dbInitializer = new DatabaseInitializer(connectionString, dbName);
await dbInitializer.InitializeAsync();

// Middleware
app.UseCors(x => x.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());

// Port-based routing/filtering
app.Use(async (context, next) =>
{
    var localPort = context.Connection.LocalPort;
    
    if (localPort == port)
    {
        // Agent API (Port 4000)
        if (context.Request.Path == "/")
        {
            await context.Response.WriteAsJsonAsync(new { 
                success = true, 
                message = "Nesk Agent Operando normalmente",
                version = "1.0.0",
                status = "online",
                timestamp = DateTime.UtcNow 
            });
            return;
        }
        if (context.Request.Path == "/health")
        {
            await context.Response.WriteAsJsonAsync(new { 
                success = true, 
                message = "Nesk Agent está operante", 
                timestamp = DateTime.UtcNow 
            });
            return;
        }
    }
    else if (localPort == cdnPort)
    {
        // CDN API (Port 4001)
        if (context.Request.Path == "/")
        {
            await context.Response.WriteAsJsonAsync(new { 
                success = true, 
                message = "Nesk Agent Operando normalmente",
                serving = "/attachments",
                status = "online",
                timestamp = DateTime.UtcNow
            });
            return;
        }
        if (context.Request.Path == "/health")
        {
            await context.Response.WriteAsJsonAsync(new { 
                success = true, 
                message = "Nesk Agent está operante",
                serving = "/attachments"
            });
            return;
        }
    }
    
    await next();
});

// Auth Middleware (for API routes)
app.UseMiddleware<AuthMiddleware>();

// Static Files for CDN
var attachmentsPath = Path.Combine(AppContext.BaseDirectory, "cdn", "attachments");
if (!Directory.Exists(attachmentsPath)) Directory.CreateDirectory(attachmentsPath);

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(attachmentsPath),
    RequestPath = "/attachments",
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers.Append("Cache-Control", "public,max-age=86400");
    }
});

app.MapControllers();

Console.WriteLine("-------------------------------------------");
Console.WriteLine($"🚀 Nesk Agent iniciado na porta {port}");
Console.WriteLine($"🚀 Nesk CDN iniciado na porta {cdnPort}");
Console.WriteLine($"🚀 Data: {DateTime.Now}");
Console.WriteLine("-------------------------------------------");

app.Run();
