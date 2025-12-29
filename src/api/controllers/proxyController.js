const ProxyService = require('../../services/proxyService');
const NginxService = require('../../services/nginxService');

/**
 * Controller para gerenciar as requisições de Proxy
 */
class ProxyController {
  static async create(req, res) {
    try {
      const proxy = await ProxyService.createProxy(req.body);
      res.status(201).json({ success: true, data: proxy });
    } catch (error) {
      res.status(400).json({ success: false, error: error.message });
    }
  }

  static async getAll(req, res) {
    try {
      const proxies = await ProxyService.getAll();
      res.json({ success: true, data: proxies });
    } catch (error) {
      res.status(500).json({ success: false, error: error.message });
    }
  }

  static async update(req, res) {
    try {
      const proxy = await ProxyService.updateProxy(req.params.id, req.body);
      res.json({ success: true, data: proxy });
    } catch (error) {
      res.status(400).json({ success: false, error: error.message });
    }
  }

  static async enable(req, res) {
    try {
      const proxy = await ProxyService.setEnabled(req.params.id, true);
      res.json({ success: true, data: proxy });
    } catch (error) {
      res.status(400).json({ success: false, error: error.message });
    }
  }

  static async disable(req, res) {
    try {
      const proxy = await ProxyService.setEnabled(req.params.id, false);
      res.json({ success: true, data: proxy });
    } catch (error) {
      res.status(400).json({ success: false, error: error.message });
    }
  }

  static async delete(req, res) {
    try {
      const success = await ProxyService.deleteProxy(req.params.id);
      res.json({ success });
    } catch (error) {
      res.status(400).json({ success: false, error: error.message });
    }
  }

  static async getConfig(req, res) {
    try {
      const config = await NginxService.getConfig(req.params.id);
      res.json({ success: true, data: config });
    } catch (error) {
      res.status(404).json({ success: false, error: error.message });
    }
  }

  static async saveConfig(req, res) {
    try {
      const { content } = req.body;
      if (!content) throw new Error('Conteúdo é obrigatório');
      
      await NginxService.saveConfig(req.params.id, content);
      res.json({ success: true, message: 'Configuração salva com sucesso' });
    } catch (error) {
      res.status(400).json({ success: false, error: error.message });
    }
  }

  static async uploadChunk(req, res) {
    try {
      const { chunkIndex, totalChunks, fileName, subfolder } = req.body;
      const data = await ProxyService.handleChunkUpload({ 
        chunkFile: req.file, 
        chunkIndex: parseInt(chunkIndex), 
        totalChunks: parseInt(totalChunks), 
        fileName, 
        subfolder 
      });
      res.json({ success: true, data });
    } catch (error) {
      console.error('[AGENT CHUNK ERROR]', error);
      res.status(500).json({ success: false, error: error.message });
    }
  }
}

module.exports = ProxyController;