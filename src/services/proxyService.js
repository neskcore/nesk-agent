const { pool } = require('../db/mysql');
const NginxService = require('./nginxService');
const { v4: uuidv4 } = require('uuid');
const fs = require('fs');
const path = require('path');

const ATTACHMENTS_DIR = path.join(__dirname, "..", "cdn", "attachments");
const TEMP_CHUNKS_DIR = path.join(__dirname, "..", "cdn", "temp_chunks");

/**
 * Serviço responsável pela lógica de negócio dos Proxies
 */
class ProxyService {
  /**
   * Lida com o upload de arquivos em partes (Chunks) usando multipart/form-data
   */
  static async handleChunkUpload({ chunkFile, chunkIndex, totalChunks, fileName, subfolder }) {
    if (!chunkFile) throw new Error('Arquivo de chunk não recebido');

    if (!fs.existsSync(TEMP_CHUNKS_DIR)) {
      fs.mkdirSync(TEMP_CHUNKS_DIR, { recursive: true });
    }

    const fileTempDir = path.join(TEMP_CHUNKS_DIR, fileName);
    if (!fs.existsSync(fileTempDir)) {
      fs.mkdirSync(fileTempDir, { recursive: true });
    }

    // Move o chunk temporário do multer para nossa pasta de controle
    const chunkDest = path.join(fileTempDir, `chunk_${chunkIndex}`);
    fs.renameSync(chunkFile.path, chunkDest);

    // Verifica se todas as partes chegaram
    const uploadedChunks = fs.readdirSync(fileTempDir);
    if (uploadedChunks.length === totalChunks) {
      // Monta o arquivo final
      const cleanSubfolder = String(subfolder || "").replace(/\\/g, "/").replace(/^\/+|\/+$/g, "").trim();
      const targetDir = cleanSubfolder 
        ? path.join(ATTACHMENTS_DIR, cleanSubfolder)
        : ATTACHMENTS_DIR;

      if (!fs.existsSync(targetDir)) {
        fs.mkdirSync(targetDir, { recursive: true });
      }

      const finalPath = path.join(targetDir, fileName);
      const writeStream = fs.createWriteStream(finalPath);

      // Ordenar chunks numericamente para garantir a ordem correta
      const sortedChunks = uploadedChunks
        .filter(c => c.startsWith('chunk_'))
        .sort((a, b) => {
          return parseInt(a.split('_')[1]) - parseInt(b.split('_')[1]);
        });

      for (const chunkName of sortedChunks) {
        const chunkPath = path.join(fileTempDir, chunkName);
        const data = fs.readFileSync(chunkPath);
        writeStream.write(data);
        fs.unlinkSync(chunkPath);
      }

      writeStream.end();
      
      // Limpa diretório temporário
      setTimeout(() => {
        try {
          if (fs.existsSync(fileTempDir)) fs.rmdirSync(fileTempDir);
        } catch (e) {
          console.error('Erro ao remover pasta temporária:', e.message);
        }
      }, 1000);

      return {
        filename: fileName,
        path: subfolder ? `attachments/${subfolder}/${fileName}` : `attachments/${fileName}`,
        completed: true
      };
    }

    return { completed: false, received: uploadedChunks.length };
  }
  /**
   * Cria um novo proxy no banco e aplica no Nginx se estiver ativado
   */
  static async createProxy(data) {
    const { domain, target_host, target_port, enabled } = data;

    if (!domain || !target_host) {
      throw new Error('Domínio e host de destino são obrigatórios');
    }

    if (target_port && (target_port < 1 || target_port > 65535)) {
      throw new Error('Número de porta inválido');
    }

    const [existing] = await pool.execute('SELECT id FROM proxies WHERE domain = ?', [domain]);
    if (existing.length > 0) {
      throw new Error('Este domínio já está em uso');
    }

    const id = uuidv4();
    const isEnabled = enabled === true || enabled === 1;

    await pool.execute(
      'INSERT INTO proxies (id, domain, target_host, target_port, enabled) VALUES (?, ?, ?, ?, ?)',
      [id, domain, target_host, target_port || null, isEnabled ? 1 : 0]
    );

    const proxy = { id, domain, target_host, target_port, enabled: isEnabled };

    if (isEnabled) {
      await NginxService.applyConfig(proxy);
    }

    return proxy;
  }

  /**
   * Atualiza as configurações de um proxy existente
   */
  static async updateProxy(id, data) {
    const [rows] = await pool.execute('SELECT * FROM proxies WHERE id = ?', [id]);
    if (rows.length === 0) throw new Error('Proxy não encontrado');

    const current = rows[0];
    const domain = data.domain || current.domain;
    const target_host = data.target_host || current.target_host;
    const target_port = data.target_port !== undefined ? data.target_port : current.target_port;
    const enabled = data.enabled !== undefined ? (data.enabled ? 1 : 0) : current.enabled;

    await pool.execute(
      'UPDATE proxies SET domain = ?, target_host = ?, target_port = ?, enabled = ? WHERE id = ?',
      [domain, target_host, target_port, enabled, id]
    );

    const updated = { id, domain, target_host, target_port, enabled: !!enabled };

    if (enabled) {
      await NginxService.applyConfig(updated);
    } else {
      await NginxService.removeConfig(id, current.domain);
    }

    return updated;
  }

  /**
   * Ativa ou desativa um proxy
   */
  static async setEnabled(id, enabled) {
    const [rows] = await pool.execute('SELECT * FROM proxies WHERE id = ?', [id]);
    if (rows.length === 0) throw new Error('Proxy não encontrado');

    const proxy = rows[0];
    const isEnabled = !!enabled;

    await pool.execute('UPDATE proxies SET enabled = ? WHERE id = ?', [isEnabled ? 1 : 0, id]);

    const updatedProxy = { ...proxy, id, enabled: isEnabled };

    if (isEnabled) {
      await NginxService.applyConfig(updatedProxy);
    } else {
      await NginxService.removeConfig(id, proxy.domain);
    }

    return updatedProxy;
  }

  /**
   * Remove permanentemente um proxy e seus certificados
   */
  static async deleteProxy(id) {
    const [rows] = await pool.execute('SELECT domain FROM proxies WHERE id = ?', [id]);
    const domain = rows.length > 0 ? rows[0].domain : null;
    
    // Tenta remover a config do Nginx e o SSL antes de apagar do banco
    await NginxService.removeConfig(id, domain);
    
    const [result] = await pool.execute('DELETE FROM proxies WHERE id = ?', [id]);
    return result.affectedRows > 0;
  }

  /**
   * Lista todos os proxies registrados
   */
  static async getAll() {
    const [rows] = await pool.execute('SELECT * FROM proxies ORDER BY created_at DESC');
    return rows.map(r => ({ ...r, enabled: !!r.enabled }));
  }
}

module.exports = ProxyService;