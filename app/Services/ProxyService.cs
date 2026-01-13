using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MySqlConnector;
using Dapper;
using NeskAgent.Models;

namespace NeskAgent.Services
{
    public class ProxyService
    {
        private readonly string _connectionString;
        private static readonly string ATTACHMENTS_DIR = Path.Combine(AppContext.BaseDirectory, "cdn", "attachments");
        private static readonly string TEMP_CHUNKS_DIR = Path.Combine(AppContext.BaseDirectory, "cdn", "temp_chunks");

        public ProxyService(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<Proxy> CreateProxyAsync(Proxy proxy)
        {
            if (string.IsNullOrEmpty(proxy.Domain) || string.IsNullOrEmpty(proxy.TargetHost))
            {
                throw new ArgumentException("Domínio e host de destino são obrigatórios");
            }

            if (proxy.TargetPort < 1 || proxy.TargetPort > 65535)
            {
                throw new ArgumentException("Número de porta inválido");
            }

            using (var connection = new MySqlConnection(_connectionString))
            {
                var existing = await connection.QueryFirstOrDefaultAsync<string>(
                    "SELECT id FROM proxies WHERE domain = @Domain", new { proxy.Domain });
                
                if (existing != null)
                {
                    throw new Exception("Este domínio já está em uso");
                }

                proxy.Id = Guid.NewGuid().ToString();
                proxy.CreatedAt = DateTime.UtcNow;

                await connection.ExecuteAsync(
                    "INSERT INTO proxies (id, domain, target_host, target_port, enabled, created_at) VALUES (@Id, @Domain, @TargetHost, @TargetPort, @Enabled, @CreatedAt)",
                    proxy);

                if (proxy.Enabled)
                {
                    await ApplyConfigAsync(proxy);
                }

                return proxy;
            }
        }

        public async Task<IEnumerable<Proxy>> GetAllAsync()
        {
            using (var connection = new MySqlConnection(_connectionString))
            {
                var rows = await connection.QueryAsync<Proxy>("SELECT * FROM proxies ORDER BY created_at DESC");
                return rows;
            }
        }

        public async Task<Proxy> UpdateProxyAsync(string id, Proxy data)
        {
            using (var connection = new MySqlConnection(_connectionString))
            {
                var current = await connection.QueryFirstOrDefaultAsync<Proxy>("SELECT * FROM proxies WHERE id = @id", new { id });
                if (current == null) throw new Exception("Proxy não encontrado");

                current.Domain = data.Domain ?? current.Domain;
                current.TargetHost = data.TargetHost ?? current.TargetHost;
                current.TargetPort = data.TargetPort != 0 ? data.TargetPort : current.TargetPort;
                current.Enabled = data.Enabled;

                await connection.ExecuteAsync(
                    "UPDATE proxies SET domain = @Domain, target_host = @TargetHost, target_port = @TargetPort, enabled = @Enabled WHERE id = @Id",
                    current);

                if (current.Enabled)
                {
                    await ApplyConfigAsync(current);
                }
                else
                {
                    await RemoveConfigAsync(id, current.Domain);
                }

                return current;
            }
        }

        public async Task<Proxy> SetEnabledAsync(string id, bool enabled)
        {
            using (var connection = new MySqlConnection(_connectionString))
            {
                var proxy = await connection.QueryFirstOrDefaultAsync<Proxy>("SELECT * FROM proxies WHERE id = @id", new { id });
                if (proxy == null) throw new Exception("Proxy não encontrado");

                proxy.Enabled = enabled;

                await connection.ExecuteAsync("UPDATE proxies SET enabled = @Enabled WHERE id = @Id", new { Enabled = enabled ? 1 : 0, Id = id });

                if (enabled)
                {
                    await ApplyConfigAsync(proxy);
                }
                else
                {
                    await RemoveConfigAsync(id, proxy.Domain);
                }

                return proxy;
            }
        }

        public async Task<bool> DeleteProxyAsync(string id)
        {
            using (var connection = new MySqlConnection(_connectionString))
            {
                var domain = await connection.QueryFirstOrDefaultAsync<string>("SELECT domain FROM proxies WHERE id = @id", new { id });
                
                await RemoveConfigAsync(id, domain);
                
                var affected = await connection.ExecuteAsync("DELETE FROM proxies WHERE id = @id", new { id });
                return affected > 0;
            }
        }

        public async Task ApplyConfigAsync(Proxy proxy)
        {
            var config = NginxService.GenerateConfig(proxy);
            NginxService.SaveConfig(proxy.Id, config);
            await NginxService.IssueCertificateAsync(proxy.Domain);
            
            // Regenerate config after cert issue to include SSL
            config = NginxService.GenerateConfig(proxy);
            NginxService.SaveConfig(proxy.Id, config);
            
            await NginxService.ReloadNginxAsync();
        }

        public async Task RemoveConfigAsync(string id, string domain)
        {
            NginxService.DeleteConfig(id);
            await NginxService.ReloadNginxAsync();
        }

        public async Task<object> HandleChunkUploadAsync(Stream chunkStream, int chunkIndex, int totalChunks, string fileName, string subfolder)
        {
            if (!Directory.Exists(TEMP_CHUNKS_DIR)) Directory.CreateDirectory(TEMP_CHUNKS_DIR);

            var fileTempDir = Path.Combine(TEMP_CHUNKS_DIR, fileName);
            if (!Directory.Exists(fileTempDir)) Directory.CreateDirectory(fileTempDir);

            var chunkDest = Path.Combine(fileTempDir, $"chunk_{chunkIndex}");
            using (var fs = File.Create(chunkDest))
            {
                await chunkStream.CopyToAsync(fs);
            }

            var uploadedChunks = Directory.GetFiles(fileTempDir, "chunk_*");
            if (uploadedChunks.Length == totalChunks)
            {
                var cleanSubfolder = (subfolder ?? "").Replace("\\", "/").Trim('/');
                var targetDir = string.IsNullOrEmpty(cleanSubfolder) 
                    ? ATTACHMENTS_DIR 
                    : Path.Combine(ATTACHMENTS_DIR, cleanSubfolder);

                if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

                var finalPath = Path.Combine(targetDir, fileName);
                using (var finalFs = File.Create(finalPath))
                {
                    var sortedChunks = uploadedChunks
                        .OrderBy(c => int.Parse(Path.GetFileName(c).Split('_')[1]));

                    foreach (var chunkPath in sortedChunks)
                    {
                        using (var chunkFs = File.OpenRead(chunkPath))
                        {
                            await chunkFs.CopyToAsync(finalFs);
                        }
                        File.Delete(chunkPath);
                    }
                }

                _ = Task.Run(async () => {
                    await Task.Delay(1000);
                    try { if (Directory.Exists(fileTempDir)) Directory.Delete(fileTempDir, true); } catch { }
                });

                return new {
                    filename = fileName,
                    path = string.IsNullOrEmpty(subfolder) ? $"attachments/{fileName}" : $"attachments/{subfolder}/{fileName}",
                    completed = true
                };
            }

            return new { completed = false, received = uploadedChunks.Length };
        }
    }
}
