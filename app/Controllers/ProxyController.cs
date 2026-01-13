using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NeskAgent.Models;
using NeskAgent.Services;
using Microsoft.AspNetCore.Http;
using System.IO;

namespace NeskAgent.Controllers
{
    [ApiController]
    [Route("api")]
    public class ProxyController : ControllerBase
    {
        private readonly ProxyService _proxyService;

        public ProxyController(ProxyService proxyService)
        {
            _proxyService = proxyService;
        }

        [HttpPost("proxy")]
        public async Task<IActionResult> Create([FromBody] Proxy proxy)
        {
            try
            {
                var result = await _proxyService.CreateProxyAsync(proxy);
                return CreatedAtAction(nameof(Create), new { success = true, data = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        [HttpGet("proxy")]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var result = await _proxyService.GetAllAsync();
                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        [HttpPut("proxy/{id}")]
        public async Task<IActionResult> Update(string id, [FromBody] Proxy proxy)
        {
            try
            {
                var result = await _proxyService.UpdateProxyAsync(id, proxy);
                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        [HttpPost("proxy/{id}/enable")]
        public async Task<IActionResult> Enable(string id)
        {
            try
            {
                var result = await _proxyService.SetEnabledAsync(id, true);
                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        [HttpPost("proxy/{id}/disable")]
        public async Task<IActionResult> Disable(string id)
        {
            try
            {
                var result = await _proxyService.SetEnabledAsync(id, false);
                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        [HttpDelete("proxy/{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            try
            {
                var success = await _proxyService.DeleteProxyAsync(id);
                return Ok(new { success });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        [HttpGet("proxy/{id}/config")]
        public IActionResult GetConfig(string id)
        {
            try
            {
                // In a real scenario, we might want to read from the file system
                // For now, let's return a message or implement the logic
                return Ok(new { success = true, data = "Configuração do Nginx (leitura do sistema não implementada diretamente aqui)" });
            }
            catch (Exception ex)
            {
                return NotFound(new { success = false, error = ex.Message });
            }
        }

        [HttpPost("proxy/{id}/config")]
        public IActionResult SaveConfig(string id, [FromBody] dynamic body)
        {
            try
            {
                string content = body.content;
                if (string.IsNullOrEmpty(content)) return BadRequest(new { success = false, error = "Conteúdo é obrigatório" });

                NginxService.SaveConfig(id, content);
                return Ok(new { success = true, message = "Configuração salva com sucesso" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        [HttpPost("cdn/upload/chunk")]
        public async Task<IActionResult> UploadChunk([FromForm] IFormFile chunk, [FromForm] string chunkIndex, [FromForm] string totalChunks, [FromForm] string fileName, [FromForm] string subfolder)
        {
            try
            {
                if (chunk == null) return BadRequest(new { success = false, error = "Arquivo de chunk não recebido" });

                using (var stream = chunk.OpenReadStream())
                {
                    var data = await _proxyService.HandleChunkUploadAsync(
                        stream, 
                        int.Parse(chunkIndex), 
                        int.Parse(totalChunks), 
                        fileName, 
                        subfolder);
                    return Ok(new { success = true, data });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }
    }
}
