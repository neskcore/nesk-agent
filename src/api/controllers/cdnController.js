const CdnService = require("../../services/cdnService");

class CdnController {
  async list(req, res) {
    try {
      const { subfolder } = req.query;
      const data = await CdnService.list(subfolder);
      res.json({ success: true, data });
    } catch (error) {
      console.error("[CDN LIST ERROR]", error);
      res.status(error.message === "Diretório não encontrado" ? 404 : 500)
         .json({ success: false, error: error.message || "Falha ao listar arquivos" });
    }
  }

  async createFolder(req, res) {
    try {
      const { name, subfolder } = req.body;
      await CdnService.createFolder(name, subfolder);
      res.json({ success: true, message: "Pasta criada com sucesso" });
    } catch (error) {
      console.error("[CDN FOLDER ERROR]", error);
      res.status(error.message === "Pasta já existe" ? 400 : 500)
         .json({ success: false, error: error.message || "Falha ao criar pasta" });
    }
  }

  async uploadFile(req, res) {
    try {
      const subfolder = req.query.subfolder || req.body.subfolder || "";
      const data = await CdnService.processUpload(req.file, subfolder);
      res.json({ 
        success: true, 
        message: "Upload realizado com sucesso",
        data
      });
    } catch (error) {
      console.error("[CDN UPLOAD ERROR]", error);
      res.status(error.message === "Nenhum arquivo enviado" ? 400 : 500)
         .json({ success: false, error: error.message || "Falha ao realizar upload" });
    }
  }

  async deleteItem(req, res) {
    try {
      const { item_path } = req.body;
      await CdnService.deleteItem(item_path);
      res.json({ success: true, message: "Item removido com sucesso" });
    } catch (error) {
      console.error("[CDN DELETE ERROR]", error);
      res.status(error.message === "Item n�o encontrado" ? 404 : 500)
         .json({ success: false, error: error.message || "Falha ao remover item" });
    }
  }
}

module.exports = new CdnController();
