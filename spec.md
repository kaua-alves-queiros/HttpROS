# HttpROS - Http Router Operating System

HttpROS é um wrapper de alto nível para o Nginx, projetado para oferecer uma experiência de configuração via CLI inspirada em sistemas operacionais de rede (Datacom, Huawei, Cisco). O objetivo é gerenciar rotas HTTP, certificados SSL, e segurança de forma declarativa e interativa.

## Princípios de Design
1.  **Hierarquia de Modos**: A configuração é feita navegando entre contextos (Global -> Rota -> Módulo).
2.  **Verbosidade Explícita**: O comando `show` exibe tanto o que está ativo quanto o que está explicitamente desativado (`no <cmd>`).
3.  **Segurança de Configuração**: Validação de conflitos de domínio e precedência de módulos (Balancer > Target).

---

## Regras de Conflito e Precedência
1.  **Domínios Únicos**: Um domínio só pode pertencer a um tipo de rota (`proxy`, `static` ou `redirect`).
2.  **Precedência de Proxy**: Se houver um `balancer` configurado com upstreams, ele anula o `target` único.
3.  **Negação Universal**: O prefixo `no` remove ou desativa qualquer funcionalidade em qualquer nível.

---

## Estrutura de Modos (CLI Hierarchy)

### 1. View Mode (`HttpROS>`)
Modo operacional inicial para monitoramento e entrada em configuração.
- `show routes`: Resumo de todas as rotas.
- `show status`: Saúde do sistema e processos Nginx.
- `configure`: Entra no modo de configuração global.

### 2. Config Mode (`HttpROS(config)#`)
Onde rotas são criadas, deletadas ou selecionadas.
- `proxy <domain>`: Cria/Edita rota de proxy.
- `static <domain>`: Cria/Edita rota estática.
- `redirect <domain>`: Cria/Edita rota de redirecionamento.
- `backup`: Gera backup de todos os JSONs.
- `top`: Retorna ao View Mode.

### 3. Route-Config Mode (`HttpROS(config-route-xxx)#`)
Configurações específicas da rota selecionada.
- `target <val>`: Define destino.
- `ssl lets-encrypt | manual <name>`: Configura SSL.
- `balancer`: Entra no **Sub-modo Balancer**.
- `error-page`: Entra no **Sub-modo Error-Page**.
- `ip-filter mode <whitelist/blacklist>`: Política de acesso.
- `auth <user> <pass>`: Basic Authentication.
- `save`: Persiste e volta um nível.

### 4. Balancer-Config Mode (`HttpROS(...-balancer)#`)
Contexto específico para Load Balancing.
- `method <round-robin/least-conn/ip-hash>`: Algoritmo de distribuição.
- `sticky enable/disable`: Persistência de sessão.
- `upstream <ip:port>`: Adiciona nó ao cluster.
- `no upstream <ip:port>`: Remove nó.

### 5. Error-Page-Config Mode (`HttpROS(...-error-page)#`)
Contexto específico para mapeamento de erros.
- `<code> <file>`: Mapeia código (ex: 404) para arquivo em `/error-pages/`.
- `no <code>`: Remove mapeamento customizado.

---

## Funcionalidades Implementadas

- [x] **Hierarquia OS-Style**: Navegação completa entre modos.
- [x] **Contextual Help**: Uso do `?` em qualquer comando para ajuda imediata.
- [x] **Smart Tab Completion**: Completa comandos, sub-comandos, domínios e arquivos.
- [x] **Contextual Show**: O comando `show` dentro de um módulo exibe apenas as configurações daquele módulo.
- [x] **Global Top**: Comando `top` para sair de qualquer nível para a raiz.
- [x] **Docker Integration**: Rodando em .NET 10 (Preview) com SSH Server (Port 50022).
- [x] **Persistência JSON**: Estado salvo de forma legível e editável.

---

## Guia de Acesso Remoto

Para acessar a infraestrutura HttpROS:
1.  Conecte via SSH: `ssh rosadmin@<ip> -p 50022` (Senha: `ros123`).
2.  Inicie a CLI: Digite `http-ros`.
3.  Use o atalho local: `./connect.ssh`.

---

## Estrutura JSON (State Persistence)

```json
{
  "domain": "api.exemplo.com",
  "type": "proxy",
  "target": "http://10.0.0.50:8080",
  "balancer": {
    "method": "round-robin",
    "sticky": true,
    "upstreams": ["10.0.0.10:80", "10.0.0.11:80"]
  },
  "features": {
    "ssl": { "enabled": true, "provider": "lets-encrypt" },
    "gzip": true,
    "websockets": true,
    "cors": false,
    "ipFilter": { "mode": "blacklist", "whitelist": [], "blacklist": [] },
    "basicAuth": { "user": "admin", "pass": "123" },
    "rateLimit": "10r/s",
    "customErrorPages": { "404": "404-default.html" }
  }
}
```
