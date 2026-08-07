# Documentação de autenticação e isolamento de dados

Aplicativo de gestão de bovinos para pequenos produtores rurais
Backend: ASP.NET Core 8 · Mobile: Android (Kotlin + Jetpack Compose)

---

## 1. Visão geral

O sistema responde a duas perguntas distintas em toda requisição:

| Pergunta | Nome | Como é resolvida |
|---|---|---|
| Quem é você? | Autenticação | E-mail e senha, via ASP.NET Core Identity |
| De qual fazenda são os dados? | Resolução de tenant | Claim `farm_id` dentro do JWT |

A analogia é o crachá de uma empresa com filiais: o documento prova a identidade na portaria, mas é o crachá que define em qual unidade a pessoa pode circular. O JWT emitido no login carrega as duas informações ao mesmo tempo.

**Regra central do projeto:** o identificador da fazenda nunca é enviado pelo aplicativo. Ele é escrito pelo servidor no momento da emissão do token e lido de volta a cada requisição. Como o JWT é assinado, o cliente não consegue alterá-lo.

### 1.1 Convenção de nomenclatura

Todo identificador de código está em inglês: rotas, entidades, tabelas, colunas, DTOs e claims. Os corpos JSON usam `camelCase`, seguindo o padrão do `System.Text.Json` configurado com `JsonNamingPolicy.CamelCase`.

A documentação em si permanece em português.

---

## 2. Modelo de dados

### 2.1 Entidades de identidade

```
FARM                              USER
----                              ----
Id            uuid  PK            Id             uuid  PK
Name          text                Email          text  UNIQUE
CreatedAt     timestamp           PasswordHash   text
                                  Name           text
                                  FarmId         uuid  FK  NOT NULL
                                  IsActive       bool
                                  CreatedAt      timestamp
```

O relacionamento é **1:N** — uma fazenda tem vários usuários, e cada usuário pertence a exatamente uma fazenda. Não existe tabela de vínculo muitos-para-muitos.

A restrição "um usuário não participa de duas fazendas" é garantida pela própria estrutura (`FarmId` obrigatório e único por linha de usuário), e não por validação em código. O estado inválido é impossível de representar no banco.

### 2.2 Entidades de dados

Toda entidade de negócio carrega `FarmId`, inclusive as que já possuem um caminho indireto até a fazenda:

```
ANIMAL                            HEALTH_RECORD
------                            -------------
Id            uuid  PK            Id            uuid  PK
FarmId        uuid  FK            AnimalId      uuid  FK
TagNumber     text                FarmId        uuid  FK   <- redundante, proposital
BirthDate     date                AppliedAt     date
```

A redundância em `HEALTH_RECORD` é deliberada: o filtro de isolamento fica idêntico em todas as tabelas e nunca depende de um `JOIN` para saber a quem a linha pertence.

## 3. Estrutura do token

### 3.1 Access token (JWT)

Claims obrigatórios no payload:

| Claim | Conteúdo | Uso |
|---|---|---|
| `sub` | Id do usuário | Auditoria, "criado por" |
| `farm_id` | Id da fazenda | Filtro de isolamento de dados |
| `email` | E-mail do usuário | Exibição no app |
| `exp` | Expiração | Validado pelo middleware |

### 3.2 Tempo de vida

| Token | Validade | Justificativa |
|---|---|---|
| Access token | 24 horas | Cobre uma jornada inteira de trabalho sem exigir rede |

Não há refresh token nesta versão. A autenticação é feita por um único token de acesso; quando ele expira, o usuário faz login novamente.

O prazo é mais longo que o padrão de aplicações web por causa da conectividade limitada no meio rural. Um token de 15 minutos expiraria constantemente enquanto o produtor está no curral sem sinal, e sem mecanismo de renovação isso inviabilizaria o uso.

**Limitação assumida:** passadas 24 horas, o usuário precisa de conexão para autenticar de novo. O aplicativo deve tratar esse caso preservando os dados locais e permitindo a consulta offline (seção 8.4), de modo que a expiração bloqueie apenas a sincronização, não o acesso às informações já baixadas.

---

## 4. Fluxos de cadastro

Existem **duas portas de entrada** de usuários no sistema. A porta utilizada determina o que é criado — o servidor nunca precisa detectar se um usuário é "o primeiro" de uma fazenda.

### 4.1 Porta 1 — Registro público

Usada quando a fazenda ainda não existe no sistema. Cria usuário e fazenda na mesma transação.

Este endpoint **sempre** cria uma fazenda nova. Não existe a possibilidade de um usuário se registrar e entrar numa fazenda existente informando seu nome — o nome de uma fazenda não é segredo, e permitir isso deixaria qualquer pessoa se cadastrar na propriedade de um vizinho.

### 4.2 Porta 2 — Criação de usuário por quem já está dentro

Usada quando a fazenda já existe e alguém quer dar acesso a outra pessoa (cônjuge, filho, funcionário). Cria apenas o usuário, herdando a fazenda de quem fez a chamada.

Vínculo com fazenda existente só pode ser criado por quem já está dentro dela.

### 4.3 Fluxo prático

1. O produtor abre o app e toca em "Criar conta". Informa nome, e-mail, senha e o nome da fazenda. → **Porta 1**
2. Dentro do app, qualquer usuário da fazenda acessa "Usuários" → "Adicionar" e informa nome, e-mail e uma senha inicial. → **Porta 2**
3. A outra pessoa baixa o app e faz login normalmente. O token dela já vem com o mesmo `farm_id`.

A opção pela criação direta dentro do app — em vez de convite por link de e-mail — é uma decisão de projeto ligada ao perfil do usuário final. Um fluxo de "abra o e-mail, encontre a mensagem, clique no link antes que expire" pressupõe familiaridade digital e conectividade estável, que não estão presentes no contexto. Duas pessoas resolvendo o cadastro juntas em trinta segundos é mais aderente.

---

## 5. Endpoints

Base: `/api/v1`
Todos os corpos são `application/json`.

### 5.1 `POST /auth/register`

Anônimo. Cria fazenda + usuário.

**Requisição** — `RegisterRequest`
```json
{
  "name": "Leo Teloeken",
  "email": "leo@exemplo.com",
  "password": "SenhaForte123",
  "farmName": "Sítio Santa Rita"
}
```

**Resposta — 201 Created** — `AuthResponse`
```json
{
  "accessToken": "eyJhbGciOi...",
  "expiresAt": "2026-08-06T22:00:00Z",
  "user": {
    "id": "9f1c...",
    "name": "Leo Teloeken"
  },
  "farm": {
    "id": "3ab7...",
    "name": "Sítio Santa Rita"
  }
}
```

**Erros**
| Código | Situação |
|---|---|
| 400 | Campos inválidos ou senha fora da política |
| 409 | E-mail já cadastrado |

A criação de usuário e fazenda deve ocorrer dentro de uma transação explícita. Um usuário salvo sem fazenda fica órfão: consegue autenticar, mas nenhuma funcionalidade responde.

---

### 5.2 `POST /auth/login`

Anônimo. Valida credenciais e emite o token.

**Requisição** — `LoginRequest`
```json
{ "email": "leo@exemplo.com", "password": "SenhaForte123" }
```

**Resposta — 200 OK:** `AuthResponse`, mesmo formato de `/auth/register`.

**Erros**
| Código | Situação |
|---|---|
| 401 | E-mail inexistente ou senha incorreta |
| 403 | Usuário desativado (`IsActive = false`) |

A mensagem retornada em 401 deve ser genérica ("Credenciais inválidas"), sem distinguir e-mail inexistente de senha errada — do contrário o endpoint vira um mecanismo de descoberta de contas.

---

### 5.3 Logout

Não existe endpoint de logout. O JWT não é revogável por natureza: ele permanece válido até `exp`, independentemente do que o servidor faça.

O logout é uma operação exclusivamente local do aplicativo — apagar o token do armazenamento seguro e voltar à tela de login.

---

### 5.4 `GET /auth/me`

Requer autenticação. Retorna os dados da sessão atual. Útil para o app validar o token guardado ao abrir.

**Resposta — 200 OK** — `CurrentUserResponse`
```json
{
  "id": "9f1c...",
  "name": "Leo Teloeken",
  "email": "leo@exemplo.com",
  "farm": { "id": "3ab7...", "name": "Sítio Santa Rita" }
}
```

---

### 5.5 `POST /users`

Requer autenticação. Cria um usuário na fazenda de quem chamou.

**Requisição** — `CreateUserRequest`
```json
{
  "name": "Pedro Teloeken",
  "email": "pedro@exemplo.com",
  "temporaryPassword": "Provisoria123"
}
```

**Resposta — 201 Created** — `UserResponse`
```json
{ "id": "b42e...", "name": "Pedro Teloeken", "email": "pedro@exemplo.com" }
```

**Erros**
| Código | Situação |
|---|---|
| 400 | Campos inválidos |
| 409 | E-mail já cadastrado |

Não existe campo `farmId` no corpo da requisição. O valor vem do token de quem chamou. Se esse campo aparecer em algum DTO de entrada da API, é bug.

---

### 5.6 `GET /users`

Requer autenticação. Lista os usuários da fazenda.

### 5.7 `DELETE /users/{id}`

Requer autenticação. Desativa o usuário (`IsActive = false`). Não remove a linha, para preservar o histórico de quem registrou cada lançamento.

Um usuário não pode desativar a si mesmo — sem essa trava, a última pessoa ativa poderia se remover e deixar a fazenda inacessível.

Como não há revogação de token, um usuário desativado continua operando até o token dele expirar (no máximo 24 horas). A verificação de `IsActive` acontece apenas no login.

Este endpoint pode ficar fora da primeira versão. Sem hierarquia de permissões, qualquer usuário pode desativar os demais; não implementá-lo agora elimina o cenário de remoção mútua acidental sem custo funcional relevante, já que o próprio criador da conta pode ser identificado pelo menor `CreatedAt` da fazenda caso a recuperação seja necessária.

---

## 6. Modelo de permissões

Não há papéis, perfis ou níveis de acesso nesta versão. **Todo usuário vinculado a uma fazenda tem acesso a todas as funcionalidades dela**, incluindo o cadastro de novos usuários.

A decisão é deliberada e reflete o contexto de uso: propriedades familiares com poucos usuários, todos com relação de confiança direta, onde uma hierarquia de permissões adicionaria complexidade sem resolver um problema real observado no levantamento de requisitos.

A única distinção existente no sistema é a fronteira da fazenda, tratada na seção 7. Dentro dela não há distinção; fora dela, nenhum dado é visível.

Consequências assumidas:

- Qualquer usuário pode criar novos usuários na fazenda.
- Qualquer usuário pode desativar outros usuários, caso `DELETE /users/{id}` seja implementado.
- Não existe conceito de "dono" da fazenda em termos de permissão. Se essa informação for necessária, o primeiro usuário é obtido pelo menor `CreatedAt` entre os usuários da fazenda — dado já existente, sem coluna adicional.

Aplicação em ASP.NET Core: todos os endpoints de negócio usam apenas

```csharp
[Authorize]
```

sem `Roles` ou políticas. Endpoints anônimos (`/auth/register` e `/auth/login`) usam `[AllowAnonymous]`.

---

## 7. Isolamento de dados no backend

### 7.1 Provedor de tenant

```csharp
public interface ITenantProvider { Guid FarmId { get; } }

public class TenantProvider(IHttpContextAccessor accessor) : ITenantProvider
{
    public Guid FarmId =>
        Guid.TryParse(accessor.HttpContext?.User.FindFirst("farm_id")?.Value, out var id)
            ? id : Guid.Empty;
}
```

Registrado como `Scoped` (uma instância por requisição).

O provedor **não lança exceção** quando não há claim — do contrário os endpoints anônimos (`/auth/login`, `/auth/register`) quebrariam ao instanciar o `DbContext`. Ele devolve `Guid.Empty`, que não corresponde a nenhum registro. Uma requisição sem token enxerga zero linhas em vez de enxergar tudo: a falha ocorre fechando a porta, não abrindo.

### 7.2 Filtro global de consulta

```csharp
public interface ITenantEntity { Guid FarmId { get; set; } }

public class AppDbContext : DbContext
{
    private readonly Guid _farmId;

    public AppDbContext(DbContextOptions<AppDbContext> options, ITenantProvider tenant)
        : base(options) => _farmId = tenant.FarmId;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<Animal>().HasQueryFilter(a => a.FarmId == _farmId);
        builder.Entity<Supply>().HasQueryFilter(s => s.FarmId == _farmId);
        builder.Entity<HealthRecord>().HasQueryFilter(h => h.FarmId == _farmId);
    }

    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        foreach (var entry in ChangeTracker.Entries<ITenantEntity>()
                                           .Where(e => e.State == EntityState.Added))
            entry.Entity.FarmId = _farmId;
        return base.SaveChangesAsync(ct);
    }
}
```

A expressão do filtro referencia um **campo do próprio `DbContext`**, nunca o `ITenantProvider` injetado. O EF Core cacheia o modelo após o primeiro `OnModelCreating`; se a expressão capturasse um serviço `Scoped`, a referência da primeira requisição ficaria congelada no modelo compilado e todos os usuários passariam a enxergar os dados da primeira fazenda que fez login.

### 7.3 Limitações do filtro

O filtro global **não se aplica** a:

- `FromSqlRaw` e `FromSqlInterpolated`
- `ExecuteUpdate` e `ExecuteDelete`
- Consultas com `IgnoreQueryFilters()`

Nesses casos o `WHERE FarmId = @farmId` é responsabilidade de quem escreve a consulta.

### 7.4 Tabelas fora do filtro

`User` e `Farm` **não** possuem filtro global. No momento do login o usuário ainda não tem fazenda resolvida — se essas tabelas fossem filtradas, seria impossível descobrir a qual fazenda ele pertence. O isolamento dessas tabelas é feito explicitamente nos endpoints de `/users`.

---

## 8. Integração no aplicativo Android

### 8.1 Armazenamento

O token vai em `EncryptedSharedPreferences` (ou DataStore com criptografia). Nunca em `SharedPreferences` comum, arquivo em texto ou log.

O que o app guarda: `accessToken`, `expiresAt`, e uma cópia local de `user` e `farm` para exibição offline.

### 8.2 Envio do token

Interceptor OkHttp anexando o cabeçalho em todas as chamadas exceto as de `/auth`:

```kotlin
class AuthInterceptor(private val store: TokenStore) : Interceptor {
    override fun intercept(chain: Interceptor.Chain): Response {
        val request = chain.request()
        if (request.url.encodedPath.contains("/auth/")) return chain.proceed(request)

        val token = store.accessToken ?: return chain.proceed(request)
        return chain.proceed(
            request.newBuilder().header("Authorization", "Bearer $token").build()
        )
    }
}
```

### 8.3 Tratamento de token expirado

Sem refresh token, não há renovação automática. O aplicativo trata o 401 assim:

```kotlin
val response = chain.proceed(request)
if (response.code == 401) {
    store.clear()
    events.emit(SessionExpired)   // observado pela camada de navegação
}
return response
```

A camada de navegação observa esse evento e leva o usuário para a tela de login, preservando o banco local — os dados já sincronizados continuam visíveis após o novo login.

Antes de cada chamada, o app compara `expiresAt` com o horário atual. Se o token já expirou, evita a requisição e vai direto ao login, poupando uma ida à rede fadada a falhar.

### 8.4 Abertura do app

1. Existe token guardado? Não → tela de login.
2. Sim → navega direto para a tela principal, exibindo dados do banco local.
3. Em segundo plano, chama `GET /auth/me`. Sucesso → segue. 401 → limpa o token e vai para a tela de login.
4. Sem conexão → o app continua funcionando com os dados locais. A ausência de rede nunca deve levar à tela de login.

O passo 4 é essencial no contexto rural: expulsar o usuário para o login porque o celular está sem sinal no curral inviabiliza o uso do aplicativo.

### 8.5 Sincronização e o identificador de fazenda

Registros criados offline são gerados com `UUID` no próprio dispositivo e enviados quando houver conexão. O app **não** envia `farmId` nesses registros — o backend o preenche a partir do token, no `SaveChangesAsync`. Isso mantém o payload menor e elimina a possibilidade de um registro subir com a fazenda errada.

---

## 9. Formato de erro

Todos os erros seguem o padrão `ProblemDetails` (RFC 7807), nativo do ASP.NET Core:

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "Credenciais inválidas",
  "status": 401,
  "detail": "E-mail ou senha incorretos.",
  "instance": "/api/v1/auth/login"
}
```

O aplicativo exibe `title` ao usuário e registra `detail` no log local.

---

## 10. Invariantes de segurança

Estas regras não admitem exceção. Qualquer código que as viole é bug, independentemente de funcionar:

1. `farm_id` vem do token ou é gerado pelo servidor. Nunca do corpo, query string ou cabeçalho enviado pelo cliente.
2. Nenhum DTO de entrada possui campo `farmId`.
3. Senhas trafegam apenas sobre HTTPS e são gravadas apenas como hash (padrão do Identity: PBKDF2).
4. Toda entidade de negócio implementa `ITenantEntity` e possui filtro global registrado.
5. Consultas com SQL bruto declaram o filtro de fazenda explicitamente.
6. Autenticação é exigida em todos os endpoints de negócio. A ausência de papéis não implica ausência de `[Authorize]`.
7. A chave de assinatura do JWT fica em configuração de ambiente, nunca versionada no repositório. Sem revogação de token, o vazamento da chave só é contornável trocando a chave e invalidando todas as sessões.

---

## 11. Fora do escopo atual

Registrados como possibilidades de evolução, deliberadamente não implementados:

- **Refresh token.** Permitiria sessões longas sem novo login e revogação real de acesso. Exige uma tabela `RefreshToken` (com o token gravado como hash), endpoint de troca com rotação e um `Authenticator` no OkHttp. Não altera o modelo de isolamento nem o formato do JWT.
- **Papéis e permissões diferenciadas.** Caso surja a necessidade de restringir operações (por exemplo, impedir que um funcionário temporário cadastre usuários), a estrutura comporta a adição de uma coluna `Role` em `User` e do claim correspondente no token, sem alteração no modelo de isolamento.
- **Usuário em múltiplas fazendas.** Exigiria tabela de vínculo muitos-para-muitos e um passo adicional de seleção de fazenda após o login.
- **Recuperação de senha por e-mail.** Depende de serviço de envio e de conectividade; no cenário atual outro usuário da fazenda redefine a senha diretamente.
- **Autenticação em dois fatores.**
- **Transferência de fazenda** entre usuários (venda da propriedade).

---

*Documento de arquitetura — revisar a cada alteração no modelo de autenticação.*
