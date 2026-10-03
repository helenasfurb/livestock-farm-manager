# Catálogo de Erros da API — MuuBoi

**Data:** 02/Out/2026
**Escopo:** todos os erros que a API retorna **hoje** (estado do código em `main` + Fase 1 do offline), levantados a partir de `Application/Services`, `Api/Controllers`, `Api/Middleware/ExceptionMiddleware.cs`, `Application/DTOs` e `Program.cs`.
**Uso:** referência para o front-end web e para o app offline decidirem o que fazer com cada resposta.

> Os exemplos de JSON são ilustrativos: `traceId`, ids e datas variam a cada requisição.

---

## 1. Visão geral

| Status | Significado | Quem gera | Formato do corpo (§2) |
|---|---|---|---|
| `400 Bad Request` | Dado de entrada inválido | Validação automática dos DTOs; JSON malformado; Identity (senha fraca) | **B** (ProblemDetails de validação) ou **D** (`message` + `errors`) |
| `401 Unauthorized` | Sem token, token inválido/expirado, sessão invalidada ou credenciais erradas | Autenticação JWT; `AuthController` | **E** (vazio), **C** ou **D** |
| `403 Forbidden` | Sem permissão (não é Admin) ou usuário desativado no login | `[Authorize(Roles = "Admin")]`; `AuthController.Login` | **E** (vazio) ou **D** |
| `404 Not Found` | Recurso não existe (ou é de outra propriedade) | `NotFoundException`; `return NotFound()`; rota inexistente | **A**, **C** ou **E** |
| `405 Method Not Allowed` | Verbo HTTP errado para a rota | ASP.NET Core | **E** (vazio) |
| `409 Conflict` | Conflito de estado (duplicado, já inativo, já existe) | `ConflictException`; `AuthController.Register` | **A** ou **D** |
| `415 Unsupported Media Type` | Corpo sem `Content-Type: application/json` | ASP.NET Core | **C** |
| `422 Unprocessable Entity` | Regra de negócio violada | `BusinessRuleException` | **A** |
| `500 Internal Server Error` | Erro inesperado | `ExceptionMiddleware` (qualquer exceção não tratada); `AuthController.Login` | **A** ou **D** |

**Tenant:** como todos os repositórios filtram por `PropertyId`, um recurso que existe mas pertence a **outra propriedade** responde **`404`** — a API nunca revela que ele existe.

---

## 2. Formatos de corpo

A API tem hoje **cinco formatos** de erro, dependendo de onde o erro nasce.

### Formato A — `ExceptionMiddleware` (`{ "error": ... }`)

Usado por **todas as exceções de domínio** lançadas nos services e controllers (`NotFoundException`, `ConflictException`, `BusinessRuleException`, `ValidationException`) e por qualquer exceção não tratada (`500`).

```http
HTTP/1.1 422 Unprocessable Entity
Content-Type: application/json

{ "error": "Não há doses disponíveis para a amostra de sêmen selecionada." }
```

```http
HTTP/1.1 500 Internal Server Error

{ "error": "Internal server error" }
```

| Exceção | Status |
|---|---|
| `NotFoundException` | `404` |
| `ConflictException` | `409` |
| `BusinessRuleException` | `422` |
| `System.ComponentModel.DataAnnotations.ValidationException` | `400` |
| Qualquer outra (`Exception`) | `500` — mensagem fixa, sem detalhes |

### Formato B — ProblemDetails de validação (`errors` por campo)

Gerado **automaticamente** pelo `[ApiController]` **antes de chegar ao controller**, quando o DTO falha em `[Required]`, `[Range]`, `[MaxLength]`, `[RegularExpression]`, `[EmailAddress]` ou `IValidatableObject.Validate`. A chave de `errors` é o nome do campo; pode haver várias mensagens por campo.

```http
POST /api/milk-productions
{ "date": "2030-01-01", "volume": 0 }
```

```http
HTTP/1.1 400 Bad Request
Content-Type: application/problem+json

{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Volume": [ "O volume deve ser maior que zero." ]
  },
  "traceId": "00-6f1c...-01"
}
```

> Os erros de `IValidatableObject` (ex.: "A data não pode ser futura.") **só aparecem se os atributos passarem**. No exemplo acima, a data futura seria reportada numa segunda tentativa, depois de corrigir o volume.

**JSON malformado ou tipo errado** — mesma estrutura, com a chave no formato de caminho JSON:

```http
POST /api/milk-productions
{ "date": "2026-10-02", "volume": "muito" }
```

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "dto": [ "The dto field is required." ],
    "$.volume": [ "The JSON value could not be converted to System.Decimal. Path: $.volume | LineNumber: 0 | BytePositionInLine: 37." ]
  },
  "traceId": "00-..."
}
```

**Corpo vazio** num `POST`/`PATCH`:

```json
{
  "errors": {
    "": [ "A non-empty request body is required." ],
    "dto": [ "The dto field is required." ]
  }
}
```

### Formato C — ProblemDetails só com status

Gerado quando o controller retorna `NotFound()` / `Unauthorized()` **sem corpo**, ou pelo ASP.NET Core em `415`. Não traz mensagem de negócio.

```http
HTTP/1.1 404 Not Found
Content-Type: application/problem+json

{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "Not Found",
  "status": 404,
  "traceId": "00-..."
}
```

### Formato D — `{ "message": ... }` (Auth/Users)

Usado diretamente no `AuthController` e em parte do `UsersController`. Quando vem do Identity, inclui `errors` (lista de textos).

```http
HTTP/1.1 401 Unauthorized

{ "message": "Credenciais inválidas." }
```

```http
HTTP/1.1 400 Bad Request

{
  "message": "Erro ao criar usuário.",
  "errors": [
    "Passwords must have at least one non alphanumeric character.",
    "Passwords must have at least one uppercase ('A'-'Z')."
  ]
}
```

### Formato E — sem corpo

| Situação | Status | Detalhe |
|---|---|---|
| Token ausente | `401` | Header `WWW-Authenticate: Bearer` |
| Token expirado | `401` | Header `WWW-Authenticate: Bearer error="invalid_token", error_description="The token expired at '...'"` |
| Token com assinatura inválida | `401` | Header `WWW-Authenticate: Bearer error="invalid_token", ...` |
| Sessão invalidada (troca de senha, `security_stamp` diferente) | `401` | Header `WWW-Authenticate: Bearer error="invalid_token"` |
| Usuário não-Admin em rota de Admin | `403` | — |
| Rota inexistente ou restrição não satisfeita (ex.: `/api/milk-productions/abc`, pois `{id:int}`) | `404` | — |
| Verbo errado (ex.: `PUT /api/animals/1`) | `405` | — |
| Exceção **antes** do `ExceptionMiddleware` (ex.: falha de banco ao validar a sessão no `OnTokenValidated`) | `500` | O `ExceptionMiddleware` é registrado depois de `UseAuthentication`/`UseAuthorization` (`Program.cs:177–180`), então não captura esses erros |

---

## 3. Autenticação e autorização

| Situação | Status | Formato | Mensagem / detalhe |
|---|---|---|---|
| Rota com `[Authorize]` sem header `Authorization` | `401` | E | — |
| Token expirado (`ClockSkew = 0`: expira no segundo exato) | `401` | E | `error_description` com a data de expiração |
| Token sem `NameIdentifier` ou sem `security_stamp` | `401` | E | Falha interna "Sessão inválida." (não vai no corpo) |
| `security_stamp` do token diferente do atual (senha trocada, acesso revogado) | `401` | E | Idem |
| Rota de Admin (`/api/users/*`, `DELETE /api/auth/me`) com usuário Member | `403` | E | — |
| `ICurrentUserService` usado sem usuário autenticado | `500` | A | `"Internal server error"` (exceção interna: "User is not authenticated.") |

---

## 4. Catálogo por módulo

Legenda de status: **400** validação · **404** não encontrado · **409** conflito · **422** regra de negócio.
Os erros de validação de DTO (**400**, formato B) estão agrupados na §5.

### 4.1 Auth — `/api/auth`

| Rota | Status | Formato | Mensagem |
|---|---|---|---|
| `POST /register` | 409 | D | Email já cadastrado. |
| `POST /register` | 400 | D | Erro ao criar usuário. + `errors` do Identity (§6) |
| `POST /login` | 401 | D | Credenciais inválidas. *(email não existe **ou** senha errada — mesma mensagem de propósito)* |
| `POST /login` | 403 | D | Usuário desativado. |
| `POST /login` | 500 | D | Propriedade não encontrada. |
| `GET /me` | 401 | C | — |
| `PATCH /me` | 401 | C | — |
| `PATCH /me/password` | 401 | C | — |
| `PATCH /me/password` | 422 | A | Senha atual incorreta. |
| `PATCH /me/password` | 400 | D | Erro ao alterar a senha. + `errors` do Identity (§6) |
| `DELETE /me` *(Admin)* | 401 | C | — |
| `DELETE /me` *(Admin)* | 403 | E | — (usuário Member) |
| `DELETE /me` *(Admin)* | 422 | A | Senha incorreta. |

### 4.2 Usuários (membros) — `/api/users` *(somente Admin)*

| Rota | Status | Formato | Mensagem |
|---|---|---|---|
| Todas | 403 | E | — (usuário Member) |
| `POST /` | 409 | A | Email já cadastrado. |
| `POST /` | 400 | D | Erro ao criar usuário. + `errors` do Identity (§6) |
| `PATCH /{id}`, `/{id}/revoke`, `/{id}/reactivate` | 404 | A | Membro não encontrado. |
| `PATCH /{id}` | 422 | A | Não é possível rebaixar o último administrador ativo da propriedade. |
| `PATCH /{id}/revoke` | 409 | A | O acesso deste membro já está revogado. |
| `PATCH /{id}/revoke` | 422 | A | Não é possível revogar o acesso do último administrador ativo da propriedade. |
| `PATCH /{id}/reactivate` | 409 | A | Este membro já está ativo. |

### 4.3 Animais — `/api/animals`

| Rota | Status | Mensagem |
|---|---|---|
| `GET /{id}`, `PATCH /{id}`, `PATCH /{id}/exit`, `PATCH /{id}/reactivate`, `GET /{id}/exit-records` | 404 | Animal com id '{id}' não encontrado. |
| `POST /`, `PATCH /{id}` | 409 | Já existe um animal com o brinco '{brinco}' nesta propriedade. |
| `POST /` | 422 | A lactação inicial só se aplica a vacas e novilhas. |
| `PATCH /{id}/exit` | 409 | Não é possível registrar saída de um animal já inativo. |
| `PATCH /{id}/reactivate` | 409 | Não é possível reativar um animal que já está ativo. |
| `GET /{id}/vaccination-history` | 404 | Animal com id '{id}' não encontrado. |
| `GET /{id}/health-history` | 404 | Animal com id '{id}' não encontrado. |

Exemplo:

```http
POST /api/animals
{ "tagNumber": "123456", "gender": 2, "classification": 1, ... }
```

```http
HTTP/1.1 409 Conflict

{ "error": "Já existe um animal com o brinco '123456' nesta propriedade." }
```

### 4.4 Pesagens — `/api/animals/{animalId}/weight-records`

| Rota | Status | Formato | Mensagem |
|---|---|---|---|
| Todas | 404 | A | Animal com id '{animalId}' não encontrado. |
| `GET /{weightRecordId}`, `PATCH /{weightRecordId}`, `DELETE /{weightRecordId}` | 404 | C | — (registro não encontrado) |
| Todas, com `animalId` não numérico (ex.: `/api/animals/abc/weight-records`) | **500** | A | Internal server error *(ver §7, item 3)* |

### 4.5 Medicamentos do animal — `/api/animals/{animalId}/medications`

| Rota | Status | Formato | Mensagem |
|---|---|---|---|
| Todas | 404 | A | Animal not found *(em inglês — ver §7)* |
| `POST /` | 404 | A | Medication not found |
| `GET /{id}`, `PATCH /{id}`, `DELETE /{id}` | 404 | C | — |
| Todas, com `animalId` não numérico | **500** | A | Internal server error *(ver §7, item 3)* |

### 4.6 Catálogos: medicamentos e vacinas — `/api/medications`, `/api/vaccines`

| Rota | Status | Formato | Mensagem |
|---|---|---|---|
| `GET /{id}`, `PATCH /{id}`, `DELETE /{id}` | 404 | C | — |

### 4.7 ECC (condição corporal) — `/api/animals/{animalId}/body-condition-records`

| Rota | Status | Mensagem |
|---|---|---|
| `GET /`, `POST /`, `PATCH /{recordId}` | 404 | Animal com id '{animalId}' não encontrado. |
| `POST /` | 409 | Não é possível registrar ECC de um animal inativo. |
| `PATCH /{recordId}` | 404 | Registro de ECC com id '{recordId}' não encontrado. |
| `POST /`, `PATCH /{recordId}` | 422 | A data da avaliação não pode ser futura. |

### 4.8 Coberturas — `/api/breeding-events`, `/api/animals/{animalId}/breeding-events`

| Rota | Status | Mensagem |
|---|---|---|
| `GET /api/animals/{animalId}/breeding-events`, `POST` | 404 | Animal com id '{animalId}' não encontrado. |
| `GET /api/breeding-events/{id}` | 404 | Evento reprodutivo com id '{id}' não encontrado. |
| `POST` | 409 | Não é possível registrar coberturas para um animal inativo. |
| `POST` | 422 | Apenas vacas e novilhas podem ser submetidas a coberturas. |
| `POST` | 422 | O animal já possui uma cobertura aguardando diagnóstico. Registre o diagnóstico do serviço anterior antes de criar uma nova. |
| `POST` | 422 | O animal possui uma gestação ativa. Não é possível registrar uma nova cobertura. |
| `POST`, `PATCH /{id}` | 404 | Amostra de sêmen com id '{id}' não encontrada. |
| `POST`, `PATCH /{id}` | 409 | A amostra de sêmen selecionada está inativa. |
| `POST` | 422 | Não há doses disponíveis para a amostra de sêmen selecionada. |
| `POST`, `PATCH /{id}` | 404 | Touro com id '{id}' não encontrado. |
| `POST`, `PATCH /{id}` | 409 | O touro selecionado está inativo. |
| `POST`, `PATCH /{id}` | 422 | O animal informado como touro pai não possui classificação 'Touro'. |
| `PATCH /{id}` | 404 | Cobertura com id '{id}' não encontrado. |
| `PATCH /{id}` | 409 | Apenas coberturas com diagnóstico pendente podem ser editados. |
| `PATCH /{id}/status` | 404 | Cobertura com id '{id}' não encontrada. |
| `PATCH /{id}/status` | 409 | O diagnóstico desta cobertura já foi registrado. |
| `PATCH /{id}/status` | 422 | A data do diagnóstico não pode ser anterior à data da cobertura. |
| `DELETE /{id}` | 404 | Cobertura com id '{id}' não encontrada. |
| `DELETE /{id}` | 409 | A cobertura já está inativa. |
| `DELETE /{id}` | 409 | Esta cobertura possui uma gestação ativa vinculada. Inative a gestação primeiro. |

### 4.9 Gestações — `/api/pregnancies`, `/api/animals/{animalId}/pregnancies`

| Rota | Status | Mensagem |
|---|---|---|
| `GET /api/animals/{animalId}/pregnancies`, `POST` | 404 | Animal com id '{animalId}' não encontrado. |
| `GET /api/pregnancies/{id}`, `PATCH /{id}/status`, `DELETE /{id}` | 404 | Gestação com id '{id}' não encontrada. |
| `POST` | 409 | Não é possível registrar gestações para um animal inativo. |
| `POST` | 422 | Apenas vacas e novilhas podem ter gestação registrada. |
| `POST` | 409 | O animal já possui uma gestação ativa confirmada. |
| `POST` | 422 | Informe no máximo um entre touro e sêmen. |
| `POST` | 404 | Touro com id '{id}' não encontrado. |
| `POST` | 409 | O touro informado está inativo. |
| `POST` | 422 | O animal informado como pai não possui classificação 'Touro'. |
| `POST` | 404 | Amostra de sêmen com id '{id}' não encontrada. |
| `POST` | 409 | A amostra de sêmen informada está inativa. |
| `POST` | 422 | A data prevista de parto deve ser posterior à data de confirmação. |
| `PATCH /{id}/status` | 409 | Apenas gestações confirmadas podem ser marcadas como interrompidas. |
| `DELETE /{id}` | 409 | A gestação já está inativa. |
| `DELETE /{id}` | 409 | Esta gestação possui um parto ativo vinculado. Inative o parto primeiro. |

### 4.10 Partos — `/api/pregnancies/{pregnancyId}/calvings`, `/api/calvings`

| Rota | Status | Mensagem |
|---|---|---|
| `POST` | 404 | Gestação com id '{pregnancyId}' não encontrada. |
| `POST` | 409 | O parto só pode ser registrado para uma gestação confirmada. |
| `POST` | 409 | Esta gestação já possui um parto ativo registrado. |
| `POST` | 422 | A data do parto não pode ser anterior à data de confirmação da gestação. |
| `POST` | 422 | O animal possui uma lactação em aberto. Registre a secagem antes de lançar um novo parto. |
| `POST` | 409 | O brinco '{brinco}' está repetido entre as crias deste parto. |
| `POST` | 409 | Já existe um animal com o brinco '{brinco}'. |
| `PATCH /api/calvings/{calvingId}/calves/{calfId}` | 404 | Cria com id '{calfId}' não encontrada no parto '{calvingId}'. |
| `PATCH /api/calvings/{calvingId}/calves/{calfId}` | 409 | Não é possível editar uma cria de um parto inativo. |
| `DELETE /api/calvings/{id}` | 404 | Parto com id '{id}' não encontrado. |
| `DELETE /api/calvings/{id}` | 409 | O parto já está inativo. |

### 4.11 Lactações — `/api/lactations`, `/api/animals/{animalId}/lactations`

| Rota | Status | Mensagem |
|---|---|---|
| `GET /api/animals/{animalId}/lactations`, `.../current`, `POST` | 404 | Animal com id '{animalId}' não encontrado. |
| `POST` | 409 | Não é possível cadastrar lactação para um animal inativo. |
| `POST` | 422 | Somente vacas e novilhas podem ter lactação. |
| `POST` | 409 | O animal já possui uma lactação em aberto. Seque a atual antes de abrir outra. |
| `GET /api/lactations/{id}`, `PATCH`, `POST /dry-off`, `DELETE /dry-off`, `DELETE` | 404 | Lactação com id '{id}' não encontrada. |
| `PATCH /{id}` | 409 | Não é possível editar uma lactação inativa. |
| `PATCH /{id}` | 422 | A data de início não pode ser posterior à data da secagem. |
| `POST /{id}/dry-off` | 409 | Não é possível secar uma lactação inativa. |
| `POST /{id}/dry-off` | 409 | Esta lactação já está seca. |
| `POST /{id}/dry-off` | 422 | A data da secagem não pode ser anterior ao início da lactação. |
| `DELETE /{id}/dry-off` | 409 | Não é possível reabrir uma lactação inativa. |
| `DELETE /{id}/dry-off` | 409 | Esta lactação já está em aberto. |
| `DELETE /{id}/dry-off` | 409 | O animal já possui outra lactação em aberto. |
| `DELETE /{id}` | 409 | A lactação já está inativa. |

> `GET /api/animals/{animalId}/lactations/current` responde **`204 No Content`** (não é erro) quando não há lactação aberta.

### 4.12 Produção de leite — `/api/milk-productions`

| Rota | Status | Mensagem |
|---|---|---|
| `GET /{id}`, `PATCH /{id}`, `DELETE /{id}` | 404 | Lançamento de produção de leite com id '{id}' não encontrado. |
| `GET /changes?since=` | 400 | Cursor de sincronização inválido. *(formato A — `since` não numérico ou negativo)* |

> `GET /changes` **não** retorna erro para `limit` fora da faixa: ausente, zero ou negativo vira 500; acima de 500 é limitado a 500.

> **Rota com suporte offline** (ver `Docs/Plan/plano-offline-producao-leite.md`). Repetir uma operação já aplicada **não é erro**:
> - `POST` com `syncId` já existente → `201` com o registro existente.
> - `PATCH` com `updatedAt` mais antigo que a versão do servidor → `200` com a versão do servidor (edição ignorada).
> - `DELETE` em registro já inativo → `204` *(até 02/Out/2026 era `409` "O lançamento de produção de leite já está inativo.")*.
> - `POST` com `syncId` vazio (`00000000-...`) → `400` "O identificador de sincronização não pode ser vazio." (formato B, campo `SyncId`).

### 4.13 Sêmen — `/api/semen-samples`

| Rota | Status | Mensagem |
|---|---|---|
| `GET /{id}`, `PATCH /{id}`, `DELETE /{id}`, `PATCH /{id}/reactivate` | 404 | Amostra de sêmen com id '{id}' não encontrada. |
| `DELETE /{id}` | 409 | A amostra de sêmen já está inativa. |
| `PATCH /{id}/reactivate` | 409 | A amostra de sêmen já está ativa. |
| Rotas de `/{semenSampleId}/movements` | 404 | Amostra de sêmen com id '{semenSampleId}' não encontrada. |
| `GET`, `PATCH`, `DELETE /{semenSampleId}/movements/{movementId}` | 404 | Movimentação com id '{movementId}' não encontrada. |
| `POST /{semenSampleId}/movements` | 409 | Não é possível registrar movimentação para uma amostra de sêmen inativa. |
| `PATCH /{semenSampleId}/movements/{movementId}` | 409 | Movimentações geradas pelo sistema não podem ser editadas diretamente. |
| `DELETE /{semenSampleId}/movements/{movementId}` | 409 | Movimentações geradas pelo sistema não podem ser inativadas diretamente. |
| `DELETE /{semenSampleId}/movements/{movementId}` | 409 | A movimentação já está inativa. |

### 4.14 Estoque de insumos — `/api/stock-items`

| Rota | Status | Mensagem |
|---|---|---|
| `GET /{id}`, `PATCH /{id}`, `DELETE /{id}` | 404 | Insumo com id '{id}' não encontrado. |
| `POST`, `PATCH /{id}` | 404 | Categoria com id '{id}' não encontrada. |
| `POST`, `PATCH /{id}` | 404 | Unidade de medida com id '{id}' não encontrada. |
| `DELETE /{id}` | 409 | O insumo já está inativo. |
| Rotas de `/{stockItemId}/movements` | 404 | Insumo com id '{stockItemId}' não encontrado. |
| `GET`, `PATCH`, `DELETE /{stockItemId}/movements/{movementId}` | 404 | Movimentação com id '{movementId}' não encontrada. |
| `POST /{stockItemId}/movements` | 409 | Não é possível registrar movimentação para um insumo inativo. |
| `PATCH /{stockItemId}/movements/{movementId}` | 422 | A data da movimentação não pode ser futura. |
| `DELETE /{stockItemId}/movements/{movementId}` | 409 | A movimentação já está inativa. |

### 4.15 Vacinação — `/api/vaccination-events`

| Rota | Status | Mensagem |
|---|---|---|
| `GET /{id}`, `PATCH /{id}`, `DELETE /{id}` | 404 | Evento de vacinação com id '{id}' não encontrado. |
| `POST` | 404 | Vacina com id '{id}' não encontrada. |
| `POST` | 404 | Animais não encontrados: {id1}, {id2}. |
| `POST /{id}/booster` | 404 | Evento de vacinação com id '{id}' não encontrado. |
| `POST /{id}/booster` | 422 | A data prevista do reforço não pode ser anterior à data de aplicação do evento pai. |
| `POST /{id}/booster` | 422 | A data prevista do reforço não pode ser anterior à data prevista do evento pai. |
| `POST /{id}/booster` | 409 | Já existe um reforço para este evento. |
| `DELETE /{id}` | 409 | O evento de vacinação já está inativo. |

Exemplo:

```http
POST /api/vaccination-events
{ "vaccineId": 3, "animalIds": [10, 999, 1000], "applicationDate": "2026-10-01" }
```

```http
HTTP/1.1 404 Not Found

{ "error": "Animais não encontrados: 999, 1000." }
```

### 4.16 Casos de saúde — `/api/health-cases`

| Rota | Status | Mensagem |
|---|---|---|
| `POST` | 404 | Animal com id '{animalId}' não encontrado. |
| `GET /{id}`, `PATCH /{id}`, `DELETE /{id}`, `POST /{id}/medications`, `POST /{id}/tests` | 404 | Caso de saúde com id '{id}' não encontrado. |
| `DELETE /{id}` | 409 | O caso de saúde já está inativo. |
| `DELETE /{id}/medications/{medicationId}` | 404 | Aplicação com id '{medicationId}' não encontrada. |
| `DELETE /{id}/medications/{medicationId}` | 409 | A aplicação já está inativa. |
| `DELETE /{id}/tests/{testId}` | 404 | Teste com id '{testId}' não encontrado. |
| `DELETE /{id}/tests/{testId}` | 409 | O teste já está inativo. |

### 4.17 Somente leitura (sem erros de domínio)

`/api/dashboard`, `/api/dashboard/productive`, `/api/dashboard/reproductive`, `/api/stock/*`, `/api/stock-categories`, `/api/units-of-measure` e todos os endpoints de *lookup* (`/genders`, `/breeds`, `/milkings`, `/statuses` etc.) só retornam os erros transversais: `401`, `500`.

---

## 5. Erros de validação de DTO (`400`, formato B)

Disparados automaticamente antes do controller. A chave de `errors` é o nome do campo (ou `""` para validações de `IValidatableObject` sem campo definido).

### 5.1 Mensagens personalizadas (português)

| DTO | Mensagens |
|---|---|
| `AnimalCreateDto` | O brinco principal é obrigatório. · O brinco principal deve ter exatamente 6 dígitos numéricos. · O sexo é obrigatório. · A classificação é obrigatória. · A classificação '{x}' é exclusiva de fêmeas. · A classificação '{x}' é exclusiva de machos. · A data do ECC inicial não pode ser futura. |
| `AnimalUpdateDto` | O brinco principal deve ter exatamente 6 dígitos numéricos. · A classificação '{x}' é exclusiva de fêmeas. · A classificação '{x}' é exclusiva de machos. |
| `AnimalExitDto` | O motivo de saída é obrigatório. · A data de saída é obrigatória. · A data de saída não pode ser futura. |
| `BodyConditionRecordCreateDto` | O escore de condição corporal é obrigatório. · A data da avaliação é obrigatória. |
| `BreedingEventCreateDto` | O tipo de reprodução é obrigatório. · A data da cobertura é obrigatória. · A data da cobertura não pode ser futura. · O sêmen é obrigatório para inseminação artificial. · O touro pai não deve ser informado para inseminação artificial. · O touro pai é obrigatório para monta natural. · O sêmen não deve ser informado para monta natural. |
| `BreedingEventUpdateDto` | A data da cobertura não pode ser futura. |
| `BreedingEventStatusUpdateDto` | O status é obrigatório. · A data do diagnóstico é obrigatória. · O método de diagnóstico é obrigatório. · A idade gestacional deve estar entre 1 e 279 dias. · O status não pode ser alterado para 'Aguardando diagnóstico'. · A data do diagnóstico não pode ser futura. · A idade gestacional só pode ser informada quando a gestação é confirmada. |
| `AnimalPregnancyRetroactiveCreateDto` | A data de confirmação é obrigatória. · Informe pelo menos uma: data estimada de concepção ou data prevista de parto. · A data prevista de parto deve ser posterior à data de confirmação. · Informe no máximo um entre touro (SireAnimalId) e sêmen (SemenSampleId). · A data de confirmação não pode ser futura. |
| `AnimalPregnancyStatusUpdateDto` | O status é obrigatório. · A data da perda é obrigatória. · Apenas o status 'Parto interrompido' pode ser definido manualmente. 'Parto realizado' é gerado automaticamente pelo sistema. · A data da perda não pode ser futura. |
| `AnimalCalvingCreateDto` | A data do parto é obrigatória. · Informe ao menos uma cria. · A data do parto não pode ser futura. |
| `AnimalCalvingCalfCreateDto` | O sexo da cria é obrigatório. · O peso deve ser entre 0,01 e 999,99 kg. · O status vital da cria é obrigatório. · O brinco deve ter no máximo 6 caracteres. · O nome é obrigatório para crias nascidas vivas. · A raça é obrigatória para crias nascidas vivas. · Brinco e brinco de fazenda só se aplicam a crias nascidas vivas. |
| `AnimalCalvingCalfUpdateDto` | O peso deve ser entre 0,01 e 999,99 kg. |
| `LactationCreateDto` | A data de início da lactação é obrigatória. · A data de início não pode ser futura. |
| `LactationUpdateDto` | A data de início não pode ser futura. |
| `LactationDryOffDto` | A data da secagem é obrigatória. · A data da secagem não pode ser futura. |
| `LactationSeedDto` *(dentro de `AnimalCreateDto`)* | A data de início da lactação é obrigatória. · A data de início não pode ser futura. · A data da secagem não pode ser futura. · A data da secagem não pode ser anterior ao início da lactação. |
| `MilkProductionCreateDto` | A data é obrigatória. · O volume é obrigatório. · O volume deve ser maior que zero. · A data não pode ser futura. · O identificador de sincronização não pode ser vazio. |
| `MilkProductionUpdateDto` | O volume deve ser maior que zero. · A data não pode ser futura. |
| `SemenSampleCreateDto` | O nome é obrigatório. · A quantidade inicial deve ser entre 1 e 9.999. |
| `SemenSampleMovementCreateDto` | O tipo de movimentação é obrigatório. · A data da movimentação é obrigatória. · A quantidade é obrigatória. · A quantidade deve ser entre 1 e 9.999. |
| `SemenSampleMovementUpdateDto` | A quantidade deve ser entre 1 e 9.999. |
| `StockItemCreateDto` | O nome é obrigatório. · A categoria é obrigatória. · A unidade de medida é obrigatória. · O ponto crítico não pode ser negativo. · O tempo de reposição deve ser entre 1 e 3.650 dias. · A quantidade inicial deve ser maior que zero. · O valor inicial não pode ser negativo. |
| `StockItemUpdateDto` | O ponto crítico não pode ser negativo. · O tempo de reposição deve ser entre 1 e 3.650 dias. |
| `StockMovementCreateDto` | O tipo de movimentação é obrigatório. · O motivo da movimentação é obrigatório. · A data da movimentação é obrigatória. · A quantidade é obrigatória. · A quantidade deve ser maior que zero. · O valor total não pode ser negativo. · O valor unitário não pode ser negativo. · A data da movimentação não pode ser futura. · O motivo '{x}' exige movimentação do tipo '{y}'. · O valor unitário é obrigatório quando o modo de valor é 'Unitário'. · O valor total é obrigatório para compra ou saldo inicial. |
| `StockMovementUpdateDto` | A quantidade deve ser maior que zero. · O valor total não pode ser negativo. |
| `VaccinationEventCreateDto` | A vacina é obrigatória. · Informe ao menos um animal. · Informe ao menos uma data: de aplicação ou prevista. · A data de aplicação não pode ser futura. · A lista de animais não pode conter duplicatas. |
| `VaccinationEventUpdateDto` | A data de aplicação não pode ser futura. |
| `VaccinationBoosterCreateDto` | A data prevista é obrigatória. |
| `HealthCaseCreateDto` | O animal é obrigatório. · A data de diagnóstico é obrigatória. · Informe o tipo de doença. · Informe o nome da doença quando o tipo for 'Outra'. · Quartos afetados só se aplicam a casos de mastite. · A data de diagnóstico não pode ser futura. |
| `HealthCaseUpdateDto` | A data de diagnóstico não pode ser futura. · A data de encerramento não pode ser futura. |
| `MedicationUseCreateDto` | O medicamento é obrigatório. · A data de aplicação é obrigatória. · A data de aplicação não pode ser futura. · A carência não pode ser negativa. |
| `MastitisTestCreateDto` | O resultado é obrigatório. · A data do teste é obrigatória. · Informe o tipo de teste. · A data do teste não pode ser futura. |
| `UpdateProfileDto` | O nome não pode ficar em branco. · Telefone inválido. |

### 5.2 Mensagens em inglês

| DTO | Mensagem |
|---|---|
| `MedicationCreateDto` | Name is required |
| `VaccineCreateDto` | Name is required |
| `AnimalMedicationCreateDto` | MedicationId is required |

### 5.3 Mensagens padrão do .NET (atributos sem `ErrorMessage`)

Todos os `[MaxLength(n)]` sem mensagem própria (campos `Notes`, `Name`, `Description`, `Manufacturer`, `PropertyTagNumber`, `Diagnosis`, `Responsible` etc.) e os atributos de `RegisterDto`, `CreateUserDto`, `ChangePasswordDto`, `DeleteAccountDto` e `UpdateUserDto` usam a mensagem padrão do framework, **em inglês**:

| Atributo | Mensagem padrão (exemplo) |
|---|---|
| `[Required]` | The Email field is required. |
| `[EmailAddress]` | The Email field is not a valid e-mail address. |
| `[MinLength(6)]` | The field Password must be a string or array type with a minimum length of '6'. |
| `[MaxLength(500)]` | The field Notes must be a string or array type with a maximum length of '500'. |

---

## 6. Erros do ASP.NET Identity (senha e usuário)

Retornados em `errors` (formato D) por `POST /api/auth/register`, `POST /api/users` e `PATCH /api/auth/me/password`. O projeto usa `AddIdentityCore` **sem customizar** `PasswordOptions` nem o `IdentityErrorDescriber`, então valem as regras e mensagens padrão (em inglês):

| Regra | Mensagem |
|---|---|
| Mínimo 6 caracteres | Passwords must be at least 6 characters. |
| Ao menos um caractere especial | Passwords must have at least one non alphanumeric character. |
| Ao menos um dígito | Passwords must have at least one digit ('0'-'9'). |
| Ao menos uma minúscula | Passwords must have at least one lowercase ('a'-'z'). |
| Ao menos uma maiúscula | Passwords must have at least one uppercase ('A'-'Z'). |

A senha atual errada em `PATCH /me/password` **não** vem nesse formato: é convertida para `422` "Senha atual incorreta." (formato A).

---

## 7. Inconsistências observadas

Registradas para correção futura. **Nenhuma foi alterada.**

| # | Inconsistência | Onde | Impacto |
|---|---|---|---|
| 1 | **Cinco formatos de corpo** de erro (§2) | Toda a API | O cliente precisa ler `error`, `message`, `errors` (objeto ou lista) e corpo vazio |
| 2 | **Mensagens em inglês** misturadas com português | `AnimalMedicationService` ("Animal not found", "Medication not found"); `MedicationCreateDto`/`VaccineCreateDto` ("Name is required"); `AnimalMedicationCreateDto`; atributos sem `ErrorMessage` (§5.3); Identity (§6) | Usuário vê textos em inglês |
| 3 | **`int.Parse` em id da rota** → `FormatException` → **`500`** | `WeightRecordService.FindAnimalAsync`, `AnimalMedicationService` (rotas com `{animalId}` sem `:int`) | Id inválido deveria ser `404`/`400`; o app trataria como erro temporário e tentaria de novo |
| 4 | **`404` sem mensagem** (`return NotFound()` com formato C) | `MedicationsController`, `VaccinesController`, `WeightRecordsController`, `AnimalMedicationsController` | Diferente dos demais `404` (formato A com mensagem) |
| 5 | **"Email já cadastrado." em dois formatos** | `AuthController.Register` (D, `message`) × `UsersController.Create` (A, `error`) | Mesmo erro, corpos diferentes |
| 6 | **`409` para "já está no estado pedido"** ("já está inativo", "já está seca", "já está ativo"...) | Vários services | Conflita com reenvio no offline; tratado por entidade no plano offline (A6). **Já corrigido em `MilkProduction`** (02/Out/2026) |
| 7 | **`500` com mensagem de negócio** ("Propriedade não encontrada.") | `AuthController.Login` | Situação de dado inconsistente reportada como erro de servidor |
| 8 | **Concordância** em "Cobertura com id '{id}' não encontrado." e "Apenas coberturas com diagnóstico pendente podem ser editados." | `BreedingEventService.UpdateAsync` | Texto |

---

## 8. Como o cliente deve tratar (resumo)

| Status | Web | App offline (fila) |
|---|---|---|
| `2xx` | Sucesso | Remove da fila e grava o retorno |
| `400` | Mostrar `errors` por campo (B) ou `errors` lista (D) | Erro definitivo → `Failed` |
| `401` | Redirecionar para login | Renovar token e repetir o item |
| `403` | Mostrar "sem permissão" | Erro definitivo → `Failed` |
| `404` | Mostrar `error` ou mensagem genérica | Erro definitivo → `Failed` |
| `409` | Mostrar `error` | Erro definitivo → `Failed` |
| `422` | Mostrar `error` | Erro definitivo → `Failed` |
| `5xx` / sem resposta | Mensagem genérica, permitir tentar de novo | Manter na fila, `Result.retry()`, com limite de tentativas |

**Extração da mensagem (ordem sugerida):** `error` → `message` (+ `errors` se lista) → `errors` (objeto, juntar as mensagens) → mensagem genérica pelo status.
