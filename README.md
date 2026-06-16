# NeskAgent v3

> **Agente de gerenciamento de servidores em tempo real** com controle via WebSocket, telemetria integrada e gerenciamento automatizado de reverse proxy via Nginx.

## Índice

- [Visão Geral](#visão-geral)
- [Arquitetura: EXE vs DLL](#arquitetura-exe-vs-dll)
- [Estrutura do Projeto](#estrutura-do-projeto)
- [Como Funciona a Comunicação](#como-funciona-a-comunicação)
- [Ciclo de Vida do Agente](#ciclo-de-vida-do-agente)
- [Sistema de Plugins](#sistema-de-plugins)
- [Reverse Proxy e Nginx](#reverse-proxy-e-nginx)
- [Comandos e Ações Disponíveis](#comandos-e-ações-disponíveis)
- [Configuração](#configuração)
- [Build e Deploy](#build-e-deploy)
- [Tecnologias](#tecnologias)

---

## Visão Geral

O **NeskAgent** é um agente escrito em **C# / .NET 8** que roda nos seus servidores e se conecta via **WebSocket** a uma API Central. Ele permite que você controle remotamente sua infraestrutura — visual publicar/desligar sites, gerenciar SSL, coletar métricas de sistema e executar comandos shell — tudo através de uma única conexão WebSocket persistente.

```
┌─────────────────┐      WebSocket       ┌─────────────────┐
│   API Central   │  ◄──────────────►   │  NeskAgent      │
│  (Painel Web)   │                     │  (Seu Servidor) │
└─────────────────┘                     └─────────────────┘
                                                │
                              ┌─────────────────┼─────────────────┐
                              │                 │                 │
                        ┌─────────┐      ┌──────────┐   ┌──────────┐
                        │  Nginx  │      │  Shell   │   │ Métricas │
                        │ (Proxy) │      │ (Bash)   │   │ (CPU/RAM)│
                        └─────────┘      └──────────┘   └──────────┘
```

---

## Arquitetura: EXE vs DLL

Antes de entender o código, é importante entender a diferença entre o arquivo **.exe** (executável) e os arquivos **.dll** (bibliotecas) e como eles se relacionam no .NET.

### O que é um `.exe`?

> O **.exe** (no caso, `NeskAgent.exe`) é o **ponto de entrada** do seu programa. É o arquivo que você clica (ou executa no terminal) para iniciar o agente.

**Características:**
- É gerado pelo projeto `NeskAgent` (o projeto raiz com `<OutputType>Exe</OutputType>`)
- Tem o método `Main()`, o primeiro código a ser executado
- **Sozinho não faz nada** — ele carrega e executa o código que está nas DLLs

```
NeskAgent.exe       ←  Você executa este arquivo. Ele carrega as DLLs em memória
     │                 e inicia o agente WebSocket.
     │
     ├─► NeskAgent.Core.dll          (WebSocket, lógica principal)
     ├─► NeskAgent.Plugins.dll        (Shell, Telemetria)
     ├─► NeskAgent.Proxy.dll         (Nginx, SSL, Proxy)
     └─► NeskAgent.Command.dll        (Contratos, interfaces)
```

### O que é uma `.dll`?

> Um arquivo **.dll** (Dynamic Link Library) é uma **biblioteca de código** que não pode ser executada diretamente. Ela contém classes, métodos e funções que outros programas (como o `.exe`) podem usar.

**Características:**
- É **reutilizável** — múltiplos projetos podem usar a mesma DLL
- É **linkada** (carregada em memória) na hora da execução pelo .EXE
- Pode ser atualizada independentemente do .EXE (se a interface pública for mantida)

### Analogia Prática

Imagine que você tem um carro:

| Arquivo | Analogia |
|---------|----------|
| `NeskAgent.exe` | É o **motor de ignição** — você gira a chave e ele liga o carro. |
| `NeskAgent.Core.dll` | É o **sistema de direção** — sem ele, o carro não anda. |
| `NeskAgent.Proxy.dll` | É o **sistema de GPS** — te guia (gerencia Nginx). |
| `NeskAgent.Plugins.dll` | São os **acessórios** (radio, o ar-condicionado). |

Você **liga o carro pelo motor de ignição** (executa o .exe), e **ele usa os outros sistemas** (as .dlls) para funcionar.

### Como funciona fisicamente?

Quando você executa `NeskAgent.exe`:

1. O **CLR** (Common Language Runtime) do .NET verifica as dependências
2. As **DLLs necessárias são carregadas em memória**
3. O método `Main()` no `Program.cs` é executado
4. O `Program.cs` constroi o `AgentCore`, registra plugins e inicia鸦片 a conexão WebSocket
5. O agente agora **vive em memória** e processa comandos da API Central

---

## Estrutura do Projeto

A arquitetura é dividida em **camadas** (separation of concerns), cada uma em seu próprio projeto. Isso facilita manutenção, testes e evolução independente.

```
NeskAgent.sln                        (Solution file)
│
├── NeskAgent                          ← ⭐ Executável (.exe)
│   ├── Program.cs                     Ponto de entrada. Carrega .env, registra plugins, inicia AgentCore.
│   └── NeskAgent.csproj
│
├── NeskAgent.Core                     ← 🧠 Lógica Principal
│   ├── Services/AgentCore.cs          WebSocket client, reconnection, loop principal
│   └── NeskAgent.Core.csproj
│
├── NeskAgent.Command                 決めContratos compartilhados
│   ├── Interfaces/IAgentPlugin.cs     Interface que todo plugin deve implementar
│   ├── CommandRouter.cs              Encaminha comandos ao plugin correto
│   └── NeskAgent.Command.csproj     Models/CommandResult.cs
│
├── NeskAgent.Plugins                  ← 🛠 Plugins funcionalidades
│   ├── ShellPlugin.cs                 Executa comandos shell (bash/cmd)
│   ├── TelemetryPlugin.cs            Coleta métricas do sistema (CPU, RAM, disco)
│   └── NeskAgent.Plugins.csproj
│
└── NeskAgent.Proxy                    ← 🌐 Gerenciamento de Proxy
    ├── ProxyPlugin.cs                 Plugin que expõe comandos de proxy
    └── Services/Nginx/                Serviços de orquestração do Nginx
        ├── NginxService.cs            Orquestrador (controla os outros serviços)
        ├── NginxConfigGenerator.cs    Gera arquivos de configuração .conf
        ├── NginxConfigService.cs      Salva/carrega/le configurações
        ├── NginxProcessService.cs     Interage com o processo do Nginx
        └── NginxSslService.cs         Gera e instala certificados SSL
```

### Fluxo de Dependência

```
NeskAgent (EXE) ──► NeskAgent.Core
                        │
                        ├──► NeskAgent.Command  (contratos)
                        │
                        ├──► NeskAgent.Plugins   (Shell + Telemetria)
                        │
                        └──► NeskAgent.Proxy     (Nginx Proxy)
```

> **Transitive References:** `.Plugins` e `.Proxy` dependem de `.Command` **indiretamente** através do `.Core`. Em .NET, referências transitivas funcionam automaticamente.

---

## Como Funciona a Comunicação

O NeskAgent usa **WebSocket** para manter uma conexão persistente, full-duplex e em tempo real com a API Central.

### Por que WebSocket?

| Protocolo | Problema |
|-----------|----------|
| **HTTP REST** | O servidor precisaria perguntar (polling) a cada segundo. Ineficiente. |
| **SSE** | O servidor pode **enviar**, mas o cliente só **recebe** (half-duplex) |
| **WebSocket** | **Full-duplex** — ambos podem mandar e receber a qualquer momento. Ideal. |

### Ciclo da Conexão

```
Agente                              API Central
  │                                     │
  │  1. WebSocket Handshake (HTTP 101)  │
  │ ─────────────────────────────────► │
  │                                     │
  │  2. Heartbeat (Ping/Pong)           │
  │  <──── a cada 30s ---->           │
  │                                     │
  │  3. API Central envia comandos JSON │
  │  <───────────────── {"action":"..."}│
  │                                     │
  │  4. Executa e responde             │
  │  ─────────────────> {"success": true}│
  │                                     │
  │  5. Telemetria enviada automatica  │
  │  ───────────> {"cpu": 45%, ...}    │
```

### Formato dos Comandos

Comando enviado pela API Central:
```json
{
  "action": "update_proxy",
  "requestId": "uuid-123",
  "data": {
    "proxyId": "web-01",
    "domain": "site.com",
    "targetHost": "localhost",
    "targetPort": 8080,
    "enabled": true,
    "ssl": true
  }
}
```

Resposta do agente:
```json
{
  "requestId": "uuid-123",
  "kind": "success",
  "stdout": "Proxy atualizado com sucesso.",
  "stderr": null
}
```

---

## Ciclo de Vida do Agente

O `AgentCore` é o coração do NeskAgent. Ele gerencia a conexão WebSocket e a reconnection automática.

### Diagrama de Estado

```
      ┌─────────────────────────────────────┐
      │           CRIAÇÃO                  │
      └───────────────┬─────────────────────┘
                      │
                      ▼
          ┌─────────────────────┐
          │   AgentCore ctor    │
          │ (guarda configs)    │
          └──────────┬──────────┘
                     │
                     ▼
      ┌────────────────────────────┐
      │        RunAsync()          │
      │  Loop principal (while)    │
      └─────────────┬──────────────┘
                    │
          ┌─────────┴──────────┐
          │                    │
          ▼                    ▼
 ┌──────────────┐    ┌──────────────────┐
 │ Conectando   │    │   ERROR          │
 │ ├─> Handshake│    │ catch {          │
 │ ├─> Ping/Pong│    │  esperar 5 retries│
 │ └─> Autentica│    │  c/ exponencial  │
 └──────┬───────┘    │  e reconecta     │
        │            └──────────────────┘
        │
        ▼
 ┌──────────────┐
 │ Receiving    │  ← Loop de recebimento
 │ Loop         │    Processa comandos    │
 └──────────────┘    e enfileRadar
                     resultados pendentes
        │
        ▼
 ┌──────────────┐
 │  DISPOSE   │  ← Cancela tokens, fecha
 │  (shutdown)│    websocket, libera recursos
 └──────────────┘
```

### Resilience (T.Package para Reconnect)

O `AgentCore` implementa um sistema de resiliência:

1. **Reconnection automática**: Quando a conexão cai (erro de rede, reinício da API), o agente espera um delay crescente (backoff exponencial) e tenta reconectar.
2. **Queue de Resultados Pendentes**: Se um comando é processado mas a resposta não pode ser enviada (WebSocket desconectado), o resultado é enfileirado. Quando a reconexão ocorre, todos os resultados pendentes são flushados automaticamente.
3. **TTL (Time To Live)**: Resultados pendentes expiram em 5 minutos. A fila tem limite de 50 itens para não vazar memória.
4. **CancellationTokenSource**: Shutdown limpo — cancela todas as operações pendentes e fecha o socket corretamente.

---

## Sistema de Plugins

Plugins são o mecanismo de extensão do NeskAgent. Eles implementam a interface `IAgentPlugin` e registram quais "ações" eles suportam.

### Interface IAgentPlugin

```csharp
public interface IAgentPlugin
{
    // Quais ações este plugin pode processar?
    IReadOnlySet<string> SupportedActions { get; }

    // Método que processa o comando
    Task<CommandResult> ExecuteAsync(JsonDocument command, CancellationToken ct);
}
```

### CommandRouter: O Maestro

O `CommandRouter` é um dicionário que mapeia `action -> plugin`:

```csharp
router.RegisterPlugin(new TelemetryPlugin());  // action: "request_telemetry"
router.RegisterPlugin(new ShellPlugin(true));    // action: "shell_execute"
router.RegisterPlugin(new ProxyPlugin(nginx));     // actions: "update_proxy", "get_config", ...
```

Quando um comando chega via WebSocket, o `AgentCore` chama:
```csharp
var result = await _router.RouteAsync(commandJson, cancellationToken);
```

O router verifica a propriedade `action` do JSON e entrega ao plugin correto.

### Plugins Implementados

| Plugin | Ação | Descrição |
|--------|------|-----------|
| **TelemetryPlugin** | `request_telemetry` | Retorna CPU, RAM, disco, uptime e info do SO |
| **ShellPlugin** | `shell_execute` | Executa um comando no bash do servidor |
| **ProxyPlugin** | `update_proxy` | Configura um site no Nginx |
| **ProxyPlugin** | `delete_proxy` | Remove um site do Nginx |
| **ProxyPlugin** | `toggle_config` | Liga/desliga configuração |
| **ProxyPlugin** | `get_config` | Retorna configuração gerada |
| **ProxyPlugin** | `generate_ssl` | Gera e instala certificado SSL |

### Como adicionar um novo Plugin?

1. Crie uma classe que herda de `IAgentPlugin`
2. Implemente `SupportedActions` com as ações que seu plugin responde
3. Implemente `ExecuteAsync(...)` com a lógica
4. Registre no `Program.cs`:

```csharp
router.RegisterPlugin(new MeuNovoPlugin());
```

---

## Reverse Proxy e Nginx

O NeskAgent pode gerenciar o **Nginx** como reverse proxy, permitindo que você publique/desligue sites remotamente.

### O que é um Reverse Proxy?

> Um **reverse proxy** é um servidor que recebe requisições de clientes e as repassa para servidores backend, retornando as respostas ao cliente. O Nginx é o mais popular do mundo.

```
Usuário             Nginx (80/443)                   Aplicação
   │                       │                             │
   │   site.com    ──►     │  ──► server localhost:8080  │
   │                       │    (proxy_pass)             │
```

### Serviços Nginx no NeskAgent

| Serviço | Responsabilidade |
|---------|------------------|
| **NginxService** | Orquestrador principal. Coordena os outros serviços com `SemaphoreSlim` para evitar race conditions. |
| **NginxConfigGenerator** | Gera o texto do arquivo `.conf`. Suporta 3 modos: `HTTP`, `HTTPS` e `Disabled`. |
| **NginxConfigService** | Salva o arquivo gerado no disco, carrega e lista orphans. |
| **NginxProcessService** | Interage com o processo `nginx` (reload, test, start, stop). |
| **NginxSslService** | Gera e instala certificados SSL (`certbot` / `acme.sh`). |

### Modos de Configuração

```
HTTP     → Porta 80,   sem SSL
HTTPS    → Porta 443, com SSL / redireciona 80→443
Disabled → Comentada, site offline
```

### Exemplo de uso

**Comando via API Central:**
```json
{
  "action": "update_proxy",
  "requestId": "req-123",
  "data": {
    "proxyId": "web-01",
    "domain": "meusite.com",
    "targetHost": "localhost",
    "targetPort": 8080,
    "enabled": true,
    "ssl": false
  }
}
```

**Resultado no servidor:**
```nginx
# /etc/nginx/conf.d/web-01.meusite.com.conf
server {
    listen 80;
    server_name meusite.com;

    location / {
        proxy_pass http://localhost:8080;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
    }
}
```

---

## Comandos e Ações Disponíveis

Lista completa de ações que a API Central pode enviar ao agente:

### Telemetria
| Ação | Retorno |
|------|---------|
| `request_telemetry` | JSON com CPU, RAM, disco, uptime, SO |

### Shell
| Ação | Parâmetros | Observação |
|------|-----------|------------|
| `shell_execute` | `command`, `args` | Apenas se `SHELL_ENABLED=true` no .env |

### Reverse Proxy
| Ação | Parâmetros | Resultado |
|------|-----------|-----------|
| `update_proxy` | `proxyId`, `domain`, `targetHost`, `targetPort`, `enabled`, `ssl` | Atualiza ou cria proxy |
| `delete_proxy` | `proxyId`, `domain` | Remove config do Nginx |
| `toggle_config` | `domain`, `mode` (http/https/disabled) | Altera modo |
| `get_config` | `domain` | Retorna texto da configuração |
| `generate_ssl` | `domain`, `email` | Gera certificado SSL |

---

## Configuração

### Arquivo `.env`

Copie `.env.example` para `.env` e ajuste:

```bash
# Identificação
AGENT_ID=nortlin-sp-01
AGENT_NAME=nortlin-01

# Endpoint WebSocket da API Central
API_CENTRAL_URL=wss://agent.nesk.fun/ws

# Segurança — habilita/desabilita comandos shell
SHELL_ENABLED=false

# Configurações de proxy (exemplo)
NGINX_CONF_DIR=/etc/nginx/conf.d
NGINX_BIN=/usr/sbin/nginx
```

### Variáveis Importantes

| Variável | Descrição | Padrão |
|----------|-----------|--------|
| `AGENT_ID` | ID único do agente (ex: hostname) | `default-agent-id` |
| `AGENT_NAME` | Nome humano-legível do agente | `NeskAgent` |
| `API_CENTRAL_URL` | URL WebSocket do servidor central | `wss://agent.nesk.fun/ws` |
| `SHELL_ENABLED` | Permite execução de comandos shell? | `false` |

---

## Build e Deploy

### Requisitos

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Windows 10/11 ou Linux (x64/arm64)

### Build de Desenvolvimento

```bash
# Compila tudo
$ dotnet build

# Executa local
$ cd NeskAgent
$ dotnet run
```

### Build de Produção (Build Script)

O projeto inclui `build.bat` para builds automatizados por plataforma:

```bash
$ build.bat

# Escolha no menu:
# [1] Windows (win-x64)          → gera NeskAgent.exe
# [2] Linux (linux-x64)          → gera NeskAgent (binary ELF)
# [3] Linux ARM (linux-arm64)   → gera NeskAgent (Raspberry Pi)
# [4] Todas as plataformas
```

### PublishSingleFile

Com a flag `-p:PublishSingleFile=true`, o build gera um **único arquivo .exe** que embute todas as DLLs internamente. É ideal para distribuição:

```
# Antes do PublishSingleFile:
├── NeskAgent.exe
├── NeskAgent.Core.dll
├── NeskAgent.Plugins.dll
├── NeskAgent.Proxy.dll
└── NeskAgent.Command.dll

# Depois do PublishSingleFile:
└── NeskAgent.exe  ← arquivo único
```

O .NET extrai as DLLs para um diretório temporário em memória e executa tudo automaticamente.

---

## Tecnologias

| Tecnologia | Uso |
|-----------|-----|
| **.NET 8.0** | Framework principal (C#) |
| **WebSocket (System.Net.WebSockets)** | Comunicação persistente com a API |
| **System.Text.Json** | Serialização/deserialização de comandos |
| **Nginx** | Reverse proxy HTTP/HTTPS |
| **DotNetEnv** | Leitura de variáveis de ambiente do `.env` |

---

## Diagrama de Sequência: Atualizando um Proxy

```
API Central                  NeskAgent                 Servidor
     │                          │                         │
     │  {"action":"update_proxy"}│                         │
     ├─────────────────────────>│                         │
     │                          │                         │
     │                          │  NginxConfigGenerator   │
     │                          │  .Generate("site.com")  │
     │                          ├─────────────────────────>│
     │                          │  (retorna config texto)  |
     │                          │<─────────────────────────│
     │                          │                         │
     │                          │  NginxConfigService     │
     │                          │  .SaveConfig(texto)      │
     │                          ├─────────────────────────>│
     │                          │  (escreve no disco)      │
     │                          │<─────────────────────────│
     │                          │                         │
     │                          │  NginxProcessService     │
     │                          │  .Reload()               │
     │                          ├─────────────────────────>│
     │                          │  (nginx -s reload)       │
     │                          │<─────────────────────────│
     │                          │                         │
     │  {"success": true}       │                         │
     │<─────────────────────────│                         │
```

---

## Licença

[MIT License](LICENSE)

---

> **Desenvolvido por ByCronoz de NeskCore.**
