using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace NeskAgent.Proxy.Services.Nginx
{
    public class NginxSslService
    {
        /// <summary>
        /// Checks if a Let's Encrypt certificate exists for the given domain.
        /// </summary>
        public bool HasCertificate(string domain)
        {
            return File.Exists($"/etc/letsencrypt/live/{domain}/fullchain.pem");
        }

        public async Task<bool> GenerateAsync(string domain, string? email = null, CancellationToken ct = default)
        {
            // Se tem email valido, usa --email (registro normal). Caso contrario usa
            // --register-unsafely-without-email (gambiarra para dominios de teste).
            string emailArg;
            if (!string.IsNullOrWhiteSpace(email) && email.Contains('@'))
            {
                emailArg = $"--email {email}";
            }
            else
            {
                emailArg = "--register-unsafely-without-email";
            }

            // --nginx edita a config do nginx automaticamente
            // --non-interactive = sem prompt
            // --agree-tos = aceita ToS
            // --no-redirect = nao tenta forcar HTTPS (deixa o painel decidir)
            var command = $"sudo certbot --nginx -d {domain} --cert-name {domain} --non-interactive --agree-tos {emailArg} --no-redirect";
            return await RunCertbotAsync(command, ct);
        }

        public async Task<bool> DeleteAsync(string domain, CancellationToken ct)
        {
            var command = $"sudo certbot delete --cert-name {domain} --non-interactive";
            return await RunCertbotAsync(command, ct);
        }

        private async Task<bool> RunCertbotAsync(string command, CancellationToken ct)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = $"-c \"{command}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            await process.WaitForExitAsync(ct);
            return process.ExitCode == 0;
        }
    }
}
