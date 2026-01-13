# 🚀 Nesk Agent (C# Edition)

[![.NET Version](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![Nginx](https://img.shields.io/badge/Nginx-Proxy-blue.svg)](https://nginx.org/)
[![SSL](https://img.shields.io/badge/SSL-Certbot-orange.svg)](https://certbot.eff.org/)

O **Nesk Agent** é um microserviço de alta performance desenvolvido em C# (.NET 10) projetado para automatizar o gerenciamento de proxies reversos Nginx e a emissão de certificados SSL via Certbot. Esta versão foi otimizada para ser executada como um binário único e nativo em sistemas Linux ARM64 (VPS).

## 🛠️ Funcionalidades

- ✅ **Performance Superior:** Reescrito em C# para menor consumo de recursos e maior velocidade.
- ✅ **Binário Único:** Executável auto-contido, sem necessidade de instalar o runtime do .NET na VPS.
- ✅ **Gerenciamento de Proxy:** Criação, atualização, ativação e remoção de configurações Nginx.
- ✅ **SSL Automático:** Integração nativa com Certbot para emissão e renovação de certificados.
- ✅ **Dual-Port:** API na porta 4000 e CDN estática na porta 4001 integradas no mesmo processo.
- ✅ **CDN Integrada:** Gerenciamento de arquivos e pastas para distribuição de conteúdo estático.
- ✅ **Segurança:** Autenticação via API Key (Bearer Token).

## 🚀 Instalação na VPS (Linux ARM64)

1. **Preparação:**
   Certifique-se de que o Nginx e Certbot estão instalados:
   ```bash
   sudo apt update
   sudo apt install nginx certbot python3-certbot-nginx -y
   ```

2. **Deploy:**
   - Copie o executável `NeskAgent` para a sua pasta na VPS (ex: `/root/apis/neskagent/`).
   - Crie um arquivo `.env` na mesma pasta do executável.

3. **Configuração do `.env`:**
   ```env
   PORT=4000
   CDN_PORT=4001
   AGENT_API_KEY=sua_chave_secreta_aqui
   AGENT_DB_HOST=seu_ip_mysql
   AGENT_DB_NAME=nesk_agent
   AGENT_DB_USER=seu_usuario
   AGENT_DB_PASS=sua_senha
   NGINX_CONF_PATH=/etc/nginx/conf.d/
   NGINX_BIN_PATH=/usr/sbin/nginx
   ```

4. **Execução:**
   ```bash
   chmod +x NeskAgent
   ./NeskAgent
   ```

## 🔌 API Endpoints

Todas as requisições (exceto `/health`) requerem o header:
`Authorization: Bearer <AGENT_API_KEY>`

| Método | Rota | Descrição |
| :--- | :--- | :--- |
| `GET` | `/health` | Verifica o status do agente |
| `GET` | `/api/proxy` | Lista todos os proxies |
| `POST` | `/api/proxy` | Cria um novo proxy |
| `PUT` | `/api/proxy/:id` | Atualiza um proxy existente |
| `DELETE` | `/api/proxy/:id` | Remove um proxy e seu certificado SSL |
| `POST` | `/api/proxy/:id/enable` | Ativa um proxy no Nginx |
| `POST` | `/api/proxy/:id/disable` | Desativa um proxy e remove o SSL |
| `GET` | `/api/cdn/list` | Lista arquivos e pastas da CDN |
| `POST` | `/api/cdn/folder` | Cria uma nova pasta na CDN |
| `POST` | `/api/cdn/upload` | Faz upload de arquivo para a CDN |
| `DELETE` | `/api/cdn/item` | Remove um arquivo ou pasta da CDN |

---
Desenvolvido com ❤️ por ByCronoz
