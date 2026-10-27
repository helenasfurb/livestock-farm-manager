# Catálogo de Erros da API — MuuBoi

**Data:** 02/Out/2026
**Escopo:** todos os erros que a API retorna **hoje** (estado do código em `main` + Fase 1 do offline), levantados a partir de `Application/Services`, `Api/Controllers`, `Api/Middleware/ExceptionMiddleware.cs`, `Application/DTOs` e `Program.cs`.
**Uso:** referência para o front-end web e para o app offline decidirem o que fazer com cada resposta.
**Atualização (26/Out/2026, Spec #23 — `spec-recuperacao-senha.md`):** rotas de recuperação de senha e confirmação de e-mail (§4.1), `register` sem JWT, `403` `EmailNotConfirmed` no login, `429` por limite de tentativas (§1, §8) e mensagens do Identity em português (§6).

> Os exemplos de JSON são ilustrativos: `traceId`, ids e datas variam a cada requisição.

---

## 1. Visão geral

| Status | Significado | Quem gera | Formato do corpo (§2) |
|---|---|---|---|
| `400 Bad Request` | Dado de entrada inválido | Validação automática dos DTOs; JSON malformado; Identity (senha fraca) | **B** (ProblemDetails de validação) ou **D** (`message` + `errors`) |
| `401 Unauthorized` | Sem token, token inválido/expirado, sessão invalidada ou credenciais erradas | Autenticação JWT; `AuthController` | **E** (vazio), **C** ou **D** |
| `403 Forbidden` | Sem permissão (não é Admin), usuário desativado no login ou e-mail ainda não confirmado no login | `[Authorize(Roles = "Admin")]`; `AuthController.Login` | **E** (vazio) ou **D** (com `code` no caso do e-mail) |
| `404 Not Found` | Recurso não existe (ou é de outra propriedade) | `NotFoundException`; `return NotFound()`; rota inexistente | **A**, **C** ou **E** |
| `405 Method Not Allowed` | Verbo HTTP errado para a rota | ASP.NET Core | **E** (vazio) |
| `409 Conflict` | Conflito de estado (duplicado, já inativo, já existe) | `ConflictException`; `AuthController.Register` | **A** ou **D** |
| `415 Unsupported Media Type` | Corpo sem `Content-Type: application/json` | ASP.NET Core | **C** |
| `422 Unprocessable Entity` | Regra de negócio violada | `BusinessRuleException` | **A** |
| `429 Too Many Requests` | Mais de 5 requisições em 15 min do mesmo IP na mesma rota | `AddRateLimiter` (política `AccountCodes`) em `register`, `forgot-password`, `reset-password`, `confirm-email`, `resend-confirmation` | **A** |
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

Usado diretamente no `AuthController` e em parte do `UsersController`. Quando vem do Identity, inclui `errors` (lista de textos). No `403` de e-mail não confirmado, inclui `code` para o cliente distinguir do "Usuário desativado." sem comparar textos.

```http
HTTP/1.1 401 Unauthorized

{ "message": "Credenciais inválidas." }
```

```http
HTTP/1.1 400 Bad Request

{
  "message": "Erro ao criar usuário.",
  "errors": [
    "A senha deve ter pelo menos um caractere especial (por exemplo, ! @ # $).",
    "A senha deve ter pelo menos uma letra maiúscula (A–Z)."
  ]
}
```

```http
HTTP/1.1 403 Forbidden

{ "message": "Confirme seu e-mail para entrar. Enviamos um código para você.", "code": "EmailNotConfirmed" }
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
| `POST /register` | 429 | A | Muitas tentativas. Tente novamente mais tarde. |
| `POST /login` | 401 | D | Credenciais inválidas. *(email não existe **ou** senha errada — mesma mensagem de propósito)* |
| `POST /login` | 403 | D | Usuário desativado. |
| `POST /login` | 403 | D | Confirme seu e-mail para entrar. Enviamos um código para você. + `"code": "EmailNotConfirmed"` *(só depois de a senha conferir; um código novo é enviado, no máximo um a cada 60 s)* |
| `POST /login` | 500 | D | Propriedade não encontrada. |
| `POST /forgot-password` | 429 | A | Muitas tentativas. Tente novamente mais tarde. *(fora isso, sempre `200` com a mesma mensagem, exista ou não a conta)* |
| `POST /reset-password` | 400 | D | Código inválido ou expirado. *(código errado, vencido ou já usado, e-mail inexistente, conta inativa ou 5 erros na conta em 15 min — mesma mensagem de propósito)* |
| `POST /reset-password` | 400 | D | Erro ao redefinir a senha. + `errors` do Identity (§6) *(só com o código certo)* |
| `POST /reset-password` | 429 | A | Muitas tentativas. Tente novamente mais tarde. |
| `POST /confirm-email` | 400 | D | Código inválido ou expirado. *(mesmos casos do `reset-password`)* |
| `POST /confirm-email` | 500 | D | Propriedade não encontrada. |
| `POST /confirm-email` | 429 | A | Muitas tentativas. Tente novamente mais tarde. |
| `POST /resend-confirmation` | 429 | A | Muitas tentativas. Tente novamente mais tarde. *(fora isso, sempre `200` com a mesma mensagem)* |
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

> **Membro novo (26/Out/2026, Spec #23):** `POST /` não envia e-mail; as respostas de membro trazem `emailConfirmed`. No 1º login com a senha temporária, o membro recebe `403` `EmailNotConfirmed` (§4.1).

### 4.3 Animais — `/api/animals`

| Rota | Status | Mensagem |
|---|---|---|
| `GET /{id}`, `PATCH /{id}`, `PATCH /{id}/exit`, `PATCH /{id}/reactivate`, `GET /{id}/exit-records` | 404 | Animal com id '{id}' não encontrado. |
| `POST /`, `PATCH /{id}` | 409 | Já existe um animal com o brinco '{brinco}' nesta propriedade. |
| `POST /` | 422 | A lactação inicial só se aplica a vacas e novilhas. |
| `GET /changes?since=` | 400 | Cursor de sincronização inválido. *(formato A — `since` não numérico ou negativo)* |
| `GET /?withoutBreed=true&breed=` | 400 | Informe a raça ou 'sem raça registrada', não os dois. *(formato B, campos `Breed` e `WithoutBreed`)* |
| `GET /{id}/vaccination-history` | 404 | Animal com id '{id}' não encontrado. |
| `GET /{id}/health-history` | 404 | Animal com id '{id}' não encontrado. |

> **Rotas com suporte offline** — cadastro, edição, saída e reativação (ver `Docs/Specs/spec-sincronizacao-offline-14.4-animais.md`). Repetir uma operação já aplicada **não é erro**:
> - `POST /` com `syncId` já existente → `201` com o animal existente (sem duplicar pesagem, ECC e lactação iniciais) — mesmo que o brinco já esteja em uso por ele ou que a lactação inicial seja inválida (o `syncId` é checado antes). Inclui o reenvio simultâneo ao original.
> - `POST /` com **outro** `syncId` e brinco em uso → continua `409` (conflito real).
> - `POST /` com `syncId` vazio (`00000000-...`) → `400` "O identificador de sincronização não pode ser vazio." (formato B, campo `SyncId`).
> - `POST /` com `initialWeightSyncId` vazio → `400` "O identificador de sincronização da pesagem inicial não pode ser vazio." (formato B, campo `InitialWeightSyncId`). Sem `initialWeight`, o `initialWeightSyncId` é ignorado, sem erro (Spec #14.5, P3).
> - `POST /` com `initialBodyConditionSyncId` vazio → `400` "O identificador de sincronização do ECC inicial não pode ser vazio." (formato B, campo `InitialBodyConditionSyncId`). Sem `initialBodyConditionScore`, é ignorado, sem erro (Spec #14.6, C3).
> - `POST /` com `initialLactationSyncId` vazio → `400` "O identificador de sincronização da lactação inicial não pode ser vazio." (formato B, campo `InitialLactationSyncId`). Sem `initialLactation`, é ignorado, sem erro. A resposta do cadastro traz `initialLactation { id, syncId }` (ou `null`), inclusive no reenvio (Spec #14.7, L3).
> - `POST /` com `initialLactation` sem `startDate` → `400` "A data de início da lactação é obrigatória." (campo `InitialLactation.StartDate`; até 11/Out/2026 gravava `0001-01-01`).
> - `PATCH /{id}` com `updatedAt` mais antigo que a versão do servidor → `200` com a versão do servidor (edição ignorada, **sem** checar o brinco).
> - `PATCH /{id}/exit` em animal já inativo → `200` com o animal, sem gravar *(até 09/Out/2026 era `409` "Não é possível registrar saída de um animal já inativo.")*.
> - `PATCH /{id}/reactivate` em animal já ativo → `200` com o animal, sem gravar *(até 09/Out/2026 era `409` "Não é possível reativar um animal que já está ativo.")*.
> - `GET /changes` **não** retorna erro para `limit` fora da faixa (ajustado para 500).
>
> **Correção (09/Out/2026):** a segunda saída de um animal reativado apagava fisicamente a saída anterior do histórico (`GET /{id}/exit-records`). Corrigido na Spec #14.4, Fase 4.

**Foto do animal** (21/Out/2026, `Docs/Specs/spec-sincronizacao-offline-14.12-foto-animal.md`):

| Rota | Status | Mensagem |
|---|---|---|
| `PUT /{id}/photo`, `DELETE /{id}/photo`, `GET /{id}/photo`, `GET /{id}/photo/thumbnail` | 404 | Animal com id '{id}' não encontrado. |
| `GET /{id}/photo`, `GET /{id}/photo/thumbnail` | 404 | O animal com id '{id}' não possui foto. |
| `PUT /{id}/photo` | 400 | O hash da foto não confere com o arquivo enviado. *(formato A)* |
| `PUT /{id}/photo` | 400 | O hash da miniatura não confere com o arquivo enviado. *(formato A)* |
| `PUT /{id}/photo` | 400 | Formato B: `Photo`/`Thumbnail` — "A foto é obrigatória." / "A miniatura é obrigatória." / "O arquivo deve ter no máximo 2048 KB." (foto) ou "... 200 KB." (miniatura) / "Envie uma imagem JPEG ou WebP." (tipo declarado diferente dos bytes iniciais); `PhotoHash`/`ThumbnailHash` — "O hash da ... é obrigatório." / "O hash da ... deve ser um SHA-256 em hexadecimal (64 caracteres)." |
| `PUT /{id}/photo` | 400 | Formato B, campo `""`: "Failed to read the request form. Request body too large. The max request body size is 3145728 bytes." *(corpo acima de 3 MB; mensagem do ASP.NET, em inglês)* |
| `PUT /{id}/photo` | 500 | Dois envios simultâneos para o mesmo animal (concorrência do `RowVersion`, como na §5.3 da Spec #14) ou Blob Storage indisponível/sem configuração. O app reenvia |

> **Repetir não é erro:** `PUT` da mesma foto (mesmo hash) → `200` sem gravar; `PUT` com `takenAt` mais antigo que a foto atual → `200` com a atual; `DELETE` sem foto, repetido ou com `updatedAt` mais antigo → `204` sem gravar. `GET .../photo` responde `302` (não é erro): o destino é o Blob, que responde `403` quando a SAS venceu (10 min) e `404` quando a foto foi trocada ou removida — o app pede a rota da API de novo.

Exemplo:

```http
POST /api/animals
{ "tagNumber": "123456", "gender": 2, "classification": 1, ... }
```

```http
HTTP/1.1 409 Conflict

{ "error": "Já existe um animal com o brinco '123456' nesta propriedade." }
```

### 4.4 Pesagens — `/api/animals/{animalId:int}/weight-records`

| Rota | Status | Formato | Mensagem |
|---|---|---|---|
| Todas | 404 | A | Animal com id '{animalId}' não encontrado. |
| `GET /{weightRecordId}`, `PATCH /{weightRecordId}`, `DELETE /{weightRecordId}` | 404 | A | Pesagem com id '{weightRecordId}' não encontrada. |
| Todas, com `animalId` ou `weightRecordId` não numérico | 404 | E | — (a rota não corresponde) |
| `POST /` sem `weight` | 400 | B | O peso é obrigatório. *(campo `Weight`; até 10/Out/2026 era `500`)* |
| `POST /` com `syncId` vazio | 400 | B | O identificador de sincronização não pode ser vazio. *(campo `SyncId`)* |
| `GET /api/animals/weight-records/changes?since=` | 400 | A | Cursor de sincronização inválido. *(`since` não numérico ou negativo)* |

> **Rotas com suporte offline** — cadastro, edição, exclusão e pull (ver `Docs/Specs/spec-sincronizacao-offline-14.5-pesagem.md`). Repetir uma operação já aplicada **não é erro**:
> - `POST /` com `syncId` já existente → `201` com a pesagem existente, sem alterar — inclusive por outro `animalId` na rota ou com o animal já inexistente (o `syncId` é checado antes). Inclui o reenvio simultâneo ao original.
> - `PATCH /{weightRecordId}` com `updatedAt` mais antigo que a versão do servidor → `200` com a versão do servidor (edição ignorada).
> - `PATCH /{weightRecordId}` em pesagem excluída → `200`, aplicado; a pesagem continua excluída.
> - `DELETE /{weightRecordId}` em pesagem já excluída → `204`, sem gravar *(até 10/Out/2026 a exclusão era física e a repetição dava `404` "Pesagem com id '...' não encontrada.")*.
> - `GET /{weightRecordId}` de pesagem excluída → `200` com `isActive: false` (antes, `404`). `GET /` lista só as ativas.
> - `GET /api/animals/weight-records/changes` **não** retorna erro para `limit` fora da faixa (ajustado para 500).
>
> **Formato (10/Out/2026):** `recordedAt` passou de `dd/MM/yyyy` para ISO completo (`2026-10-10T12:00:00`) em todas as respostas com pesagem, inclusive `weightRecords`/`lastWeightRecord` de `/api/animals` (Spec #14.5, P8).
>
> **Correção (10/Out/2026):** `PATCH /{weightRecordId}` sem `weight` ou sem `weightDate` gravava `0` / `0001-01-01` nesses campos. Corrigido na Spec #14.5, Fase 5.

### 4.5 Medicamentos do animal — `/api/animals/{animalId:int}/medications`

| Rota | Status | Formato | Mensagem |
|---|---|---|---|
| Todas | 404 | A | Animal com id '{animalId}' não encontrado. |
| `POST /`, `PATCH /{id}` | 404 | A | Medicamento com id '{medicationId}' não encontrado. |
| `GET /{id}`, `PATCH /{id}`, `DELETE /{id}` | 404 | A | Registro de medicação com id '{id}' não encontrado. |
| Todas, com `animalId` não numérico | 404 | E | — (a rota não corresponde) |

### 4.6 Catálogos: medicamentos e vacinas — `/api/medications`, `/api/vaccines`

| Rota | Status | Formato | Mensagem |
|---|---|---|---|
| `GET /api/medications/{id}`, `PATCH`, `DELETE` | 404 | A | Medicamento com id '{id}' não encontrado. |
| `GET /api/vaccines/{id}`, `PATCH`, `DELETE` | 404 | A | Vacina com id '{id}' não encontrada. |

### 4.7 ECC (condição corporal) — `/api/animals/{animalId}/body-condition-records`

| Rota | Status | Mensagem |
|---|---|---|
| `GET /`, `POST /`, `PATCH /{recordId}` | 404 | Animal com id '{animalId}' não encontrado. |
| `POST /` | 409 | Não é possível registrar ECC de um animal inativo. |
| `PATCH /{recordId}` | 404 | Registro de ECC com id '{recordId}' não encontrado. |
| `POST /`, `PATCH /{recordId}` | 422 | A data da avaliação não pode ser futura. |
| `POST /` sem `score` / sem `recordedAt` | 400 | O escore de condição corporal é obrigatório. / A data da avaliação é obrigatória. *(formato B; até 10/Out/2026 eram aceitos com escore `0` / data `0001-01-01`)* |
| `POST /`, `PATCH /{recordId}` com `score` fora de 1–5 | 400 | Valor inválido. Valores aceitos: Magreza Severa (1), …, Obesidade Severa (5). *(formato B, campo `Score`; até 10/Out/2026 era aceito)* |
| `POST /` com `syncId` vazio | 400 | O identificador de sincronização não pode ser vazio. *(formato B, campo `SyncId`)* |
| `GET /api/animals/body-condition-records/changes?since=` | 400 | Cursor de sincronização inválido. *(formato A — `since` não numérico ou negativo)* |

> **Rotas com suporte offline** — cadastro, edição e pull (ver `Docs/Specs/spec-sincronizacao-offline-14.6-ecc.md`). Não há exclusão de ECC. Repetir uma operação já aplicada **não é erro**:
> - `POST /` com `syncId` já existente → `201` com o ECC existente, sem alterar — inclusive depois da saída do animal ou com o animal inexistente (o `syncId` é checado antes). Inclui o reenvio simultâneo ao original.
> - `POST /` com **outro** `syncId` em animal inativo → continua `409` (conflito real).
> - `PATCH /{recordId}` com `updatedAt` mais antigo que a versão do servidor → `200` com a versão do servidor (edição ignorada). `PATCH` em ECC de animal inativo continua aceito.
> - `GET /api/animals/body-condition-records/changes` **não** retorna erro para `limit` fora da faixa (ajustado para 500).
>
> **Correção (10/Out/2026):** `score` e `recordedAt` ausentes ou fora do enum eram aceitos com `201`/`200` e gravavam dados inválidos. Corrigido na Spec #14.6, Fase 2 (C7).

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
| `POST`, `PATCH /{id}` | 404 | Touro com id '{id}' não encontrado. |
| `POST`, `PATCH /{id}` | 409 | O touro selecionado está inativo. |
| `POST`, `PATCH /{id}` | 422 | O animal informado como touro pai não possui classificação 'Touro'. |
| `PATCH /{id}` | 404 | Cobertura com id '{id}' não encontrada. |
| `PATCH /{id}` | 409 | Apenas coberturas com diagnóstico pendente podem ser editadas. |
| `PATCH /{id}/status` | 404 | Cobertura com id '{id}' não encontrada. |
| `PATCH /{id}/status` | 409 | O diagnóstico desta cobertura já foi registrado. |
| `PATCH /{id}/status` | 422 | A data do diagnóstico não pode ser anterior à data da cobertura. |
| `DELETE /{id}` | 404 | Cobertura com id '{id}' não encontrada. |
| `DELETE /{id}` | 409 | Esta cobertura possui uma gestação ativa vinculada. Inative a gestação primeiro. |
| `POST` | 400 | A data da cobertura é obrigatória. / O tipo de reprodução é obrigatório. / Valor inválido (tipo de reprodução fora do enum). *(formato B — antes, sem data gravava `0001-01-01` e tipo `0` ou `9` era gravado)* |
| `PATCH /{id}/status` | 400 | O status é obrigatório. / A data do diagnóstico é obrigatória. / Valor inválido (método fora do enum). *(formato B — antes, sem status gravava status `0` e sem data respondia `422` com a mensagem da data da cobertura)* |
| `PATCH /{id}/status` | 400 | O identificador da gestação só se aplica ao diagnóstico positivo. *(campo `PregnancySyncId`)* |
| `GET /api/breeding-events/changes?since=` | 400 | Cursor de sincronização inválido. *(formato A)* |

> **Rotas com suporte offline** — coberturas (ver `Docs/Specs/spec-sincronizacao-offline-14.10-cobertura-gestacao.md`). Repetir uma operação já aplicada **não é erro**:
> - `POST` com `syncId` já existente → `201` com a cobertura existente — mesmo que a vaca tenha sido vendida, o sêmen inativado ou a cobertura já esteja "aguardando diagnóstico" (o `syncId` é rechecado antes do `422`).
> - `PATCH /{id}/status` repetido com o **mesmo** resultado → `200` com o estado atual *(até 14/Out/2026 era `409` "O diagnóstico desta cobertura já foi registrado.")*; resultado **diferente** → `409` (mantido).
> - `PATCH /{id}` com `updatedAt` mais antigo → `200` com a versão do servidor.
> - `DELETE /{id}` em cobertura já inativa → `204` *(até 14/Out/2026 era `409` "A cobertura já está inativa.")*.
> - `syncId` / `pregnancySyncId` vazios → `400` "O identificador de sincronização … não pode ser vazio."
> - `GET /changes` **não** retorna erro para `limit` fora da faixa (ajustado para 500).
>
> **Mudança de regra (14/Out/2026):** a cobertura por IA **não** exige mais dose disponível — o `422` "Não há doses disponíveis para a amostra de sêmen selecionada." foi removido (saldo negativo permitido, Spec #14.10 B4).

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
| `DELETE /{id}` | 409 | Esta gestação possui um parto ativo vinculado. Inative o parto primeiro. |
| `POST` | 400 | A data de confirmação é obrigatória. *(formato B — antes, sem a data gravava `0001-01-01`)* |
| `PATCH /{id}/status` | 400 | A data da perda é obrigatória. / Valor inválido (status fora do enum). *(formato B — antes, sem a data gravava `0001-01-01`)* |
| `GET /api/pregnancies/changes?since=` | 400 | Cursor de sincronização inválido. *(formato A)* |

> **Rotas com suporte offline** — gestações (ver `Docs/Specs/spec-sincronizacao-offline-14.10-cobertura-gestacao.md`). Repetir uma operação já aplicada **não é erro**:
> - `POST` (retroativa) com `syncId` já existente → `201` com a gestação existente — mesmo que a vaca já tenha gestação ativa (o `syncId` é rechecado antes do `409`). O campo **`clientRequestId` (Spec #13) foi substituído por `syncId`** (14/Out/2026).
> - `PATCH /{id}/status` (perda) em gestação já perdida → `200` com o estado atual, sem gravar.
> - `DELETE /{id}` em gestação já inativa → `204` *(até 14/Out/2026 era `409` "A gestação já está inativa.")*.
> - A gestação do diagnóstico positivo é criada pela cobertura (`PATCH /api/breeding-events/{id}/status`, campo `pregnancySyncId`).

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
| `POST` | 409 | Não é possível registrar ECC de um animal inativo. *(com `bodyConditionScore` e mãe inativa — **até 16/Out/2026 o parto era gravado sem o ECC** antes deste erro; agora nada é gravado)* |
| `POST` | 400 | A data do parto é obrigatória. / O sexo da cria é obrigatório. / O status vital da cria é obrigatório. / Valor inválido (sexo, status vital ou ECC fora do enum). *(formato B — antes, sem data respondia `422` com a mensagem da confirmação, cria sem sexo virava macho e enums fora da faixa eram gravados)* |
| `POST` | 400 | O brinco principal deve ter exatamente 6 dígitos numéricos. *(campo `Calves[i].TagNumber` — mesma regra do cadastro de animal; antes aceitava até 6 caracteres quaisquer)* |
| `PATCH /api/calvings/{calvingId}/calves/{calfId}` | 400 | Valor inválido (sexo fora do enum). |
| `GET /api/calvings/changes?since=` | 400 | Cursor de sincronização inválido. *(formato A)* |

> **Rotas com suporte offline** — parto e crias (ver `Docs/Specs/spec-sincronizacao-offline-14.11-parto-crias.md`). Repetir uma operação já aplicada **não é erro**:
> - `POST` com `syncId` já existente → `201` com o parto existente — mesmo que a gestação já esteja "parto realizado", a lactação aberta e os brincos das crias já existam (o `syncId` é rechecado antes dos `409`/`422`).
> - O app envia o `syncId` do parto, da lactação (`lactationSyncId`), do ECC (`bodyConditionSyncId`) e, em cada cria, da cria (`syncId`), do animal (`animalSyncId`) e da pesagem de nascimento (`weightSyncId`). A resposta traz `{ id, syncId }` de cada derivado.
> - `PATCH .../calves/{calfId}` com `updatedAt` mais antigo que a cria → `200` com a versão do servidor; o reflexo no sexo do animal e no peso de nascimento também respeita o `updatedAt` de cada um.
> - `DELETE /api/calvings/{id}` em parto já inativo → `204` *(até 16/Out/2026 era `409` "O parto já está inativo.")*.
> - `syncId`s vazios → `400`; `animalSyncId`/`weightSyncId` em cria não viva (ou sem peso) e `bodyConditionSyncId` sem ECC → `400`.

### 4.11 Lactações — `/api/lactations`, `/api/animals/{animalId}/lactations`

| Rota | Status | Mensagem |
|---|---|---|
| `GET /api/animals/{animalId}/lactations`, `.../current`, `POST` | 404 | Animal com id '{animalId}' não encontrado. |
| `POST` | 409 | Não é possível cadastrar lactação para um animal inativo. |
| `POST` | 422 | Somente vacas e novilhas podem ter lactação. |
| `POST` | 409 | O animal já possui uma lactação em aberto. Seque a atual antes de abrir outra. |
| `GET /api/lactations/{id}`, `PATCH`, `POST /dry-off`, `DELETE /dry-off`, `DELETE` | 404 | Lactação com id '{id}' não encontrada. |
| `PATCH /{id}` | 422 | A data de início não pode ser posterior à data da secagem. |
| `POST /{id}/dry-off` | 422 | A data da secagem não pode ser anterior ao início da lactação. |
| `DELETE /{id}/dry-off` | 409 | O animal já possui outra lactação em aberto. |
| `POST` sem `startDate` / `POST /{id}/dry-off` sem `endDate` | 400 | A data de início da lactação é obrigatória. / A data da secagem é obrigatória. *(formato B; até 11/Out/2026, `startDate` ausente gravava `0001-01-01` e `endDate` ausente dava `422` com a mensagem errada)* |
| `POST` com `syncId` vazio | 400 | O identificador de sincronização não pode ser vazio. *(formato B, campo `SyncId`)* |
| `GET /api/lactations/changes?since=` | 400 | Cursor de sincronização inválido. *(formato A — `since` não numérico ou negativo)* |

> `GET /api/animals/{animalId}/lactations/current` responde **`204 No Content`** (não é erro) quando não há lactação aberta.
>
> **Rotas com suporte offline** — abertura, correção, secagem, desfazer secagem, exclusão e pull (ver `Docs/Specs/spec-sincronizacao-offline-14.7-lactacao.md`). Repetir uma operação já aplicada **não é erro**:
> - `POST` com `syncId` já existente → `201` com a lactação existente, sem alterar — inclusive quando ela própria é a "lactação em aberto" do animal, depois da saída do animal ou no reenvio simultâneo (Condição 3).
> - `POST` com **outro** `syncId` e lactação aberta → continua `409` (conflito real).
> - `PATCH /{id}` com `updatedAt` mais antigo que a versão do servidor → `200` com a versão do servidor (edição ignorada, **sem** checar a data da secagem).
> - `POST /{id}/dry-off` em lactação já seca → `200` com a secagem existente, sem gravar *(até 11/Out/2026: `409` "Esta lactação já está seca.")*.
> - `DELETE /{id}/dry-off` em lactação já aberta → `200`, sem gravar *(antes: `409` "Esta lactação já está em aberto.")*.
> - `DELETE /{id}` em lactação já inativa → `204`, sem gravar *(antes: `409` "A lactação já está inativa.")*.
> - `PATCH`, `POST /dry-off` e `DELETE /dry-off` em lactação **inativa** → `200` com a lactação (`isActive: false`), sem gravar *(antes: `409` "Não é possível editar/secar/reabrir uma lactação inativa.")* — a inativação sempre vence (Spec #14 §5.4).
> - Secar, desfazer secagem e inativar **não alteram `updatedAt`** (só a edição do `startDate` altera).
> - `GET /api/lactations/changes` **não** retorna erro para `limit` fora da faixa (ajustado para 500).

### 4.12 Produção de leite — `/api/milk-productions`

| Rota | Status | Mensagem |
|---|---|---|
| `GET /{id}`, `PATCH /{id}`, `DELETE /{id}` | 404 | Lançamento de produção de leite com id '{id}' não encontrado. |
| `GET /changes?since=` | 400 | Cursor de sincronização inválido. *(formato A — `since` não numérico ou negativo)* |

> `GET /changes` **não** retorna erro para `limit` fora da faixa: ausente, zero ou negativo vira 500; acima de 500 é limitado a 500.

> **Rota com suporte offline** (ver `Docs/Specs/spec-sincronizacao-offline.md`). Repetir uma operação já aplicada **não é erro**:
> - `POST` com `syncId` já existente → `201` com o registro existente.
> - `PATCH` com `updatedAt` mais antigo que a versão do servidor → `200` com a versão do servidor (edição ignorada).
> - `DELETE` em registro já inativo → `204` *(até 02/Out/2026 era `409` "O lançamento de produção de leite já está inativo.")*.
> - `POST` com `syncId` vazio (`00000000-...`) → `400` "O identificador de sincronização não pode ser vazio." (formato B, campo `SyncId`).

### 4.13 Sêmen — `/api/semen-samples`

| Rota | Status | Mensagem |
|---|---|---|
| `GET /{id}`, `PATCH /{id}`, `DELETE /{id}`, `PATCH /{id}/reactivate` | 404 | Amostra de sêmen com id '{id}' não encontrada. |
| `GET /changes?since=`, `GET /movements/changes?since=` | 400 | Cursor de sincronização inválido. *(formato A — `since` não numérico ou negativo)* |
| Rotas de `/{semenSampleId}/movements` | 404 | Amostra de sêmen com id '{semenSampleId}' não encontrada. |
| `GET`, `PATCH`, `DELETE /{semenSampleId}/movements/{movementId}` | 404 | Movimentação com id '{movementId}' não encontrada. |
| `POST /{semenSampleId}/movements` | 409 | Não é possível registrar movimentação para uma amostra de sêmen inativa. |
| `PATCH /{semenSampleId}/movements/{movementId}` | 409 | Movimentações geradas pelo sistema não podem ser editadas diretamente. |
| `DELETE /{semenSampleId}/movements/{movementId}` | 409 | Movimentações geradas pelo sistema não podem ser inativadas diretamente. *(vale também se ela já estiver inativa)* |

> **Rotas com suporte offline** — amostra e movimentações manuais (ver `Docs/Specs/spec-sincronizacao-offline-14.2-semen.md`, Partes A e B). Repetir uma operação já aplicada **não é erro**:
> - `POST` com `syncId` já existente → `201` com a amostra existente (sem duplicar a entrada inicial), incluindo `initialMovement` quando o `initialMovementSyncId` é reenviado.
> - `POST` com `initialMovementSyncId` vazio → `400` "O identificador de sincronização da entrada inicial não pode ser vazio." (formato B, campo `InitialMovementSyncId`).
> - `POST /{semenSampleId}/movements` com `syncId` já existente → `201` com a movimentação existente — mesmo que a amostra tenha sido inativada depois do primeiro envio.
> - `PATCH /{semenSampleId}/movements/{movementId}` com `updatedAt` mais antigo → `200` com a versão do servidor.
> - `DELETE /{semenSampleId}/movements/{movementId}` em movimentação manual já inativa → `204` *(até 06/Out/2026 era `409` "A movimentação já está inativa.")*.
> - `PATCH` com `updatedAt` mais antigo que a versão do servidor → `200` com a versão do servidor (edição ignorada).
> - `DELETE` em amostra já inativa → `200 false` *(até 06/Out/2026 era `409` "A amostra de sêmen já está inativa.")*.
> - `PATCH /{id}/reactivate` em amostra já ativa → `200 true` *(até 06/Out/2026 era `409` "A amostra de sêmen já está ativa.")*.
> - `POST` com `syncId` vazio (`00000000-...`) → `400` "O identificador de sincronização não pode ser vazio." (formato B, campo `SyncId`).
> - `GET /changes` **não** retorna erro para `limit` fora da faixa (ajustado para 500).

### 4.14 Estoque de insumos — `/api/stock-items`

| Rota | Status | Mensagem |
|---|---|---|
| `GET /{id}`, `PATCH /{id}`, `DELETE /{id}` | 404 | Insumo com id '{id}' não encontrado. |
| `POST`, `PATCH /{id}` | 404 | Categoria com id '{id}' não encontrada. |
| `POST`, `PATCH /{id}` | 404 | Unidade de medida com id '{id}' não encontrada. |
| `GET /changes?since=`, `GET /movements/changes?since=` | 400 | Cursor de sincronização inválido. *(formato A — `since` não numérico ou negativo)* |
| Rotas de `/{stockItemId}/movements` | 404 | Insumo com id '{stockItemId}' não encontrado. |
| `GET`, `PATCH`, `DELETE /{stockItemId}/movements/{movementId}` | 404 | Movimentação com id '{movementId}' não encontrada. |
| `POST /{stockItemId}/movements` | 409 | Não é possível registrar movimentação para um insumo inativo. |
| `PATCH /{stockItemId}/movements/{movementId}` | 422 | A data da movimentação não pode ser futura. |

> **Rotas com suporte offline** — insumo e movimentações (ver `Docs/Specs/spec-sincronizacao-offline-14.3-estoque.md`). Repetir uma operação já aplicada **não é erro**:
> - `POST` com `syncId` já existente → `201` com o insumo existente (sem duplicar o saldo inicial), incluindo `initialMovement` quando o `initialMovementSyncId` é reenviado — mesmo que a categoria/unidade enviadas sejam inválidas (o `syncId` é checado antes).
> - `POST` com `syncId` vazio (`00000000-...`) → `400` "O identificador de sincronização não pode ser vazio." (formato B, campo `SyncId`).
> - `POST` com `initialMovementSyncId` vazio → `400` "O identificador de sincronização da entrada inicial não pode ser vazio." (formato B, campo `InitialMovementSyncId`).
> - `PATCH /{id}` com `updatedAt` mais antigo que a versão do servidor → `200` com a versão do servidor (edição ignorada).
> - `DELETE /{id}` em insumo já inativo → `204` *(até 08/Out/2026 era `409` "O insumo já está inativo.")*.
> - `POST /{stockItemId}/movements` com `syncId` já existente → `201` com a movimentação existente, sem recalcular o custo — mesmo que o insumo tenha sido inativado depois do primeiro envio.
> - `POST /{stockItemId}/movements` com `syncId` vazio → `400` "O identificador de sincronização não pode ser vazio." (formato B, campo `SyncId`).
> - `PATCH /{stockItemId}/movements/{movementId}` com `updatedAt` mais antigo → `200` com a versão do servidor.
> - `DELETE /{stockItemId}/movements/{movementId}` em movimentação já inativa → `204` *(até 08/Out/2026 era `409` "A movimentação já está inativa.")*.
> - `GET /changes` e `GET /movements/changes` **não** retornam erro para `limit` fora da faixa (ajustado para 500).
>
> **Correção (08/Out/2026):** `PATCH /{id}` só com alguns campos (sem `stockCategoryId` e `unitOfMeasureId`) respondia `500` (violação de FK — o AutoMapper gravava `0` na categoria). Corrigido na Spec #14.3, Fase 3.

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
| `POST` | 400 | A vacina é obrigatória. *(formato B, campo `VaccineId` — antes, sem `vaccineId`, respondia `404` "Vacina com id '0' não encontrada.")* |
| `POST`, `PATCH /{id}` | 400 | Valor inválido. Valores aceitos: Primeira dose (1), Reforço (2). *(formato B, campo `DoseType` — antes, um `doseType` fora do enum era gravado)* |
| `POST /{id}/booster` | 400 | A data prevista é obrigatória. *(formato B, campo `PredictedDate` — antes, sem `predictedDate`, respondia `422` com a mensagem da data do pai)* |
| `GET /changes?since=` | 400 | Cursor de sincronização inválido. *(formato A — `since` não numérico ou negativo)* |

> **Rotas com suporte offline** — eventos e reforços (ver `Docs/Specs/spec-sincronizacao-offline-14.8-vacinacao.md`). Repetir uma operação já aplicada **não é erro**:
> - `POST` com `syncId` já existente → `201` com o evento existente e a lista **original** de animais — mesmo que a vacina tenha sido excluída depois ou que a lista enviada seja outra (o `syncId` é checado antes da vacina e dos animais).
> - `POST /{id}/booster` com `syncId` já existente → `201` com o reforço existente, inclusive quando ele próprio é o "reforço já existente" do pai e quando o pai foi inativado depois.
> - Dois reforços diferentes do mesmo pai ao mesmo tempo → um `201` e os demais `409` "Já existe um reforço para este evento." *(até 12/Out/2026 a corrida respondia `500`)*.
> - `POST` e `POST /{id}/booster` com `syncId` vazio (`00000000-...`) → `400` "O identificador de sincronização não pode ser vazio." (formato B, campo `SyncId`).
> - `PATCH /{id}` com `updatedAt` mais antigo que a versão do servidor → `200` com a versão do servidor (edição ignorada).
> - `DELETE /{id}` em evento já inativo → `204` *(até 12/Out/2026 era `409` "O evento de vacinação já está inativo.")*.
> - `GET /changes` **não** retorna erro para `limit` fora da faixa (ajustado para 500).

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
| `DELETE /{id}/medications/{medicationId}` | 404 | Aplicação com id '{medicationId}' não encontrada. |
| `DELETE /{id}/tests/{testId}` | 404 | Teste com id '{testId}' não encontrado. |
| `POST` | 400 | O animal é obrigatório. *(formato B, campo `AnimalId` — antes, sem `animalId`, respondia `404` "Animal com id '0' não encontrado.")* |
| `POST` | 400 | A data de diagnóstico é obrigatória. *(formato B, campo `DiagnosisDate` — antes, sem a data, gravava `0001-01-01`)* |
| `POST`, `PATCH /{id}` | 400 | Quartos afetados inválidos. *(formato B, campo `AffectedQuarters` — fora de 1–15; antes era gravado)* |
| `POST /{id}/medications` | 400 | A data de aplicação é obrigatória. *(formato B, campo `ApplicationDate` — antes, sem a data, gravava `0001-01-01`)* |
| `POST /{id}/tests` | 400 | A data do teste é obrigatória. *(formato B, campo `TestDate` — antes, sem a data, gravava `0001-01-01`)* |
| `GET /changes?since=` | 400 | Cursor de sincronização inválido. *(formato A — `since` não numérico ou negativo)* |

> **Rotas com suporte offline** — caso, aplicações e testes (ver `Docs/Specs/spec-sincronizacao-offline-14.9-casos-saude.md`). Repetir uma operação já aplicada **não é erro**:
> - `POST` com `syncId` já existente → `201` com o caso existente e os dados **originais** — mesmo que o animal tenha sido reclassificado depois (o `syncId` é checado antes do animal e da regra da mastite).
> - `POST /{id}/medications` e `POST /{id}/tests` com `syncId` já existente → `201` com a aplicação/teste existente (sem duplicar a carência) — mesmo que o caso tenha sido inativado depois.
> - `POST`, `POST /{id}/medications` e `POST /{id}/tests` com `syncId` vazio (`00000000-...`) → `400` "O identificador de sincronização não pode ser vazio." (formato B, campo `SyncId`).
> - `PATCH /{id}` com `updatedAt` mais antigo que a versão do servidor → `200` com a versão do servidor (edição ignorada, inclusive o encerramento).
> - `DELETE /{id}` em caso já inativo → `204` *(até 13/Out/2026 era `409` "O caso de saúde já está inativo.")*.
> - `DELETE /{id}/medications/{medicationId}` em aplicação já inativa → `204` *(até 13/Out/2026 era `409` "A aplicação já está inativa.")*.
> - `DELETE /{id}/tests/{testId}` em teste já inativo → `204` *(até 13/Out/2026 era `409` "O teste já está inativo.")*.
> - `GET /changes` **não** retorna erro para `limit` fora da faixa (ajustado para 500).
>
> O uso avulso de medicação (`/api/animals/{animalId}/medications`) **não** tem suporte offline e continua com exclusão física.

### 4.17 Somente leitura (sem erros de domínio)

`/api/dashboard`, `/api/dashboard/productive`, `/api/dashboard/reproductive`, `/api/stock/*`, `/api/stock-categories`, `/api/units-of-measure` e todos os endpoints de *lookup* (`/genders`, `/breeds`, `/milkings`, `/statuses` etc.) só retornam os erros transversais: `401`, `500`.

---

## 5. Erros de validação de DTO (`400`, formato B)

Disparados automaticamente antes do controller. A chave de `errors` é o nome do campo (ou `""` para validações de `IValidatableObject` sem campo definido).

### 5.1 Mensagens personalizadas (português)

| DTO | Mensagens |
|---|---|
| `AnimalCreateDto` | O identificador de sincronização não pode ser vazio. · O brinco principal é obrigatório. · O brinco principal deve ter exatamente 6 dígitos numéricos. · O sexo é obrigatório. · A classificação é obrigatória. · A classificação '{x}' é exclusiva de fêmeas. · A classificação '{x}' é exclusiva de machos. · A data do ECC inicial não pode ser futura. |
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
| `ForgotPasswordDto` · `ResendConfirmationDto` | O e-mail é obrigatório. · O e-mail informado é inválido. |
| `ResetPasswordDto` | O e-mail é obrigatório. · O e-mail informado é inválido. · O código é obrigatório. · O código deve ter 6 dígitos. · A nova senha é obrigatória. · A nova senha deve ter no mínimo 6 caracteres. |
| `ConfirmEmailDto` | O e-mail é obrigatório. · O e-mail informado é inválido. · O código é obrigatório. · O código deve ter 6 dígitos. |

### 5.2 Mensagens que eram em inglês *(corrigidas em 02/Out/2026)*

| DTO | Antes | Agora |
|---|---|---|
| `MedicationCreateDto` | Name is required | O nome é obrigatório. |
| `VaccineCreateDto` | Name is required | O nome é obrigatório. |
| `AnimalMedicationCreateDto` | MedicationId is required | O medicamento é obrigatório. |

### 5.3 Limites de tamanho, obrigatoriedade e formato *(corrigidos em 02/Out/2026)*

Os 98 atributos `[MaxLength]`, `[MinLength]`, `[Required]` e `[EmailAddress]` que usavam a mensagem padrão do .NET (em inglês) agora têm mensagem em português, com o nome do campo em português. Padrões:

| Atributo | Mensagem (exemplos) |
|---|---|
| `[MaxLength(n)]` | O nome deve ter no máximo 100 caracteres. · As observações devem ter no máximo 500 caracteres. · O brinco da fazenda deve ter no máximo 100 caracteres. |
| `[MinLength(6)]` | A senha deve ter no mínimo 6 caracteres. · A nova senha deve ter no mínimo 6 caracteres. |
| `[Required]` | O nome é obrigatório. · A senha atual é obrigatória. · O e-mail é obrigatório. |
| `[EmailAddress]` | O e-mail informado é inválido. |

> **Ainda em inglês:** o `title` do ProblemDetails ("One or more validation errors occurred.") e os erros de leitura do JSON ("A non-empty request body is required.", "The dto field is required.", "The JSON value could not be converted..."). Vêm do ASP.NET Core e só podem ser alterados no `Program.cs` (pendente de aprovação).

---

## 6. Erros do ASP.NET Identity (senha e usuário)

Retornados em `errors` (formato D) por `POST /api/auth/register`, `POST /api/users`, `PATCH /api/auth/me/password` e `POST /api/auth/reset-password`. As regras de senha são as padrão do Identity (`PasswordOptions` sem customização); as mensagens estão **em português desde 26/Out/2026** (`PortugueseIdentityErrorDescriber`, Spec #23 R10). Os **códigos** dos erros continuam os do Identity.

| Regra | Mensagem |
|---|---|
| Mínimo 6 caracteres | A senha deve ter no mínimo 6 caracteres. |
| Ao menos um caractere especial | A senha deve ter pelo menos um caractere especial (por exemplo, ! @ # $). |
| Ao menos um dígito | A senha deve ter pelo menos um número (0–9). |
| Ao menos uma minúscula | A senha deve ter pelo menos uma letra minúscula (a–z). |
| Ao menos uma maiúscula | A senha deve ter pelo menos uma letra maiúscula (A–Z). |

A senha atual errada em `PATCH /me/password` **não** vem nesse formato: é convertida para `422` "Senha atual incorreta." (formato A).

---

## 7. Inconsistências observadas

| # | Inconsistência | Onde | Situação |
|---|---|---|---|
| 1 | **Cinco formatos de corpo** de erro (§2) | Toda a API | ⏳ Pendente — unificar quebra o front-end web |
| 2 | **Mensagens em inglês** misturadas com português | Services, DTOs (§5.2, §5.3), Identity (§6), ASP.NET Core | ✅ **Corrigido** nos services e nos 98 atributos de DTO (02/Out/2026) e no Identity (§6, 26/Out/2026). ⏳ Pendente (exige `Program.cs`): mensagens do próprio ASP.NET Core |
| 3 | **`int.Parse` em id da rota** → `FormatException` → **`500`** | Pesagens e medicamentos do animal | ✅ **Corrigido** (02/Out/2026): ids tipados como `int` de ponta a ponta, rotas com `{animalId:int}`/`{weightRecordId:int}`; id inválido → `404` |
| 4 | **`404` sem mensagem** e `null` como sinal de erro (viola o `CLAUDE.md`) | Medicamentos, vacinas, pesagens, medicamentos do animal | ✅ **Corrigido** (02/Out/2026): services lançam `NotFoundException` com mensagem; `Delete` retorna `Task<bool>` |
| 5 | **"Email já cadastrado." em dois formatos** | `AuthController.Register` (D, `message`) × `UsersController.Create` (A, `error`) | ⏳ Pendente — corrigir muda o corpo lido pelo front-end web no cadastro |
| 6 | **`409` para "já está no estado pedido"** ("já está inativo", "já está seca", "já está ativo"...) | Vários services | Tratado por entidade no plano offline (A6). ✅ Corrigido em `MilkProduction`, vacinas, sêmen, estoque e animais (saída/reativação); ⏳ demais entidades quando ficarem sincronizáveis |
| 7 | **`500` com mensagem de negócio** ("Propriedade não encontrada.") | `AuthController.Login` | Mantido: é de fato uma inconsistência de dados no servidor |
| 8 | **Concordância** em "Cobertura com id '{id}' não encontrado." e "... podem ser editados." | `BreedingEventService.UpdateAsync` | ✅ **Corrigido** (02/Out/2026) |
| 9 | **`ExceptionMiddleware` registrado depois da autenticação** — exceções na validação da sessão escapam dele | `Program.cs:177–180` | ⏳ Pendente — exige mudança no `Program.cs` |

---

## 8. Como o cliente deve tratar (resumo)

| Status | Web | App offline (fila) |
|---|---|---|
| `2xx` | Sucesso | Remove da fila e grava o retorno |
| `400` | Mostrar `errors` por campo (B) ou `errors` lista (D) | Erro definitivo → `Failed` |
| `401` | Redirecionar para login | Renovar token e repetir o item |
| `403` | Mostrar "sem permissão". No login, com `code = "EmailNotConfirmed"`, abrir a tela de confirmação de e-mail | Erro definitivo → `Failed` |
| `404` | Mostrar `error` ou mensagem genérica | Erro definitivo → `Failed` |
| `409` | Mostrar `error` | Erro definitivo → `Failed` |
| `422` | Mostrar `error` | Erro definitivo → `Failed` |
| `429` | Mostrar `error` e desabilitar o envio por alguns minutos | Não se aplica — as rotas com limite (conta/autenticação) ficam fora da fila offline |
| `5xx` / sem resposta | Mensagem genérica, permitir tentar de novo | Manter na fila, `Result.retry()`, com limite de tentativas |

**Extração da mensagem (ordem sugerida):** `error` → `message` (+ `errors` se lista) → `errors` (objeto, juntar as mensagens) → mensagem genérica pelo status.
