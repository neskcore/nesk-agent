using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using NeskAgent.Command;
using NeskAgent.Core.Services;
using NeskAgent.Plugins;
using NeskAgent.Proxy;
using NeskAgent.Proxy.Services.Nginx;
using NeskAgent.Command.Models;
using DotNetEnv;

namespace NeskAgent
{
    class Program
    {
		static async Task<(bool approved, string? token, DateTime? expiresAt, string? refreshToken)> RegisterAgentAsync(string apiUrl, string agentId, string agentName)
		{
			try
			{
				var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
				httpClient.BaseAddress = new Uri(apiUrl);

				var payload = new { id = agentId, name = agentName };
				var response = await httpClient.PostAsJsonAsync("/api/agentes/conectar", payload);

				if (!response.IsSuccessStatusCode)
				{
					Console.WriteLine($"[NeskAgent] Registration failed: HTTP {response.StatusCode}");
					return (false, null, null, null);
				}

				var result = await response.Content.ReadFromJsonAsync<AgentConnectResponse>();
				if (result == null)
				{
					Console.WriteLine("[NeskAgent] Registration failed: empty response.");
					return (false, null, null, null);
				}

				if (result.Status == "approved" || result.Status == "pending")
				{
					DateTime? expiresAt = null;
					if (!string.IsNullOrEmpty(result.ExpiresAt) && DateTime.TryParse(result.ExpiresAt, out var parsedDate))
					{
						expiresAt = parsedDate;
					}
					Console.WriteLine($"[NeskAgent] Agent status: {result.Status}. Token expires at: {result.ExpiresAt}");
					
					// Even if pending, return the token so we can authenticate WebSocket
					return (true, result.Token, expiresAt, result.RefreshToken);
				}

				Console.WriteLine($"[NeskAgent] Agent status: {result.Status}. Waiting for admin approval...");
				return (false, null, null, null);
			}
			catch (HttpRequestException ex)
			{
				Console.WriteLine($"[NeskAgent] Registration failed: {ex.Message}");
				return (false, null, null, null);
			}
		}

        static string NormalizeWebSocketUrl(string apiUrl)
        {
            var url = apiUrl.TrimEnd('/');

            if (url.StartsWith("http://"))
            {
                url = "ws://" + url["http://".Length..];
            }
            else if (url.StartsWith("https://"))
            {
                url = "wss://" + url["https://".Length..];
            }
            else if (!url.StartsWith("ws://") && !url.StartsWith("wss://"))
            {
                url = "wss://" + url;
            }

            return url;
        }

        static async Task Main(string[] args)
        {
            Env.TraversePath().Load();

            var agentId = Environment.GetEnvironmentVariable("AGENT_ID") ?? "default-agent-id";
            var agentName = Environment.GetEnvironmentVariable("AGENT_NAME") ?? "NeskAgent";
            var apiUrl = Environment.GetEnvironmentVariable("API_CENTRAL_URL") ?? "https://agent.nesk.fun";

            // Use HTTPS for registration API
            var httpBase = apiUrl;
            if (httpBase.StartsWith("ws://"))
            {
                httpBase = "http://" + httpBase["ws://".Length..];
            }
            else if (httpBase.StartsWith("wss://"))
            {
                httpBase = "https://" + httpBase["wss://".Length..];
            }
            else if (!httpBase.StartsWith("http://") && !httpBase.StartsWith("https://"))
            {
                httpBase = "https://" + httpBase;
            }

            Console.WriteLine("========================================");
            Console.WriteLine("  NESK AGENT v3");
            Console.WriteLine("========================================");
            Console.WriteLine($"  Agent ID:   {agentId}");
            Console.WriteLine($"  Agent Name: {agentName}");
            Console.WriteLine($"  API URL:    {httpBase}");
            Console.WriteLine("========================================");

            // 1. Try to load stored token, or register new agent
            string? authToken = null;
            string? refreshToken = null;
            DateTime? tokenExpiresAt = null;
            bool isRegistered = false;

            var tokenStorage = new TokenStorage();
            var storedToken = await tokenStorage.LoadAsync();

            if (storedToken != null && !string.IsNullOrEmpty(storedToken.Token))
            {
                Console.WriteLine("[NeskAgent] Found stored token. Will use it for authentication.");
                authToken = storedToken.Token;
                refreshToken = storedToken.RefreshToken;
                tokenExpiresAt = storedToken.ExpiresAt;
                isRegistered = true;
            }
            else
            {
                while (!isRegistered)
                {
                    (isRegistered, authToken, tokenExpiresAt, refreshToken) = await RegisterAgentAsync(httpBase, agentId, agentName);
                    if (!isRegistered)
                    {
                        Console.WriteLine("[NeskAgent] Retrying in 10 seconds...");
                        await Task.Delay(TimeSpan.FromSeconds(10));
                    }
                }

                // Save the new token for future reconnects
                if (!string.IsNullOrEmpty(authToken))
                {
                    Console.WriteLine($"[NeskAgent] Saving new token and refresh token.");
                    await tokenStorage.SaveAsync(authToken, tokenExpiresAt, refreshToken);
                }
            }

            // 2. Construct the WebSocket URL
            var wsBase = NormalizeWebSocketUrl(apiUrl);
            var wsUrl = $"{wsBase}/ws/agent/{agentId}";

            // 3. Setup plugins and start the agent
            var shellEnabled = bool.TryParse(Environment.GetEnvironmentVariable("SHELL_ENABLED"), out var se) ? se : false;

            Console.WriteLine("========================================");
            Console.WriteLine("  Starting plugins...");
            Console.WriteLine("========================================");

            var router = new CommandRouter();
            router.RegisterPlugin(new TelemetryPlugin());
            router.RegisterPlugin(new ShellPlugin(shellEnabled));

            var nginxService = new NginxService(
                new NginxConfigService(),
                new NginxConfigGenerator(),
                new NginxProcessService(),
                new NginxSslService()
            );
            router.RegisterPlugin(new ProxyPlugin(nginxService));

            Console.WriteLine("========================================");
            Console.WriteLine($"  Connecting to WebSocket...");
            Console.WriteLine($"  URL: {wsUrl}");
            Console.WriteLine("========================================");

            // 4. Start the agent with the authentication token
            using var core = new AgentCore(router, wsUrl, agentId, agentName, authToken, tokenExpiresAt, tokenStorage, refreshToken);
            await core.RunAsync(CancellationToken.None);
        }
    }
}