# 🌐 Nesk Agent (C# Edition)

[![.NET Version](https://img.shields.io/badge/.NET-10.0-purple.svg?style=flat-square)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg?style=flat-square)](https://opensource.org/licenses/MIT)
[![Nginx](https://img.shields.io/badge/Nginx-Proxy-blue.svg?style=flat-square)](https://nginx.org/)
[![SSL](https://img.shields.io/badge/SSL-Certbot-orange.svg?style=flat-square)](https://certbot.eff.org/)

O **Nesk Agent** é uma solução robusta e de alta performance desenvolvida em **C# (.NET 10)**, projetada especificamente para o gerenciamento automatizado de infraestrutura de rede, incluindo proxies reversos Nginx e automação de certificados SSL via Certbot.

---

## 💎 Diferenciais Técnicos

*   **Alta Performance:** Engine reescrita em .NET 10 para execução nativa com baixíssimo overhead de memória e CPU.
*   **Arquitetura Single-Binary:** Compilado como um executável auto-contido para Linux ARM64, eliminando a dependência de runtimes externos na VPS.
*   **Gestão de Infraestrutura:** Automação completa de arquivos de configuração `.conf` do Nginx.
*   **SSL Nativo:** Ciclo de vida completo de certificados (emissão, renovação e remoção) integrado ao Certbot.
*   **Arquitetura Dual-Stack:** API de Gerenciamento (Porta 4000) e CDN de Alta Disponibilidade (Porta 4001) operando em um único processo.

---

## 🛠️ Requisitos do Sistema

Antes de iniciar, certifique-se de possuir as dependências necessárias instaladas em sua VPS Linux:

```bash
# Atualização de pacotes e instalação do Nginx + Certbot
sudo apt update && sudo apt install -y nginx certbot python3-certbot-nginx
```

---

## 🚀 Guia de Implantação (Deployment)

### 1. Preparação do Ambiente
Crie um diretório dedicado para o agente e mova o binário executável:
```bash
mkdir -p /opt/nesk-agent
mv NeskAgent /opt/nesk-agent/
cd /opt/nesk-agent/
chmod +x NeskAgent
```

### 2. Configuração de Variáveis de Ambiente
Crie um arquivo `.env` no diretório raiz do executável com as seguintes definições:

```ini
# Configurações de Rede
PORT=4000
CDN_PORT=4001

# Segurança
AGENT_API_KEY=sua_chave_secreta_aqui

# Banco de Dados (MySQL/MariaDB)
AGENT_DB_HOST=localhost
AGENT_DB_NAME=nesk_agent
AGENT_DB_USER=seu_usuario
AGENT_DB_PASS=sua_senha

# Caminhos do Sistema (Opcional)
NGINX_CONF_PATH=/etc/nginx/conf.d/
NGINX_BIN_PATH=/usr/sbin/nginx
```

---

## 🔌 Referência da API

Todas as chamadas de API (exceto endpoints de health check) devem incluir o cabeçalho de autenticação:
`Authorization: Bearer <AGENT_API_KEY>`

### 📂 Módulo de Proxy (Nginx)

| Método | Endpoint | Função |
| :--- | :--- | :--- |
| `GET` | `/health` | Status vital do serviço |
| `GET` | `/api/proxy` | Listagem de todos os hosts configurados |
| `POST` | `/api/proxy` | Provisionamento de novo host + SSL |
| `PUT` | `/api/proxy/:id` | Atualização de parâmetros de roteamento |
| `DELETE` | `/api/proxy/:id` | Descomissionamento de host e limpeza de SSL |
| `POST` | `/api/proxy/:id/enable` | Ativação imediata de configuração |
| `POST` | `/api/proxy/:id/disable` | Suspensão de serviço e remoção SSL |
| `GET` | `/api/proxy/:id/config` | Leitura de configuração bruta (.conf) |

### 📦 Módulo CDN (Content Delivery Network)

| Método | Endpoint | Função |
| :--- | :--- | :--- |
| `GET` | `/api/cdn/list` | Navegação na estrutura de arquivos |
| `POST` | `/api/cdn/folder` | Criação de diretórios estruturados |
| `POST` | `/api/cdn/upload` | Upload otimizado (Multipart/Chunked) |
| `DELETE` | `/api/cdn/item` | Remoção definitiva de ativos |

---

## � Monitoramento e Logs

O sistema utiliza um padrão de logs semânticos e minimalistas para facilitar o monitoramento via `journalctl` ou logs de container:

*   `[PROXY] Proxy criado: example.com` - Novo roteamento estabelecido.
*   `[CERTIFICADO] Certificado criado para example.com` - SSL emitido com sucesso.
*   `[CDN] Arquivo upado: asset_01.zip` - Novo ativo disponível na CDN.
*   `[PROXY] Proxy removido: old-site.com` - Roteamento e arquivos de configuração deletados.

---

## 📄 Licença

Distribuído sob a licença MIT. Veja `LICENSE` para mais informações.

**Desenvolvido por ByCronoz**
