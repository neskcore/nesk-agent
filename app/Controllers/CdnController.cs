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
        public async Task<IActionResult> CreateFolder([FromBody] dynamic body)
        {
            try
            {
                string name = body.name;
                string subfolder = body.subfolder ?? "";
                await _cdnService.CreateFolderAsync(name, subfolder);
                return Ok(new { success = true, message = "Pasta criada com sucesso" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadFile([FromForm] IFormFile file, [FromForm] string subfolder = "")
        {
            try
            {
                if (file == null) return BadRequest(new { success = false, error = "Nenhum arquivo enviado" });

                var targetDir = Path.Combine(AppContext.BaseDirectory, "cdn", "attachments", (subfolder ?? "").Replace("\\", "/").Trim('/'));
                if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

                var filePath = Path.Combine(targetDir, file.FileName);
                using (var stream = System.IO.File.Create(filePath))
                {
                    await file.CopyToAsync(stream);
                }

                var data = await _cdnService.ProcessUploadAsync(file.FileName, subfolder);
                return Ok(new { 
                    success = true, 
                    message = "Upload realizado com sucesso",
                    data
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        [HttpDelete("item")]
        public async Task<IActionResult> DeleteItem([FromBody] dynamic body)
        {
            try
            {
                string itemPath = body.item_path;
                // Assuming itemPath might need parsing or just using name/subfolder
                // For simplicity, let's assume body has name and subfolder
                string name = body.name;
                string subfolder = body.subfolder ?? "";
                
                var success = await _cdnService.DeleteItemAsync(name, subfolder);
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
