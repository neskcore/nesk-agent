using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NeskAgent.Services;
using Microsoft.AspNetCore.Http;
using System.IO;

namespace NeskAgent.Controllers
{
    [ApiController]
    [Route("api/cdn")]
    public class CdnController : ControllerBase
    {
        private readonly CdnService _cdnService;

        public CdnController(CdnService cdnService)
        {
            _cdnService = cdnService;
        }

        [HttpGet("list")]
        public async Task<IActionResult> List([FromQuery] string subfolder = "")
        {
            try
            {
                var data = await _cdnService.ListAsync(subfolder);
                return Ok(new { success = true, data });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        [HttpPost("folder")]
        public async Task<IActionResult> CreateFolder([FromBody] Newtonsoft.Json.Linq.JObject body)
        {
            try
            {
                string name = body["name"]?.ToString();
                string subfolder = body["subfolder"]?.ToString() ?? "";
                
                if (string.IsNullOrEmpty(name))
                {
                    return BadRequest(new { success = false, error = "Nome da pasta é obrigatório" });
                }

                await _cdnService.CreateFolderAsync(name, subfolder);
                return Ok(new { success = true, message = "Pasta criada com sucesso" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        [HttpPost("upload")]
        [DisableRequestSizeLimit]
        [RequestFormLimits(MultipartBodyLengthLimit = 209715200)] // 200MB
        public async Task<IActionResult> UploadFile([FromForm] IFormFile file, [FromForm] string? subfolder)
        {
            try
            {
                if (file == null || file.Length == 0) return BadRequest(new { success = false, error = "Nenhum arquivo enviado ou arquivo vazio" });

                // Pega subfolder do form ou da query
                string? actualSubfolder = subfolder;
                if (string.IsNullOrEmpty(actualSubfolder) && Request.Query.ContainsKey("subfolder"))
                {
                    actualSubfolder = Request.Query["subfolder"];
                }

                var cleanSubfolder = (actualSubfolder ?? "").Replace("\\", "/").Trim('/');
                var targetDir = Path.Combine(AppContext.BaseDirectory, "cdn", "attachments", cleanSubfolder);
                
                if (!Directory.Exists(targetDir)) 
                    Directory.CreateDirectory(targetDir);

                var filePath = Path.Combine(targetDir, file.FileName);
                
                using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
                {
                    await file.CopyToAsync(stream);
                }

                var data = await _cdnService.ProcessUploadAsync(file.FileName, cleanSubfolder);
                return Ok(new { 
                    success = true, 
                    message = "Upload realizado com sucesso",
                    data
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CDN] Erro no upload: {ex.Message}");
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        [HttpDelete("item")]
        public async Task<IActionResult> DeleteItem([FromBody] Newtonsoft.Json.Linq.JObject body)
        {
            try
            {
                // No Node.js o campo é 'item_path'
                string itemPath = body["item_path"]?.ToString();
                
                // Fallback para 'name' e 'subfolder' se item_path não existir
                if (string.IsNullOrEmpty(itemPath))
                {
                    string name = body["name"]?.ToString();
                    string subfolder = body["subfolder"]?.ToString() ?? "";
                    
                    if (!string.IsNullOrEmpty(name))
                    {
                        itemPath = string.IsNullOrEmpty(subfolder) ? name : $"{subfolder.Trim('/')}/{name}";
                    }
                }

                if (string.IsNullOrEmpty(itemPath))
                {
                    return BadRequest(new { success = false, error = "Caminho do item (item_path) é obrigatório" });
                }
                
                var success = await _cdnService.DeleteItemAsync(itemPath, "");
                if (success)
                    return Ok(new { success = true, message = "Item removido com sucesso" });
                else
                    return NotFound(new { success = false, error = "Item não encontrado" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }
    }
}
