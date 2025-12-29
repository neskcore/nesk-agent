const { pool } = require('../db/mysql');
const NginxService = require('./nginxService');
const { v4: uuidv4 } = require('uuid');

/**
 * Serviço responsável pela lógica de negócio dos Proxies
 */
class ProxyService {
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