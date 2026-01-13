using System;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using NeskAgent.Models;

namespace NeskAgent.Services
{
    public class NginxService
    {
        private static readonly string NGINX_CONF_PATH = Environment.GetEnvironmentVariable("NGINX_CONF_PATH") ?? "/etc/nginx/conf.d/";
        private static readonly string NGINX_BIN = Environment.GetEnvironmentVariable("NGINX_BIN_PATH") ?? "nginx";
        private const string CERTBOT_PATH = "/etc/letsencrypt/live/";

        public static string GenerateConfig(Proxy proxy)
        {
            var certPath = Path.Combine(CERTBOT_PATH, proxy.Domain);
            var hasCert = File.Exists(Path.Combine(certPath, "fullchain.pem"));

            if (hasCert)
            {
                return $@"
server {{
    listen 80;
    server_name {proxy.Domain};
    return 301 https://$host$request_uri;
}}

server {{
    listen 443 ssl http2;
    server_name {proxy.Domain};

    ssl_certificate {Path.Combine(certPath, "fullchain.pem")};
    ssl_certificate_key {Path.Combine(certPath, "privkey.pem")};

    # Otimizações SSL
    ssl_session_cache shared:SSL:10m;
    ssl_session_timeout 1d;
    ssl_protocols TLSv1.2 TLSv1.3;
    ssl_prefer_server_ciphers off;

    location / {{
        proxy_pass http://{proxy.TargetHost}:{proxy.TargetPort};
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        
        # Suporte a WebSockets
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection ""upgrade"";
    }}
}}".Trim();
            }

            return $@"
server {{
    listen 80;
    server_name {proxy.Domain};

    location / {{
        proxy_pass http://{proxy.TargetHost}:{proxy.TargetPort};
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        
        # Suporte a WebSockets
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection ""upgrade"";
    }}
}}".Trim();
        }

        public static async Task<bool> IssueCertificateAsync(string domain)
        {
            try
            {
                Console.WriteLine($"[CERTBOT] Gerando certificado para {domain}...");
                var processInfo = new ProcessStartInfo
                {
                    FileName = "sudo",
                    Arguments = $"certbot --nginx -d {domain} --non-interactive --agree-tos --register-unsafely-without-email",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(processInfo))
                {
                    await process.WaitForExitAsync();
                    if (process.ExitCode != 0)
                    {
                        var error = await process.StandardError.ReadToEndAsync();
                        Console.WriteLine($"[CERTBOT WARNING] Falha ao gerar certificado para {domain}: {error}");
                        return false;
                    }
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CERTBOT ERROR] {ex.Message}");
                return false;
            }
        }

        public static async Task ReloadNginxAsync()
        {
            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = "sudo",
                    Arguments = $"{NGINX_BIN} -s reload",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(processInfo))
                {
                    await process.WaitForExitAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NGINX ERROR] Falha ao recarregar: {ex.Message}");
            }
        }

        public static void SaveConfig(string proxyId, string config)
        {
            var fileName = $"nesk_proxy_{proxyId}.conf";
            var fullPath = Path.Combine(NGINX_CONF_PATH, fileName);
            File.WriteAllText(fullPath, config);
        }

        public static void DeleteConfig(string proxyId)
        {
            var fileName = $"nesk_proxy_{proxyId}.conf";
            var fullPath = Path.Combine(NGINX_CONF_PATH, fileName);
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }
    }
}
