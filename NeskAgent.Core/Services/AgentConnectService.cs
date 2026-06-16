using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using NeskAgent.Command.Models;

namespace NeskAgent.Core.Services
{
    public class AgentConnectService : IDisposable
    {
        private readonly string _httpBaseUrl;
        private readonly HttpClient _httpClient = new();

        public AgentConnectService(string wsBaseUrl)
        {
            // Converte wss:// ou ws:// para https:// ou http://
            _httpBaseUrl = wsBaseUrl.Replace("wss://", "https://", StringComparison.OrdinalIgnoreCase)
                                     .Replace("ws://", "http://", StringComparison.OrdinalIgnoreCase);

            _httpClient.BaseAddress = new Uri(_httpBaseUrl);
            _httpClient.Timeout = TimeSpan.FromSeconds(15);
        }

        public async Task<AgentConnectResponse?> TryConnectAsync(string id, string name)
        {
            try
            {
                var payload = new { id, name };
                var response = await _httpClient.PostAsJsonAsync("/api/agentes/conectar", payload);
                
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<AgentConnectResponse>() ?? null;
                }
            }
            catch (HttpRequestException ex)
            {
                // Logar silenciosamente quando a API estiver indisponivel
                Console.WriteLine($"[AgentConnectService] API indisponivel ({ex.Message}). Continuando...");
            }
            catch (TaskCanceledException)
            {
                Console.WriteLine("[AgentConnectService] Timeout ao conectar na API. Continuando...");
            }

            return null;
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}