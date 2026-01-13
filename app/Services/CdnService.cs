using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace NeskAgent.Services
{
    public class CdnService
    {
        private static readonly string ATTACHMENTS_DIR = Path.Combine(AppContext.BaseDirectory, "cdn", "attachments");

        public async Task<IEnumerable<object>> ListAsync(string subfolder = "")
        {
            if (!Directory.Exists(ATTACHMENTS_DIR))
            {
                Directory.CreateDirectory(ATTACHMENTS_DIR);
            }

            var cleanSubfolder = (subfolder ?? "").Replace("\\", "/").Trim('/');
            var targetDir = Path.Combine(ATTACHMENTS_DIR, cleanSubfolder);

            if (!Directory.Exists(targetDir))
            {
                return Enumerable.Empty<object>();
            }

            var dirInfo = new DirectoryInfo(targetDir);
            var items = dirInfo.GetFileSystemInfos();

            return items.Select(item => {
                var isDirectory = (item.Attributes & FileAttributes.Directory) == FileAttributes.Directory;
                return new {
                    name = item.Name,
                    type = isDirectory ? "directory" : "file",
                    path = string.IsNullOrEmpty(cleanSubfolder) ? item.Name : $"{cleanSubfolder}/{item.Name}",
                    size = isDirectory ? (long?)null : ((FileInfo)item).Length,
                    mtime = item.LastWriteTime,
                    created_at = item.CreationTime
                };
            });
        }

        public async Task<bool> CreateFolderAsync(string name, string subfolder = "")
        {
            if (string.IsNullOrEmpty(name)) throw new Exception("Nome da pasta é obrigatório");

            var cleanSubfolder = (subfolder ?? "").Replace("\\", "/").Trim('/');
            var folderPath = Path.Combine(ATTACHMENTS_DIR, cleanSubfolder, name);

            if (Directory.Exists(folderPath))
            {
                throw new Exception("Pasta já existe");
            }

            Directory.CreateDirectory(folderPath);
            return true;
        }

        public async Task<bool> DeleteItemAsync(string name, string subfolder = "")
        {
            var cleanSubfolder = (subfolder ?? "").Replace("\\", "/").Trim('/');
            var itemPath = Path.Combine(ATTACHMENTS_DIR, cleanSubfolder, name);

            if (File.Exists(itemPath))
            {
                File.Delete(itemPath);
                return true;
            }
            else if (Directory.Exists(itemPath))
            {
                Directory.Delete(itemPath, true);
                return true;
            }

            return false;
        }

        public async Task<object> ProcessUploadAsync(string fileName, string subfolder = "")
        {
            var cleanSubfolder = (subfolder ?? "").Replace("\\", "/").Trim('/');
            var publicPath = string.IsNullOrEmpty(cleanSubfolder) 
                ? $"attachments/{fileName}" 
                : $"attachments/{cleanSubfolder}/{fileName}";

            return new {
                filename = fileName,
                path = publicPath
            };
        }
    }
}
