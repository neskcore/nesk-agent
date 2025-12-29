const fs = require('fs');
const path = require('path');
const { execSync } = require('child_process');
require('dotenv').config();

const NGINX_CONF_PATH = process.env.NGINX_CONF_PATH || '/etc/nginx/conf.d/';
const NGINX_BIN = process.env.NGINX_BIN_PATH || 'nginx';
const CERTBOT_PATH = '/etc/letsencrypt/live/';

/**
 * Serviço responsável por gerenciar as configurações do Nginx e Certificados SSL
 */
class NginxService {
  /**
   * Gera a string de configuração do Nginx
   */
  static generateConfig(proxy) {
    const certPath = path.join(CERTBOT_PATH, proxy.domain);
    const hasCert = fs.existsSync(path.join(certPath, 'fullchain.pem'));

    if (hasCert) {
      return `
server {
    listen 80;
    server_name ${proxy.domain};
    return 301 https://$host$request_uri;
}

server {
    listen 443 ssl http2;
    server_name ${proxy.domain};

    ssl_certificate ${path.join(certPath, 'fullchain.pem')};
    ssl_certificate_key ${path.join(certPath, 'privkey.pem')};

    # Otimizações SSL
    ssl_session_cache shared:SSL:10m;
    ssl_session_timeout 1d;
    ssl_protocols TLSv1.2 TLSv1.3;
    ssl_prefer_server_ciphers off;

    location / {
        proxy_pass http://${proxy.target_host}:${proxy.target_port};
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        
        # Suporte a WebSockets
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
    }
}
`.trim();
    }

    return `
server {
    listen 80;
    server_name ${proxy.domain};

    location / {
        proxy_pass http://${proxy.target_host}:${proxy.target_port};
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        
        # Suporte a WebSockets
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
    }
}
`.trim();
  }

  /**
   * Emite um certificado SSL via Certbot
   */
  static async issueCertificate(domain) {
    try {
      console.log(`[CERTBOT] Gerando certificado para ${domain}...`);
      execSync(`sudo certbot --nginx -d ${domain} --non-interactive --agree-tos --register-unsafely-without-email`, { stdio: 'pipe' });
      return true;
    } catch (error) {
      console.warn(`[CERTBOT WARNING] Falha ao gerar certificado para ${domain}: ${error.stderr?.toString() || error.message}`);
      return false;
    }
  }

  /**
   * Retorna o conteúdo do arquivo de configuração de um proxy
   */
  static async getConfig(proxyId) {
    const fileName = `nesk_proxy_${proxyId}.conf`;
    const fullPath = path.join(NGINX_CONF_PATH, fileName);

    if (!fs.existsSync(fullPath)) {
      throw new Error('Arquivo de configuração não encontrado');
    }

    return fs.readFileSync(fullPath, 'utf8');
  }

  /**
   * Salva um conteúdo customizado no arquivo de configuração
   */
  static async saveConfig(proxyId, content) {
    const fileName = `nesk_proxy_${proxyId}.conf`;
    const fullPath = path.join(NGINX_CONF_PATH, fileName);

    try {
      // 1. Salva o novo conteúdo
      fs.writeFileSync(fullPath, content);

      // 2. Valida a sintaxe do Nginx
      try {
        execSync(`${NGINX_BIN} -t`, { stdio: 'pipe' });
      } catch (testError) {
        throw new Error(`Erro na sintaxe do Nginx: ${testError.stderr?.toString() || testError.message}`);
      }

      // 3. Reload do Nginx
      execSync(`${NGINX_BIN} -s reload`, { stdio: 'pipe' });
      return true;
    } catch (error) {
      console.error(`[NGINX SAVE CONFIG ERROR] ${error.message}`);
      throw error;
    }
  }

  /**
   * Aplica a configuração do proxy no Nginx
   */
  static async applyConfig(proxy) {
    const fileName = `nesk_proxy_${proxy.id}.conf`;
    const fullPath = path.join(NGINX_CONF_PATH, fileName);

    try {
      // 1. Configuração inicial HTTP
      fs.writeFileSync(fullPath, this.generateConfig(proxy));
      execSync(`${NGINX_BIN} -s reload`, { stdio: 'pipe' });

      // 2. SSL
      const certPath = path.join(CERTBOT_PATH, proxy.domain);
      if (!fs.existsSync(path.join(certPath, 'fullchain.pem'))) {
        await this.issueCertificate(proxy.domain);
      }

      // 3. Configuração final (com HTTPS se disponível)
      fs.writeFileSync(fullPath, this.generateConfig(proxy));

      // 4. Validação
      try {
        execSync(`${NGINX_BIN} -t`, { stdio: 'pipe' });
      } catch (testError) {
        if (fs.existsSync(fullPath)) fs.unlinkSync(fullPath);
        throw new Error(`Nginx test failed: ${testError.stderr?.toString() || testError.message}`);
      }

      // 5. Reload final
      execSync(`${NGINX_BIN} -s reload`, { stdio: 'pipe' });
      return true;
    } catch (error) {
      console.error(`[NGINX SERVICE ERROR] ${error.message}`);
      throw error;
    }
  }

  /**
   * Remove a configuração e o certificado
   */
  static async removeConfig(proxyId, domain = null) {
    const fileName = `nesk_proxy_${proxyId}.conf`;
    const fullPath = path.join(NGINX_CONF_PATH, fileName);

    console.log(`[NGINX] Iniciando remoção do proxy ID: ${proxyId}`);

    try {
      if (fs.existsSync(fullPath)) {
        fs.unlinkSync(fullPath);
        console.log(`[NGINX] Arquivo de configuração removido: ${fileName}`);
      } else {
        console.log(`[NGINX] Arquivo de configuração não encontrado, pulando...`);
      }

      if (domain) {
        try {
          console.log(`[CERTBOT] Tentando remover certificado para: ${domain}`);
          // Adicionado --quiet para reduzir output desnecessário e garantir que erros apareçam no catch
          execSync(`sudo certbot delete --cert-name ${domain} --non-interactive`, { stdio: 'inherit' });
          console.log(`[CERTBOT] Certificado removido com sucesso para: ${domain}`);
        } catch (certError) {
          console.warn(`[CERTBOT WARNING] Não foi possível remover o certificado para ${domain}. Provavelmente ele não existe ou já foi removido.`);
        }
      }

      execSync(`${NGINX_BIN} -s reload`, { stdio: 'pipe' });
      console.log(`[NGINX] Nginx recarregado com sucesso.`);
      return true;
    } catch (error) {
      console.error(`[NGINX SERVICE ERROR] Erro crítico na remoção: ${error.message}`);
      throw error;
    }
  }
}

module.exports = NginxService;