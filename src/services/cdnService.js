const fs = require("fs");
const path = require("path");
const multer = require("multer");

const ATTACHMENTS_DIR = path.join(__dirname, "..", "cdn", "attachments");

class CdnService {
  /**
   * Configura��o do Multer para Upload
   */
  static get storage() {
    return multer.diskStorage({
      destination: (req, file, cb) => {
        // Pega o subfolder da query ou do body (campos que vierem antes do arquivo)
        const subfolder = req.query.subfolder || req.body.subfolder || "";
        const cleanSubfolder = String(subfolder).replace(/\\/g, "/").replace(/^\/+|\/+$/g, "").trim();
        const targetDir = cleanSubfolder 
          ? path.join(ATTACHMENTS_DIR, cleanSubfolder)
          : ATTACHMENTS_DIR;
          
        if (!fs.existsSync(targetDir)) {
          fs.mkdirSync(targetDir, { recursive: true });
        }
        cb(null, targetDir);
      },
      filename: (req, file, cb) => {
        cb(null, file.originalname);
      }
    });
  }

  static get upload() {
    return multer({ 
      storage: this.storage,
      limits: { fileSize: 100 * 1024 * 1024 } // 100MB
    });
  }

  /**
   * Listar pastas e arquivos de um diret�rio
   */
  static async list(subfolder = "") {
    // Garante que o diret�rio raiz exista
    if (!fs.existsSync(ATTACHMENTS_DIR)) {
      fs.mkdirSync(ATTACHMENTS_DIR, { recursive: true });
    }

    const cleanSubfolder = String(subfolder || "").replace(/\\/g, "/").trim();
    const targetDir = path.join(ATTACHMENTS_DIR, cleanSubfolder);
    console.log(`[CDN LIST] subfolder: "${subfolder}", targetDir: "${targetDir}"`);

    if (!fs.existsSync(targetDir)) {
      return []; // Retorna lista vazia se a subpasta espec�fica n�o existir
    }

    const items = fs.readdirSync(targetDir, { withFileTypes: true });
    return items.map(item => {
      const fullPath = path.join(targetDir, item.name);
      const stats = fs.statSync(fullPath);
      const isDirectory = item.isDirectory();
      return {
        name: item.name,
        type: isDirectory ? "directory" : "file",
        path: subfolder ? `${subfolder}/${item.name}` : item.name,
        size: item.isFile() ? stats.size : null,
        mtime: stats.mtime,
        created_at: stats.birthtime
      };
    });
  }

  /**
   * Criar uma nova pasta
   */
  static async createFolder(name, subfolder = "") {
    if (!name) throw new Error("Nome da pasta é obrigatório");
    
    // Garante que o diret�rio raiz exista
    if (!fs.existsSync(ATTACHMENTS_DIR)) {
      fs.mkdirSync(ATTACHMENTS_DIR, { recursive: true });
    }

    const folderPath = path.join(ATTACHMENTS_DIR, subfolder, name);
    if (fs.existsSync(folderPath)) {
      throw new Error("Pasta já existe");
    }
    fs.mkdirSync(folderPath, { recursive: true });
    return true;
  }

  /**
   * Processar informa��es de um arquivo ap�s upload
   */
  static async processUpload(file, subfolder = "") {
    if (!file) throw new Error("Nenhum arquivo enviado");
    const publicPath = subfolder 
      ? `attachments/${subfolder}/${file.originalname}` 
      : `attachments/${file.originalname}`;

    return {
      filename: file.originalname,
      path: publicPath,
      size: file.size
    };
  }

  /**
   * Remover um arquivo ou pasta
   */
  static async deleteItem(itemPath) {
    if (!itemPath) throw new Error("Caminho do item é obrigatório");
    const fullPath = path.join(ATTACHMENTS_DIR, itemPath);
    if (!fs.existsSync(fullPath)) {
      throw new Error("Item não encontrado");
    }
    const stats = fs.statSync(fullPath);
    if (stats.isDirectory()) {
      fs.rmSync(fullPath, { recursive: true, force: true });
    } else {
      fs.unlinkSync(fullPath);
    }
    return true;
  }
}

module.exports = CdnService;
