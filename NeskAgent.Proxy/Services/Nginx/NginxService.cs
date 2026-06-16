using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NeskAgent.Proxy.Services.Nginx.Exceptions;

namespace NeskAgent.Proxy.Services.Nginx
{
    public class NginxService
    {
        private readonly NginxConfigService _configService;
        private readonly NginxConfigGenerator _configGenerator;
        private readonly NginxProcessService _processService;
        private readonly NginxSslService _sslService;
        private readonly SemaphoreSlim _semaphore = new(1, 1);

        public NginxService(
            NginxConfigService configService,
            NginxConfigGenerator configGenerator,
            NginxProcessService processService,
            NginxSslService sslService)
        {
            _configService = configService;
            _configGenerator = configGenerator;
            _processService = processService;
            _sslService = sslService;
        }

        public async Task UpdateProxyAsync(string proxyId, string domain, string targetHost, int targetPort, bool enabled, bool? sslAvailable, CancellationToken ct)
        {
            await _semaphore.WaitAsync(ct);
            try
            {
                // V2 behavior: if API doesn't specify ssl_available, default to whether cert exists
                bool certExists = !string.IsNullOrEmpty(domain) && _sslService.HasCertificate(domain);
                bool effectiveSslAvailable = sslAvailable ?? certExists;
                bool actualSsl = enabled && effectiveSslAvailable && certExists;

                // Garante diretório e HTML de manutenção antes de qualquer operação
                _configService.EnsureDirectoryExists();
                if (!enabled) _configService.EnsureMaintenanceFile();

                var config = _configGenerator.Generate(domain, targetHost, targetPort, enabled, actualSsl);
                await _configService.SaveConfigAsync($"nesk_proxy_{proxyId}.conf", config);
                await ReloadWithRetryAsync(ct);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task DeleteProxyAsync(string proxyId, string domain, CancellationToken ct)
        {
            await _semaphore.WaitAsync(ct);
            try
            {
                // Fallback: se domain veio vazio, tenta recuperar do arquivo de config
                if (string.IsNullOrEmpty(domain))
                {
                    domain = await _configService.GetDomainFromConfigAsync(proxyId) ?? "";
                    if (!string.IsNullOrEmpty(domain))
                        Console.WriteLine($"[NginxService] Domínio '{domain}' recuperado da config para deleção.");
                }

                var filename = $"nesk_proxy_{proxyId}.conf";
                await _configService.DeleteConfigAsync(filename);
                await _configService.DeleteBackupAsync(filename);
                if (!string.IsNullOrEmpty(domain))
                    await _sslService.DeleteAsync(domain, ct);
                await ReloadWithRetryAsync(ct);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task ToggleProxyAsync(string proxyId, string domain, string targetHost, int targetPort, bool active, bool? sslAvailable, CancellationToken ct)
        {
            await _semaphore.WaitAsync(ct);
            try
            {
                var filename = $"nesk_proxy_{proxyId}.conf";
                _configService.EnsureDirectoryExists();

                if (active)
                {
                    await _configService.RestoreAsync(filename);
                    await _configService.DeleteBackupAsync(filename);
                }
                else
                {
                    await _configService.BackupAsync(filename);
                    _configService.EnsureMaintenanceFile();
                    // V2 behavior: if API doesn't specify ssl_available, default to whether cert exists
                    bool certExists = !string.IsNullOrEmpty(domain) && _sslService.HasCertificate(domain);
                    bool effectiveSslAvailable = sslAvailable ?? certExists;
                    bool includeSsl = effectiveSslAvailable && certExists;
                    var config = _configGenerator.Generate(domain, targetHost, targetPort, false, includeSsl);
                    await _configService.SaveConfigAsync(filename, config);
                }
                await ReloadWithRetryAsync(ct);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<string> GetConfigAsync(string proxyId, CancellationToken ct)
        {
            var filename = $"nesk_proxy_{proxyId}.conf";
            return await _configService.GetConfigAsync(filename);
        }

        public async Task SaveRawConfigAsync(string filename, string content, CancellationToken ct)
        {
            await _semaphore.WaitAsync(ct);
            try
            {
                _configService.EnsureDirectoryExists();
                await _configService.SaveConfigAsync(filename, content);
                await ReloadWithRetryAsync(ct);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// Tenta recarregar o Nginx. Em caso de falha por "cannot load certificate"
        /// (orphan config), limpa os configs órfãos e tenta novamente.
        /// </summary>
        private async Task ReloadWithRetryAsync(CancellationToken ct)
        {
            try
            {
                await _processService.ReloadAsync(ct);
            }
            catch (NginxOrphanConfigException ex)
            {
                Console.WriteLine($"[NginxService] {ex.Message}. Limpando orphan configs...");
                await _configService.CleanupOrphanConfigsAsync();
                await _processService.ReloadAsync(ct);
            }
        }

        public async Task<bool> GenerateSslAsync(string domain, string? email, CancellationToken ct)
        {
            return await _sslService.GenerateAsync(domain, email, ct);
        }

        public async Task<bool> DeleteSslAsync(string domain, CancellationToken ct)
        {
            return await _sslService.DeleteAsync(domain, ct);
        }

        /// <summary>
        /// Roda nginx -t + reload, retornando sucesso e a stderr em caso de erro.
        /// Usado por save_ssl_files e outros caminhos que precisam de feedback
        /// sem propagar exception.
        /// </summary>
        public async Task<(bool success, string error)> TestAndReloadAsync(CancellationToken ct)
        {
            try
            {
                await _processService.ReloadAsync(ct);
                return (true, "");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        /// <summary>
        /// Checks if an SSL certificate already exists for the given domain.
        /// </summary>
        public bool HasCertificate(string domain)
        {
            return !string.IsNullOrEmpty(domain) && _sslService.HasCertificate(domain);
        }

        public async Task HandleOrphanCleanupAsync(string proxyId, string domain, CancellationToken ct)
        {
            var filename = $"nesk_proxy_{proxyId}.conf";
            await _configService.DeleteConfigAsync(filename);
            await _configService.CleanupOrphanConfigsAsync();
            Console.WriteLine($"[NginxService] Orphan config '{filename}' removida (proxyId={proxyId}, domain={domain}).");
        }
    }
}
