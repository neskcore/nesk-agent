# NeskAgent V3 — Plano de Implementação (Final)

> **Escopo:** Apenas o Agent (C#). Sem refatoração de API ou Painel Desktop.
> **Prioridade:** Proxy Reverso. CDN fica para depois.
> **Base de referência:** `nesk-agentV2/`

---

## 1. Inventário Completo de Funcionalidades do Proxy na V2

### 1.1 Comandos WebSocket que o Agent Processa

| Comando | Origem | O que faz |
|---|---|---|
| `update_proxy` | API (`POST/PUT /:agent_id/config`) | Gera arquivo `.conf` do Nginx com `domain`, `target_host`, `target_port`, `enabled`. Se `enabled=false`, gera config apontando para página HTML de "Proxy desativado". |
| `get_config` | API (`GET /:agent_id/config/:filename`) | Lê o conteúdo raw do `.conf` pelo `proxy_id` ou `domain`. Retorna via WS com `type: config_content` e `request_id`. |
| `save_config` | API (`POST /:agent_id/config/:filename`) | Escreve conteúdo raw diretamente no arquivo `.conf` (edição manual do Nginx) e faz `nginx -s reload`. **Observação:** este é o único comando que pode ser fire-and-forget (sem `request_id` obrigatório); se não houver `request_id`, o resultado não é rastreado pela API. |
| `delete_proxy` | API (`DELETE /:agent_id/config/:proxy_id`) | Deleta o `.conf` + backup `.bak` + certificado SSL (via `certbot delete`) e faz reload. |
| `toggle_config` | API (`POST /:agent_id/config/:proxy_id/toggle`) | Ativa/desativa proxy. Desativar = salva backup do `.conf` original → substitui por config que serve `nesk_deactivated.html`. Ativar = restaura backup. **Importante:** deve usar o mesmo `NginxConfigGenerator` que o `update_proxy` para garantir paridade de template. |
| `generate_ssl` | API (`POST /ssl/:agent_id/generate`) | Roda `certbot --nginx -d {domain}` para emitir certificado SSL. Opera no modo `Kind.Async` — responde imediatamente com ack e envia push de conclusão via `async_result`. |
| `save_ssl_files` | API (`POST /ssl/:agent_id/upload`) | Salva arquivos `.pem` de certificado SSL manualmente enviados. |
| `shell_execute` | API (`POST /:agent_id/shell`) | Executa comando shell arbitrário na VPS. Controlado por `SHELL_ENABLED` no `.env`. |
| `request_telemetry` | API | Envia métricas do sistema (CPU, RAM, Disk, Uptime, Latência, OS). |

### 1.2 Sistema de Ativação/Desativação de Proxy

Quando um proxy é **desativado** (`enabled = false`):

1. O `.conf` original (com `proxy_pass`) é salvo como backup (`.conf.bak`)
2. Um novo `.conf` é gerado apontando para `/var/www/html/nesk_deactivated.html`
3. A página HTML (EmbeddedResource na DLL) exibe:
   - "Proxy desativado" com Ray ID, timestamp UTC
   - Diagrama visual: Browser ✓ → Nesk Agent ✓ → Host ✗ (OFF)
   - Explicação do que aconteceu e o que o visitante pode fazer
   - Footer "Powered by Nortlin Studios"
4. Se tiver SSL, gera também bloco `server 443` com o certificado servindo a mesma página
5. Nginx é recarregado

Quando é **reativado** (`enabled = true`):

1. O backup `.conf.bak` é restaurado sobre o `.conf`
2. O `.bak` é deletado
3. Nginx é recarregado
4. Se não houver backup, gera config novo do zero via `NginxConfigGenerator`

### 1.3 Geração de Config Nginx (NginxConfigGenerator)

3 modos de geração, todos passando pelo mesmo método centralizado:

| Modo | Condição | Resultado |
|---|---|---|
| **HTTP Only** | `enabled=true`, sem certificado SSL | `listen 80` → `proxy_pass http://host:port` com headers + WebSocket |
| **HTTPS + Redirect** | `enabled=true`, com certificado em `/etc/letsencrypt/live/{domain}/` | `listen 80` redireciona 301 para HTTPS. `listen 443 ssl http2` com otimizações SSL, proxy_pass + WebSocket |
| **Desativado** | `enabled=false` | `listen 80` (e 443 se tiver cert) servindo `nesk_deactivated.html` |

Headers incluídos em todos os configs ativos:
```nginx
proxy_set_header Host $host;
proxy_set_header X-Real-IP $remote_addr;
proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
proxy_set_header X-Forwarded-Proto $scheme;

# Suporte a WebSockets
proxy_http_version 1.1;
proxy_set_header Upgrade $http_upgrade;
proxy_set_header Connection "upgrade";
```

### 1.4 Serviços Internos do Proxy (Nginx/)

| Classe | Responsabilidade |
|---|---|
| `NginxService` | Fachada thread-safe (tudo passa por `SemaphoreSlim`). Métodos: `SaveConfig`, `DeleteConfig`, `GetRawConfig`, `WriteRawConfig`, `ListConfigs`, `FindConfigByDomain`, `IssueSsl`, `DeleteSsl`, `Reload` |
| `NginxConfigService` | Operações de arquivo: escrita/leitura/deleção de `.conf`, backup/restore `.bak`, extração do HTML de desativação do EmbeddedResource, cleanup de orphan configs (SSL ausente), permissões `chmod 644` |
| `NginxConfigGenerator` | Gera o conteúdo textual do `.conf` conforme os 3 modos acima. Usado por `update_proxy` E `toggle_config` — nunca duplicar templates |
| `NginxProcessService` | Executa `nginx -t` (teste) e `nginx -s reload`. Se falhar por certificado órfão, lança `NginxOrphanConfigException` → cleanup automático → retry → emite evento `orphan_cleanup` auditável |
| `NginxSslService` | Executa `certbot --nginx` (emitir SSL) e `certbot delete` (revogar SSL) de forma assíncrona |
| `NginxOrphanConfigException` | Exception custom para configs que referenciam certificados que não existem mais |

---

## 2. Nova Arquitetura V3

```text
NeskAgent/                          ← Executável principal
├── NeskAgent.csproj
├── Program.cs
├── .env
│
├── NeskAgent.Core/                 ← Núcleo de Conexão
│   ├── NeskAgent.Core.csproj
│   └── Services/
│       └── AgentCore.cs            ← WebSocket, reconexão, fila de CommandResults pendentes
│
├── NeskAgent.Command/              ← Roteador e contrato de comandos
│   ├── NeskAgent.Command.csproj
│   ├── Interfaces/
│   │   └── IAgentPlugin.cs         ← SupportedActions + ExecuteAsync → CommandResult
│   ├── Models/
│   │   └── CommandResult.cs        ← Record tipado (ver seção 3)
│   └── CommandRouter.cs            ← Valida request_id e roteia para o plugin correto
│
├── NeskAgent.Plugins/              ← Plugins nativos (projeto separado)
│   ├── NeskAgent.Plugins.csproj
│   ├── TelemetryPlugin.cs          ← Responde a 'request_telemetry' + timer de push periódico
│   └── ShellPlugin.cs              ← Responde a 'shell_execute' (desativável via SHELL_ENABLED)
│
└── NeskAgent.Proxy/                ← Plugin de Proxy (DLL separada)
    ├── NeskAgent.Proxy.csproj
    ├── ProxyPlugin.cs              ← Implementa IAgentPlugin
    ├── Resources/
    │   └── nesk_deactivated.html   ← EmbeddedResource (não depende do FileSystem)
    └── Services/
        └── Nginx/
            ├── NginxService.cs
            ├── NginxConfigService.cs
            ├── NginxConfigGenerator.cs
            ├── NginxProcessService.cs
            ├── NginxSslService.cs
            └── Exceptions/
                └── NginxOrphanConfigException.cs
```

### Fluxo dos comandos na V3

1. `AgentCore` recebe a mensagem via WebSocket (buffer dinâmico via `MemoryStream`)
2. `AgentCore` repassa o JSON para o `CommandRouter`
3. `CommandRouter` valida presença do `request_id` (exceto `save_config`) e localiza o plugin pelo campo `action`
4. Plugin executa e devolve um `CommandResult` padronizado
5. `AgentCore` serializa e envia de volta pela conexão WS
6. Se a conexão WS cair antes do envio, o resultado entra na **fila em memória** com timestamp de criação. Ao reconectar, a fila é flushed descartando itens com mais de **5 minutos** (TTL) ou acima de **50 itens** (limite de tamanho — descarta os mais antigos)

---

## 3. Contrato de Tipos

### CommandResult

```csharp
public record CommandResult(
    bool Success,
    string? Message,
    string? Payload,        // conteúdo raw para get_config, etc.
    CommandResultKind Kind
);

public enum CommandResultKind
{
    Ack,      // confirmação simples de execução
    Content,  // resultado com dados (Payload preenchido)
    Async,    // resposta imediata; push de conclusão virá depois via async_result
    Error     // falha com mensagem de erro
}
```

### IAgentPlugin

```csharp
public interface IAgentPlugin
{
    IReadOnlySet<string> SupportedActions { get; }
    Task<CommandResult> ExecuteAsync(JsonDocument command, CancellationToken ct);
}
```

---

## 4. Problemas da V2 Resolvidos

| Problema | Solução na V3 |
|---|---|
| Comandos perdidos silenciosamente | `CommandRouter` exige retorno de `CommandResult`; sem resposta = erro explícito |
| Mensagens longas cortadas no WS | Buffer dinâmico via `MemoryStream` no `AgentCore` |
| Timeout por falta de `request_id` | `CommandRouter` valida e sempre devolve o `request_id` intacto |
| Acoplamento Core/Proxy | `AgentCore` não referencia nenhum plugin diretamente |
| `generate_ssl` bloqueando o canal WS | `Kind.Async` — ack imediato + push de conclusão via `async_result` |
| Limpeza silenciosa de configs órfãos | Evento `orphan_cleanup` emitido com `proxy_id` + `domain` para rastreabilidade |
| Página "desativado" dependente do FileSystem | `nesk_deactivated.html` como `EmbeddedResource` na DLL do Proxy |
| `shell_execute` sem controle de segurança | `ShellPlugin` desativável via `SHELL_ENABLED=false` no `.env` |
| Resultados perdidos durante reconexão | Fila em memória com TTL de 5min e limite de 50 itens |
| Divergência de template entre `update_proxy` e `toggle_config` | Ambos obrigatoriamente passam pelo mesmo `NginxConfigGenerator` |

---

## 5. Protocolo de Mensagens WS (Referência Completa)

### API → Agent (comandos)

```json
{ "action": "update_proxy",  "request_id": "uuid", "id": "proxy_uuid", "domain": "app.nesk.fun", "target_host": "127.0.0.1", "target_port": 3000, "enabled": true }
{ "action": "delete_proxy",  "request_id": "uuid", "id": "proxy_uuid" }
{ "action": "get_config",    "request_id": "uuid", "id": "proxy_uuid" }
{ "action": "save_config",   "request_id": "uuid", "filename": "nesk_proxy_uuid.conf", "content": "server { ... }" }
{ "action": "toggle_config", "request_id": "uuid", "id": "proxy_uuid", "domain": "app.nesk.fun", "target_host": "127.0.0.1", "target_port": 3000, "active": false }
{ "action": "generate_ssl",  "request_id": "uuid", "domain": "app.nesk.fun" }
{ "action": "save_ssl_files","request_id": "uuid", "domain": "app.nesk.fun", "cert": "...", "key": "..." }
{ "action": "shell_execute", "request_id": "uuid", "command": "df -h" }
{ "action": "request_telemetry", "request_id": "uuid" }
```

> **Nota:** `save_config` passa a aceitar `request_id` opcionalmente para consistência, mas pode operar sem ele.

### Agent → API (respostas)

```json
// Confirmação simples (Kind.Ack)
{ "type": "command_result", "request_id": "uuid", "command": "update_proxy", "success": true, "message": "Proxy atualizado" }

// Conteúdo de config (Kind.Content)
{ "type": "command_result", "request_id": "uuid", "command": "get_config", "success": true, "payload": "server { ... }" }

// Ack imediato para tarefa assíncrona (Kind.Async)
{ "type": "command_result", "request_id": "uuid", "command": "generate_ssl", "success": true, "message": "Certbot iniciado para app.nesk.fun" }

// Push de conclusão da tarefa assíncrona
{ "type": "async_result", "request_id": "uuid", "command": "generate_ssl", "success": true, "message": "Certificado emitido com sucesso para app.nesk.fun" }

// Erro (Kind.Error)
{ "type": "command_result", "request_id": "uuid", "command": "update_proxy", "success": false, "message": "Nginx reload falhou: porta 80 em uso" }

// Evento de limpeza órfã (sem request_id — evento assíncrono espontâneo)
{ "type": "orphan_cleanup", "proxy_id": "proxy_uuid", "domain": "app.nesk.fun", "message": "Arquivo .conf deletado: certificado SSL ausente" }

// Telemetria (push periódico ou resposta a request_telemetry)
{ "type": "telemetry", "agent_id": "...", "timestamp": "ISO8601", "data": { "cpu_usage": 12.5, "ram_used_mb": 1024, "disk_used_gb": 40.2, "uptime_seconds": 86400, "latency_ms": 4, "os": "Ubuntu 22.04" } }
```

---

## 6. Ordem de Implementação

### Passo 1 — Estrutura Base
- Criar solução `.sln` do zero
- Criar projetos: `NeskAgent`, `NeskAgent.Core`, `NeskAgent.Command`, `NeskAgent.Plugins`, `NeskAgent.Proxy`
- Configurar referências entre projetos e `.env` base

### Passo 2 — Contrato e Roteador (`NeskAgent.Command`)
- Definir `IAgentPlugin` e `CommandResult` / `CommandResultKind`
- Implementar `CommandRouter` com registro dinâmico de plugins e validação de `request_id`

### Passo 3 — Core Resiliente (`NeskAgent.Core`)
- Implementar `AgentCore` com WebSocket, lógica de reconexão exponencial e fila em memória (TTL 5min, limite 50)
- Integrar `CommandRouter` no loop de recebimento com buffer dinâmico `MemoryStream`

### Passo 4 — Plugins Nativos (`NeskAgent.Plugins`)
- Implementar `TelemetryPlugin`: responde a `request_telemetry` + timer de push periódico configurável
- Implementar `ShellPlugin`: executa comandos shell, verifica `SHELL_ENABLED` antes de qualquer execução

### Passo 5 — Plugin do Proxy (`NeskAgent.Proxy`)
- Criar `NginxConfigGenerator` com os 3 modos (HTTP, HTTPS, Desativado)
- Implementar `NginxConfigService`, `NginxProcessService`, `NginxSslService`, `NginxService` (fachada)
- Embutir `nesk_deactivated.html` como `EmbeddedResource`
- Implementar `NginxOrphanConfigException` + emissão do evento `orphan_cleanup`
- Implementar `ProxyPlugin` usando `Kind.Async` para `generate_ssl`
- Garantir que `toggle_config` reutiliza `NginxConfigGenerator` (nunca duplicar template)