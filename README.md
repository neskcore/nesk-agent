# 🚀 Nesk Agent

[![Node.js Version](https://img.shields.io/badge/node-%3E%3D14.0.0-brightgreen.svg)](https://nodejs.org/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![Nginx](https://img.shields.io/badge/Nginx-Proxy-blue.svg)](https://nginx.org/)
[![SSL](https://img.shields.io/badge/SSL-Certbot-orange.svg)](https://certbot.eff.org/)

O **Nesk Agent** é um microserviço robusto projetado para automatizar o gerenciamento de proxies reversos Nginx e a emissão de certificados SSL via Certbot. Ideal para sistemas que precisam criar e gerenciar domínios dinamicamente em uma VPS.

## 🛠️ Funcionalidades

- ✅ **Gerenciamento de Proxy:** Criação, atualização, ativação e remoção de configurações Nginx.
- ✅ **SSL Automático:** Integração nativa com Certbot para emissão e renovação de certificados.
- ✅ **CDN Integrada:** Gerenciamento de arquivos e pastas para distribuição de conteúdo estático.
- ✅ **Limpeza Inteligente:** Remoção automática de certificados SSL ao deletar um proxy para evitar acúmulo.
- ✅ **Suporte a WebSocket:** Configuração pré-otimizada para aplicações que utilizam WebSockets.
- ✅ **Arquitetura Modular:** Código organizado em Services, Controllers e Middlewares.

## 🚀 Instalação

1. Clone o repositório:
   ```bash
   git clone https://github.com/seu-usuario/nesk-agent.git
   cd nesk-agent
   ```

2. Instale as dependências:
   ```bash
   npm install
   ```

3. Configure o arquivo `.env`:
   ```env
   PORT=4000
   AGENT_API_KEY=sua_chave_secreta_aqui
   DB_HOST=localhost
   DB_USER=root
   DB_PASS=sua_senha
   DB_NAME=nesk_finance
   NGINX_CONF_PATH=/etc/nginx/conf.d/
   NGINX_BIN_PATH=nginx
   ```

4. Inicie o servidor:
   ```bash
   npm start
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

## 🛡️ Requisitos da VPS

Para o funcionamento pleno, a VPS deve ter instalado:
- **Node.js** (v14+)
- **Nginx**
- **Certbot** com plugin Nginx:
  ```bash
  sudo apt update
  sudo apt install certbot python3-certbot-nginx -y
  ```

## 📄 Licença

Este projeto está sob a licença MIT. Veja o arquivo [LICENSE](LICENSE) para mais detalhes.

---
Desenvolvido com ❤️ por ByCronoz
