# Spec: Gestão de Membros e Papéis de Acesso

**Módulo:** Membros / Controle de Acesso (`ApplicationUser` + `UserRole`)
**Versão:** 1.0
**Data:** 26/Set/2026
**Fonte:** Definição com a orientadora do TCC (26/Set) — restrição de gerenciamento de membros por papel.
**Status:** Especificada (não implementada)
**Depende de:** Fluxo de autenticação existente (`AuthController`, `TenantProvider`, JWT com `property_id`). Reaproveita a entidade **`ApplicationUser`** (Identity) e o campo **`IsActive`** já existentes.

> **Decisões de escopo desta spec:**
> - **Papéis mínimos**: apenas **dois** (`Admin`, `Member`), como um único campo em `ApplicationUser`. **Não** se usa a infraestrutura de Roles do ASP.NET Identity (tabelas `AspNetRoles`/`AspNetUserRoles`) — menos tabelas e menos consultas, coerente com o ambiente restrito.
> - **A única capacidade que o papel governa é o gerenciamento de membros** (criar, editar, revogar, reativar). Todo o resto do sistema continua acessível a qualquer usuário autenticado da propriedade.
> - **Revogar = soft delete** via `IsActive = false` (campo já existente). O login já recusa usuário inativo (`AuthController.cs:88`), então revogar já produz o efeito de bloquear acesso, sem novo mecanismo.

---

## 1. Contexto e Objetivo

Hoje qualquer usuário autenticado de uma propriedade pode criar novos membros: o `UsersController` tem apenas `[Authorize]` no nível da classe (`UsersController.cs:14`), sem distinção de papel. Não existe conceito de dono/administrador nem tela para gerenciar quem tem acesso.

Esta spec introduz uma distinção **binária e simples** de papel — `Admin` e `Member` — cujo **único** efeito é restringir o **gerenciamento de membros**. O administrador passa a ter uma tela de membros da propriedade (ativos e revogados) e as ações de **criar**, **editar** (nome e papel), **revogar acesso** e **reativar**. Membros comuns seguem usando o sistema normalmente, mas não enxergam nem executam essas ações.

A restrição é aplicada de forma nativa via `[Authorize(Roles = "Admin")]`, alimentada por um claim `role` no JWT — sem policy customizada e sem middleware novo.

---

## 2. Decisões Registradas

| # | Decisão | Motivo |
|---|---------|--------|
| D1 | Papel como **campo único** `Role : UserRole` em `ApplicationUser`, com dois valores (`Admin`, `Member`). Sem tabelas de Roles do Identity. | Requisito "papéis simples". Menos tabelas e menos joins (ambiente restrito). |
| D2 | Quem **registra a propriedade** (`AuthController.Register`) nasce **`Admin`**. Membros criados via `POST /api/users` nascem **`Member`** (papel escolhível pelo admin na criação). | O criador da fazenda é o dono natural; garante que toda propriedade começa com ao menos um admin. |
| D3 | O papel governa **exclusivamente** o gerenciamento de membros (criar/editar/revogar/reativar). Nenhuma outra funcionalidade é restrita. | Escopo mínimo pedido; evita espalhar autorização pelo sistema. |
| D4 | Papel entra no **JWT como `ClaimTypes.Role`**; a restrição usa `[Authorize(Roles = "Admin")]` nativo. | Zero policy/middleware custom; o ASP.NET já resolve `Roles=` a partir desse claim. |
| D5 | **Revogar acesso = `IsActive = false`** (soft delete, campo existente). **Reativar = `IsActive = true`**. | Reaproveita o bloqueio de login já existente (`AuthController.cs:88`); nada novo a construir para "cortar acesso". |
| D6 | **Editar** um membro permite alterar **`Name`** e **`Role`** (promover/rebaixar entre `Admin` e `Member`). | Definido com a orientadora (26/Set): suporta ter mais de um admin. |
| D7 | **Proteção contra auto-bloqueio**: não é permitido **revogar** nem **rebaixar** o **último `Admin` ativo** da propriedade. | Impede uma propriedade ficar sem nenhum administrador (lockout). |
| D8 | A tela do admin lista membros **ativos e revogados** (filtro `isActive`), para permitir a reativação. | Reativar (D5) exige enxergar os inativos. |
| D9 | Lógica permanece no `UsersController` usando `UserManager` + `DbContext` (como já está hoje), **não** migra para o padrão Service/Repository. | Área de Identity já foge do padrão `BaseEntity`/repositório; manter simples e local ao requisito. |

---

## 3. Histórias de Usuário

### US-01 — Ver os membros da propriedade
> **Como** administrador,
> **quero** ver a lista de membros da minha propriedade, com nome, e-mail, papel e situação (ativo/revogado),
> **para** acompanhar quem tem acesso ao sistema.

**Critérios de aceite:**
- Vejo nome, e-mail, papel (`Admin`/`Member`) e situação de cada membro da **minha** propriedade.
- Posso filtrar por situação: só ativos, só revogados, ou todos.
- Um `Member` comum **não** acessa esta tela (recebe `403`).

### US-02 — Criar um membro
> **Como** administrador,
> **quero** cadastrar um novo membro informando nome, e-mail, senha temporária e papel,
> **para** dar acesso a outra pessoa da fazenda.

**Critérios de aceite:**
- Informo `Name`, `Email`, `TemporaryPassword` e, opcionalmente, `Role` (default `Member`).
- E-mail já cadastrado → `409`.
- O membro é criado **na minha propriedade** (herda o `property_id` do meu token).
- Um `Member` comum **não** consegue criar (recebe `403`).

### US-03 — Editar um membro (nome e papel)
> **Como** administrador,
> **quero** alterar o nome e o papel de um membro,
> **para** corrigir dados e promover/rebaixar administradores.

**Critérios de aceite:**
- `PATCH` altera apenas os campos enviados (`Name` e/ou `Role`).
- Não posso **rebaixar** o último `Admin` ativo da propriedade → `422`.
- Membro de outra propriedade → `404`.

### US-04 — Revogar o acesso de um membro
> **Como** administrador,
> **quero** revogar o acesso de um membro,
> **para** impedir que ele continue usando o sistema.

**Critérios de aceite:**
- Revogar marca `IsActive = false`; o membro deixa de conseguir logar (o login já recusa inativos).
- Não posso revogar o **último `Admin` ativo** → `422`.
- Membro já revogado → `409`.

### US-05 — Reativar um membro
> **Como** administrador,
> **quero** reativar um membro que foi revogado,
> **para** devolver o acesso dele.

**Critérios de aceite:**
- Reativar marca `IsActive = true`; o membro volta a conseguir logar.
- Membro já ativo → `409`.

---

## 4. Casos de Uso

### CU-01 — Listar membros
1. `GET /api/users?isActive=` (opcional) com token de `Admin`.
2. Retorna os membros da propriedade do token (`_tenantProvider.PropertyId`), filtrando por `IsActive` quando informado (`true`=ativos, `false`=revogados, ausente=todos).
3. Retorna `200 [UserListItemDto]`. Chamada por `Member` → `403`.

### CU-02 — Criar membro
1. `POST /api/users` com `CreateUserDto` (token de `Admin`).
2. Valida DTO (`Name`, `Email`, `TemporaryPassword`); inválido → `400`.
3. E-mail já existe → `409`.
4. Cria `ApplicationUser` com `PropertyId` do token, `IsActive = true`, `Role` = enviado ou `Member` (default).
5. Retorna `201 UserResponseDto`.

### CU-03 — Editar membro
1. `PATCH /api/users/{id}` com `UpdateUserDto` (`Name?`, `Role?`), token de `Admin`.
2. Carrega o membro **na propriedade do token**; inexistente/fora do tenant → `404`.
3. Se a alteração rebaixa o **último `Admin` ativo** (RN-05) → `422`.
4. Aplica os campos enviados. Retorna `200 UserResponseDto`.

### CU-04 — Revogar acesso
1. `PATCH /api/users/{id}/revoke` (token de `Admin`).
2. Carrega o membro na propriedade do token; inexistente → `404`.
3. Já revogado (`IsActive == false`) → `409`.
4. É o **último `Admin` ativo** (RN-05) → `422`.
5. Marca `IsActive = false`. Retorna `200 UserResponseDto`.

### CU-05 — Reativar
1. `PATCH /api/users/{id}/reactivate` (token de `Admin`).
2. Carrega o membro na propriedade do token; inexistente → `404`.
3. Já ativo (`IsActive == true`) → `409`.
4. Marca `IsActive = true`. Retorna `200 UserResponseDto`.

---

## 5. Especificação Técnica de Modelagem

### 5.1 Entidade `ApplicationUser`
> `Domain/Models/ApplicationUser.cs` — herda de `IdentityUser` (não de `BaseEntity`). Acrescenta-se **um** campo.

| Campo | Tipo | Obrigatório | Observação |
|-------|------|-------------|------------|
| `Name` | `string` | sim | Existente. |
| `PropertyId` | `Guid` | sim | Existente (tenant). |
| `IsActive` | `bool` | sim | Existente. Revogado = `false`. |
| `Role` | `UserRole` | sim | **Novo.** Default `Member`. |

```csharp
public class ApplicationUser : IdentityUser
{
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    public Guid PropertyId { get; set; }

    public bool IsActive { get; set; } = true;

    public UserRole Role { get; set; } = UserRole.Member;

    public Property? Property { get; set; }
}
```

### 5.2 Enums
> `Domain/Enums/UserRole.cs` — nomes em **inglês**, `[Description]` em **português**.

```csharp
public enum UserRole
{
    [Description("Administrador")]
    Admin = 1,

    [Description("Membro")]
    Member = 2
}
```

### 5.3 DTOs
> `Application/DTOs/`

- **`CreateUserDto`** (existente, ajustado) — `Name` (`[Required]`, `[MaxLength(150)]`), `Email` (`[Required]`, `[EmailAddress]`), `TemporaryPassword` (`[Required]`, `[MinLength(6)]`), **`Role?`** (opcional; default `Member` no controller).
- **`UpdateUserDto`** (novo) — `Name?` (`[MaxLength(150)]`), `Role?`. Ambos opcionais (PATCH parcial; `null` = não altera).
- **`UserResponseDto`** (existente, ajustado) — `Id`, `Name`, `Email`, **`Role`** (`EnumValueDto`), **`IsActive`**.
- **`UserListItemDto`** (novo) — `Id`, `Name`, `Email`, `Role` (`EnumValueDto`), `IsActive`.
- **`UserFilterDto`** (novo) — `bool? IsActive` (`true`=ativos, `false`=revogados, `null`=todos).

### 5.4 Endpoints da API
> Auth: Bearer Token obrigatório em todos. **Todos exigem papel `Admin`** (`[Authorize(Roles = "Admin")]`).

| Método | Rota | Descrição | Retorno |
|--------|------|-----------|---------|
| `GET` | `/api/users` | Listar membros da propriedade (filtro `isActive` opcional) | `200 [UserListItemDto]` / `403` |
| `POST` | `/api/users` | Criar membro | `201 UserResponseDto` / `400` / `403` / `409` |
| `PATCH` | `/api/users/{id}` | Editar nome e/ou papel | `200 UserResponseDto` / `403` / `404` / `422` |
| `PATCH` | `/api/users/{id}/revoke` | Revogar acesso (`IsActive = false`) | `200 UserResponseDto` / `403` / `404` / `409` / `422` |
| `PATCH` | `/api/users/{id}/reactivate` | Reativar (`IsActive = true`) | `200 UserResponseDto` / `403` / `404` / `409` |

> O `GET /api/users` já existe hoje sem restrição de papel (`UsersController.cs:31`) e filtra apenas ativos; passa a exigir `Admin` e a aceitar o filtro `isActive`.

### 5.5 Regras de Negócio
| # | Regra | Onde aplicar |
|---|-------|-------------|
| RN-01 | Todos os endpoints de gerenciamento exigem papel `Admin`. | `[Authorize(Roles = "Admin")]` na classe/ações do `UsersController` |
| RN-02 | O JWT deve conter `ClaimTypes.Role` com o papel do usuário. | `AuthController.GenerateJwtToken` |
| RN-03 | Novo membro herda o `PropertyId` do token e nasce `IsActive = true`. | Controller (`Create`) |
| RN-04 | E-mail único no cadastro. | Controller → `409 Conflict` |
| RN-05 | Não é permitido **revogar** nem **rebaixar** o **último `Admin` ativo** da propriedade (contagem de `Admin` ativos > 1, senão bloqueia). | Controller/Service → lança `BusinessRuleException` (`422`) |
| RN-06 | Revogar em membro já inativo, ou reativar em membro já ativo → conflito de estado. | Controller/Service → lança `ConflictException` (`409`) |
| RN-07 | Ações só atingem membros da **mesma propriedade** do token; fora do tenant → não encontrado. | Filtro por `PropertyId` → `404 NotFoundException` |
| RN-08 | Usuário revogado (`IsActive = false`) não consegue logar. | Já implementado em `AuthController.cs:88` (nenhuma mudança) |
| RN-09 | Quem registra a propriedade nasce `Admin`. | `AuthController.Register` |

### 5.6 Camadas Impactadas
| Camada | Arquivo | Ação |
|--------|---------|------|
| `Domain/Enums` | `UserRole.cs` | **Criar** |
| `Domain/Models` | `ApplicationUser.cs` | **Adicionar** campo `Role` (default `Member`) |
| `Application/DTOs` | `CreateUserDto.cs` | **Ajustar** — adicionar `Role?` |
| `Application/DTOs` | `UpdateUserDto.cs`, `UserListItemDto.cs`, `UserFilterDto.cs` | **Criar** |
| `Application/DTOs` | `UserResponseDto.cs` | **Ajustar** — adicionar `Role`, `IsActive` |
| `Api/Controllers` | `UsersController.cs` | **Ajustar** — `[Authorize(Roles="Admin")]`; `GetAll` com filtro `isActive`; `Create` com `Role`; **adicionar** `Update`, `Revoke`, `Reactivate`; aplicar RN-05 |
| `Api/Controllers` | `AuthController.cs` | **Ajustar** — `Register` cria com `Role = Admin`; `GenerateJwtToken` adiciona claim `ClaimTypes.Role` |
| `Infrastructure/Migrations` | *(ver §6)* | **Requer aprovação antes de criar** |

> **Observação de configuração:** o `[Authorize(Roles = "Admin")]` lê `ClaimTypes.Role` do token por padrão. Se a validação de token estiver com `RoleClaimType` customizado, garantir que aponte para `ClaimTypes.Role` (padrão). Nenhuma nova policy é necessária.

---

## 6. Notas de Migração

> **Requer aprovação explícita antes de executar** (criação/edição de migração — regra do projeto).

**Alterar tabela `AspNetUsers`:**

| Coluna | Tipo | Restrições |
|--------|------|------------|
| `Role` | int | not null, default `2` (`Member`) |

**Backfill dos usuários existentes (base de desenvolvimento):**
- Como a infraestrutura atual não distingue papéis e não há campo confiável de "quem registrou", o backfill sugerido é definir **todos os usuários existentes como `Admin`** (`Role = 1`) na base de dev, evitando lockout, e ajustar manualmente depois.
- **Alternativa** (se preferir): promover apenas um usuário por propriedade a `Admin` e deixar os demais como `Member` — exige escolher o critério (ex.: o usuário mais antigo por `PropertyId`). **A confirmar antes de gerar a migração.**

> Migração sugerida: `Spec_MemberManagement_AddUserRole`. Somente após aprovação.
> Tokens JWT já emitidos não têm o claim `role` até novo login — comportamento aceitável (o usuário reautentica e passa a ter o papel no token).

---

## 7. Fora do Escopo desta Spec

- **Convite por e-mail / fluxo de aceite** — o membro é criado direto com senha temporária, como hoje.
- **Troca de senha / "esqueci minha senha"** — fluxo próprio, não coberto aqui.
- **Permissões granulares por funcionalidade** (além de gerenciamento de membros) — o papel é binário e governa só gestão de membros (D3).
- **Auditoria de ações administrativas** (quem revogou/promoveu quem, quando) — evolução futura.
- **Múltiplas propriedades por usuário** — o modelo permanece 1 usuário → 1 propriedade.
- **Sincronização offline** dos papéis/estado de membro — ver spec de sincronização offline.
