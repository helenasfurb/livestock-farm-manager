# Plano de Implementação: Suporte Offline — Produção de Leite (`MilkProduction`)

**Data:** 02/Out/2026
**Status:** 🚧 Em implementação — Fases 1 a 6 concluídas (02/Out/2026); próxima: Fase 7 (documentação)

| Fase | Status |
|---|---|
| 1 — Domínio e banco | ✅ Concluída (migração `20261002224240_Offline_MilkProduction_SyncId_RowVersion` aplicada) |
| 2 — Criação idempotente | ✅ Concluída e validada |
| 3 — Edição com last-write-wins | ✅ Concluída e validada (com helpers reutilizáveis, §4.0) |
| 4 — Inativação idempotente | ✅ Concluída e validada |
| 5 — Pull incremental | ✅ Concluída e validada |
| 6 — Testes | ✅ Concluída — 19 testes passando |
| 7 — Documentação | ⏳ |
| 8 — Validação manual | Feita por fase (2 e 3) |
**Relaciona-se com:** Spec #14 (Sincronização Offline), Spec 11.1 (Produção de Leite, §8), `Docs/Plan/analise-sincronizacao-offline.md`

---

## 1. Decisão de arquitetura (registrada)

| # | Decisão |
|---|---------|
| A1 | **Não há rota de sincronização em lote.** O app envia as operações pelas **rotas REST normais** de cada entidade (`POST`, `PATCH`, `DELETE`), que passam a suportar reenvio. |
| A2 | **O cliente ordena e envia uma requisição por vez**, na ordem em que as operações foram feitas. Um único worker esvazia a fila. |
| A3 | **Falha para a fila.** Erro temporário (timeout, sem rede, `5xx`, `401`) → para e tenta de novo depois. Erro definitivo (`400`, `404`, `409`, `422`) → para, marca o item como `Failed` e avisa o usuário. |
| A4 | **Identidade gerada no cliente:** `SyncId` (`Guid`, preferencialmente UUID v7), criado **quando o usuário salva** e nunca regenerado. A PK `int` continua sendo a identidade interna. |
| A5 | **Referências entre entidades usam o `Id` do servidor**, resolvido pelo app no momento do envio (o `201` devolve o `Id`). As rotas continuam recebendo `int`. |
| A6 | **Reenvio é sucesso, não erro.** Repetir uma operação já aplicada devolve `2xx` com o estado atual do recurso. |
| A7 | **Conflito: last-write-wins** pelo momento da edição informado pelo cliente (Spec #14, D2). |
| A8 | **Pull incremental por `rowversion`** (Spec #14, D4), com rota de mudanças por recurso. |

> A1/A2 **substituem** o push em lote (`POST /api/sync/changes`) proposto na Spec #14 §5.2. A Spec #14 precisa ser revisada (ver Fase 7).

### Por que A6 ("reenvio é sucesso")

O app **não consegue distinguir** "minha requisição falhou" de "minha requisição deu certo, mas a resposta se perdeu". Quando a primeira tentativa foi gravada, a operação **deu certo**; responder com erro seria informar algo falso ao app. Se o reenvio retornasse `409`:

1. **A fila travaria sem motivo** — `409` é erro definitivo; o item viraria `Failed` e o usuário seria avisado de um erro que não aconteceu.
2. **O app ficaria sem o `Id`** — pela decisão A5, ele precisa do `Id` para enviar os itens dependentes; uma resposta de erro não traz o recurso.
3. **Ambiguidade** — o `409` do MuuBoi também significa conflitos reais (ex.: outra gestação ativa). O app não conseguiria separar "já recebi" de "conflito de verdade".

Com `2xx` + estado atual, a lógica do app tem **uma regra só**: remove da fila, grava o retorno e segue. É o comportamento da Stripe (mesma chave → mesma resposta), do WatermelonDB (*"MUST NOT return an error code"*) e o objetivo declarado do draft IETF `Idempotency-Key` (*"fault-tolerant"*).

**Observações:**
- A RFC 9110 define idempotência pelo **efeito no servidor**, não pelo status; responder `2xx` é uma **escolha de projeto** para simplificar o cliente.
- **Conflitos reais continuam sendo erro.** A regra só vale quando **a mesma operação** já foi aplicada (mesmo `SyncId` na criação; mesmo estado pedido numa mudança de estado). Um animal com o mesmo brinco e **outro** `SyncId` continua `409`.
- **Efeito colateral aceito:** numa mudança de estado, "já está no estado pedido" pode ter sido causado por outra pessoa (ex.: a web secou a lactação em 08/09 e o celular pede secagem em 10/09). O servidor responde `200` com o estado real (08/09) e o app atualiza o local com ele.

---

## 2. Por que começar por `MilkProduction`

- **Sem dependências:** não tem `AnimalId` nem outra FK (Spec 11.1, D1). Não exige a resolução de `Id` da decisão A5.
- **Sem saldo ou estado:** os lançamentos são fatos que apenas se somam (Spec 11.1, D2). Não há regra de unicidade por dia, então o único risco de reenvio é **duplicar o registro**.
- **Já tem a base pronta:** filtro de tenant (`HasQueryFilter`, `ApplicationDbContext.cs:219`), preenchimento automático de `PropertyId` no `SaveChangesAsync`, soft delete e índice `(PropertyId, Date)`.
- A Spec 11.1 §8 já previa essa evolução de forma aditiva: uma coluna + criação idempotente.

> **`Lactation` fica de fora desta fase.** Ela depende de `Animal` (`AnimalId`), então só entra depois que `Animal` suportar offline.

---

## 3. Visão geral das mudanças

| Rota | Hoje | Depois |
|---|---|---|
| `POST /api/milk-productions` | Sempre cria | Aceita `SyncId`. Se já existir → devolve o existente (sem criar). |
| `PATCH /api/milk-productions/{id}` | Aplica e carimba `UpdatedAt = agora` | Aceita `UpdatedAt` do cliente. Aplica só se for mais recente (LWW). |
| `DELETE /api/milk-productions/{id}` | `409` se já inativo | `204` se já inativo (idempotente). |
| `GET /api/milk-productions/changes?since=` | — | **Nova.** Mudanças desde o cursor, incluindo inativados. |
| Demais `GET` | — | Sem mudança. |

**Não muda:** `Program.cs` e DI (nenhum serviço novo), `ExceptionMiddleware`, AutoMapper do PATCH (continua manual).

---

## 4. Fases de implementação

### 4.0 Helpers reutilizáveis de sincronização (implementados na Fase 3)

Toda entidade sincronizável repete a mesma configuração, a mesma inserção idempotente e a mesma resolução de horário do LWW. Isso foi extraído em três helpers, que **as próximas entidades devem usar**:

| Helper | Camada | Métodos | Uso |
|---|---|---|---|
| `SyncableModelBuilderExtensions` | `Infrastructure/Data` | `ConfigureSyncable<T>()` | No `ApplicationDbContext`: `builder.Entity<T>().ConfigureSyncable();` — default `NEWID()` no `SyncId`, índice único `UX_{Tabela}_SyncId`, `IsRowVersion()` e índice `IX_{Tabela}_PropertyId_RowVersion`. Exige `T : ISyncable, ITenantEntity`. |
| `SyncableDbContextExtensions` | `Infrastructure/Data` | `FindBySyncIdAsync<T>(syncId)` · `AddSyncableAsync<T>(entity)` | No repositório: busca por `SyncId` (respeita o filtro de tenant) e inserção que, se o índice único reclamar (corrida original × reenvio), devolve o registro existente. |
| `SyncTimestampResolver` | `Application/Helpers` | `ResolveEditedAt(clientUpdatedAt, now)` · `IsOutdated(editedAt, entity)` | No service (`UpdateAsync`): normaliza o `updatedAt` do cliente para UTC, limita a "agora" e diz se a edição é mais antiga que a versão do servidor. Recebe `now` por parâmetro (testável). |
| `SyncPaging` *(Fase 5)* | `Application/Helpers` | `ResolveLimit` · `TryDecodeCursor` · `ToRowVersionBytes` · `FromRowVersionBytes` · `BuildPage` | No service (`GetChangesAsync`): cursor, limite e montagem da página do pull. |
| `GetChangesSinceAsync<T>` *(Fase 5)* | `Infrastructure/Data/SyncableDbContextExtensions` | — | No repositório: consulta do pull com `MIN_ACTIVE_ROWVERSION()`, respeitando o tenant. |

> ⚠️ **Convenção obrigatória:** o índice único do `SyncId` **precisa** se chamar `UX_{Tabela}_SyncId` — o `AddSyncableAsync` identifica a violação por esse nome. Usar `ConfigureSyncable()` garante isso.

**Receita para tornar uma nova entidade sincronizável (lado servidor):**

1. Implementar `ISyncable` no modelo (`SyncId`, `RowVersion`).
2. `builder.Entity<T>().ConfigureSyncable();` no `ApplicationDbContext`.
3. Migração (⚠️ aprovação).
4. Repositório: `GetBySyncIdAsync` → `_context.FindBySyncIdAsync<T>(syncId)`; `CreateAsync` → `_context.AddSyncableAsync(entity)`.
5. DTO de criação: `Guid? SyncId` + validação de UUID vazio. DTO de saída: `Guid SyncId`. AutoMapper: ignorar `SyncId` e `RowVersion` na criação.
6. Service `CreateAsync`: checar `SyncId` **antes** das regras de negócio; `entity.SyncId = dto.SyncId ?? Guid.NewGuid()`.
7. DTO de edição: `DateTime? UpdatedAt`. Service `UpdateAsync`: `ResolveEditedAt` + `IsOutdated` antes de aplicar; gravar `UpdatedAt = editedAt`.
8. Mudanças de estado repetidas (inativar etc.) → `2xx` em vez de `ConflictException` (A6).
9. Repositório: `GetChangesAsync` → `_context.GetChangesSinceAsync<T>(since, take)`. Service: `SyncPaging.TryDecodeCursor` (`400` se inválido) → `ResolveLimit` → repositório com `take + 1` → `SyncPaging.BuildPage`. Controller: `[HttpGet("changes")]` (Fase 5).

### Fase 1 — Domínio e banco

**1.1 Interface `ISyncable`** — `Domain/Models/ISyncable.cs` (novo)

```csharp
namespace MuuBoi.Domain.Models
{
    public interface ISyncable
    {
        Guid SyncId { get; set; }
        byte[] RowVersion { get; set; }
    }
}
```

Interface separada em vez de colocar em `BaseEntity`, para incluir as entidades uma a uma (Spec #14 §4.1).

**1.2 `MilkProduction` implementa `ISyncable`** — `Domain/Models/MilkProduction.cs`

```csharp
public class MilkProduction : BaseEntity, ITenantEntity, ISyncable
{
    ...
    public Guid SyncId { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
```

**1.3 Configuração EF** — `Infrastructure/Data/ApplicationDbContext.cs`, bloco de `MilkProduction`

```csharp
builder.Entity<MilkProduction>()
    .Property(m => m.SyncId)
    .HasDefaultValueSql("NEWID()");

builder.Entity<MilkProduction>()
    .HasIndex(m => m.SyncId)
    .IsUnique()
    .HasDatabaseName("UX_MilkProductions_SyncId");

builder.Entity<MilkProduction>()
    .Property(m => m.RowVersion)
    .IsRowVersion();

builder.Entity<MilkProduction>()
    .HasIndex(m => new { m.PropertyId, m.RowVersion })
    .HasDatabaseName("IX_MilkProductions_PropertyId_RowVersion");
```

- `NEWID()` como default preenche automaticamente os **registros existentes** (backfill) e os criados pela web sem `SyncId`.
- A coluna `rowversion` é preenchida pelo próprio SQL Server, inclusive nas linhas existentes.
- `IsRowVersion()` também ativa a **checagem de concorrência otimista** do EF: dois `UPDATE` simultâneos na mesma linha geram `DbUpdateConcurrencyException` (decisão de tratamento na Fase 3.3).

> ✅ **Implementado.** Na Fase 3 essas quatro configurações foram substituídas por `builder.Entity<MilkProduction>().ConfigureSyncable();` (§4.0). O EF confirmou que o modelo ficou idêntico (`has-pending-model-changes` → sem mudanças).

**1.4 Migração** ⚠️ **requer aprovação antes de criar** (regra do `CLAUDE.md`)

```bash
dotnet ef migrations add Offline_MilkProduction_SyncId_RowVersion --project MuuBoi
```

Conferir no arquivo gerado:
- `SyncId uniqueidentifier NOT NULL DEFAULT NEWID()`;
- índice único `UX_MilkProductions_SyncId`;
- `RowVersion rowversion NOT NULL`;
- nenhuma outra tabela alterada.

> ✅ **Aplicada em 02/Out/2026.** Conferido: os 2 registros existentes receberam `SyncId` distintos e `RowVersion` preenchido.

---

### Fase 2 — Criação idempotente (`POST`)

**2.1 DTO de entrada** — `Application/DTOs/MilkProductionCreateDto.cs`

```csharp
public Guid? SyncId { get; set; }
```

- **Opcional:** o app sempre envia; a web pode omitir (o servidor gera).
- Validar `SyncId != Guid.Empty` quando informado (`400`).

**2.2 DTO de saída** — `Application/DTOs/MilkProductionDto.cs`

```csharp
public Guid SyncId { get; set; }
```

O app precisa do par `SyncId` → `Id` para gravar o `ServerId` local.

**2.3 AutoMapper** — `Application/Mappings/MilkProductionProfile.cs`

- `MilkProductionCreateDto → MilkProduction`: ignorar `RowVersion`. O `SyncId` é definido no service (2.5).
- `MilkProduction → MilkProductionDto`: o `SyncId` mapeia por convenção.

**2.4 Repositório** — `IMilkProductionRepository` + `MilkProductionRepository`

```csharp
Task<MilkProduction?> GetBySyncIdAsync(Guid syncId);
```

- Usa o filtro de tenant global (não precisa de filtro manual).
- **Corrida entre envio original e reenvio:** no `CreateAsync`, capturar `DbUpdateException` cuja `InnerException` seja `SqlException` com `Number` 2601 ou 2627 (violação de índice único) **no índice `UX_{Tabela}_SyncId`**. Nesse caso, desanexar a entidade (`Entry(...).State = Detached`) e devolver o registro existente; se não for encontrado, relançar com `throw;` (preserva o stack trace). Assim o serviço não precisa conhecer detalhes do SQL Server.

> ✅ **Implementado** — desde a Fase 3, via helpers genéricos: `GetBySyncIdAsync` → `_context.FindBySyncIdAsync<MilkProduction>(syncId)`; `CreateAsync` → `_context.AddSyncableAsync(milkProduction)` (§4.0).

**2.5 Serviço** — `MilkProductionService.CreateAsync`

```csharp
public async Task<MilkProductionDto> CreateAsync(MilkProductionCreateDto dto)
{
    if (dto.SyncId.HasValue)
    {
        var existing = await _repository.GetBySyncIdAsync(dto.SyncId.Value);
        if (existing != null)
            return _mapper.Map<MilkProductionDto>(existing);
    }

    var production = _mapper.Map<MilkProduction>(dto);
    production.SyncId = dto.SyncId ?? Guid.NewGuid();
    var created = await _repository.CreateAsync(production);
    return _mapper.Map<MilkProductionDto>(created);
}
```

Regras:
- A checagem do `SyncId` vem **antes** de qualquer regra de negócio.
- Mesmo `SyncId` com payload diferente → **devolve o existente sem alterar**. Alterações posteriores chegam pelo `PATCH` que vem depois na fila.
- `SyncId` de um registro **inativo** → devolve o registro inativo (sem reativar).

**2.6 Controller** — sem mudança. O reenvio também responde `201` com o mesmo corpo, como faz a Stripe ("mesma chave, mesma resposta"). O app trata qualquer `2xx` como sucesso.

**2.7 Resultado da validação (02/Out/2026)**

| Cenário | Resultado |
|---|---|
| `POST` com `syncId` | ✅ `201`, `Id` novo |
| Mesmo `POST` repetido | ✅ `201`, mesmo `Id` |
| Mesmo `syncId`, payload diferente | ✅ `201`, devolve o existente sem alterar |
| `POST` sem `syncId` (web) | ✅ `201`, `syncId` gerado |
| `syncId` vazio | ✅ `400` "O identificador de sincronização não pode ser vazio." |
| 83 envios simultâneos (4 `syncId`) | ✅ Todos `201`; 1 linha por `syncId` no banco |

- O `catch` da corrida **não chegou a ser exercitado**: todos os reenvios simultâneos foram barrados antes, na checagem do service. Está comprovado que não há duplicata; não está comprovado o retorno do existente via `catch` (exigiria teste de integração com banco real).
- **Diferença de formato no reenvio:** `volume` `120.5` × `120.50` e `createdAt` com × sem `Z`. Mesmo valor; o reenvio lê do banco. A data sem `Z` já acontece em **todos os `GET`** da API (o EF devolve `DateTimeKind.Unspecified`) — tarefa separada, para a API inteira.

---

### Fase 3 — Edição com last-write-wins (`PATCH`)

**3.1 DTO** — `Application/DTOs/MilkProductionUpdateDto.cs`

```csharp
public DateTime? UpdatedAt { get; set; }
```

É o momento em que o usuário **editou** no celular. Opcional: a web omite e o servidor usa "agora".

- **Sem validação de "data futura"**, de propósito: um celular com relógio adiantado receberia `400` (definitivo) e a fila travaria. O valor é **limitado a "agora"** no service.
- **Contrato do app: enviar sempre em UTC, com `Z`** (ex.: `"2026-10-02T17:30:00Z"`). O servidor normaliza outros formatos (3.2), mas o formato sem fuso é ambíguo.

**3.2 Serviço** — `MilkProductionService.UpdateAsync` (implementado com o helper `SyncTimestampResolver`, §4.0)

```csharp
var editedAt = SyncTimestampResolver.ResolveEditedAt(dto.UpdatedAt, DateTime.UtcNow);
if (SyncTimestampResolver.IsOutdated(editedAt, production))
    return _mapper.Map<MilkProductionDto>(production);

// aplica campos como antes
production.UpdatedAt = editedAt;
```

`ResolveEditedAt` normaliza o fuso e limita a "agora":

| `updatedAt` recebido | `DateTimeKind` | Ação |
|---|---|---|
| `...Z` | `Utc` | Usa como está |
| `...-03:00` | `Local` (convertido para o fuso do servidor) | `ToUniversalTime()` |
| Sem fuso | `Unspecified` | Assume UTC (contrato) |
| Depois de "agora" | — | Limita a "agora" |
| Ausente (web) | — | "Agora" |

**Por que normalizar:** o `DateTime` do .NET compara só o número de *ticks*, ignorando o `Kind`. Um `14:30-03:00` seria lido como `14:30` (horário local) e comparado com valores UTC do banco (`17:30`), descartando como "antigas" edições feitas até 3 horas depois de outra.

`IsOutdated` compara com `UpdatedAt ?? CreatedAt` (registro nunca editado usa a criação).

- **Edição mais antiga que a do servidor** → não aplica e devolve `200` com a versão do servidor. O app sobrescreve o local com ela.
- **Reenvio da mesma edição** → `editedAt == current` → aplica de novo os mesmos valores (sem efeito). Por isso a comparação é `<`, e não `<=`.
- **Relógio do celular adiantado** → limitado a "agora" (`editedAt < now`), para que um celular com data errada não vença todas as edições futuras.
- O `UpdatedAt` guardado passa a ser o **momento da edição**, e não o da gravação. O pull **não** usa `UpdatedAt` (usa `RowVersion`), então isso não afeta a sincronização.

**3.3 Concorrência** — com `IsRowVersion()`, um `UPDATE` concorrente gera `DbUpdateConcurrencyException`.

> **Decisão (02/Out/2026): sem tratamento — o retry é do cliente.** A exceção vira `500` (temporário); o app tenta de novo, o servidor relê o registro, refaz a comparação do LWW e aplica ou descarta. A proposta original ("recarregar e repetir no repositório") foi descartada: o repositório não sabe quais campos reaplicar nem como refazer o LWW, e o service não deve depender de exceções do EF. Converter em `409` foi rejeitado porque, para o app, `409` é definitivo. Caso raro: exige duas edições do mesmo registro no mesmo instante.

**3.4 Rota** — continua `PATCH /api/milk-productions/{id:int}`. O app resolve o `Id` pelo `ServerId` local (A5). Para registros criados offline, o `POST` vem antes na fila e já gravou o `ServerId`.

**3.5 Resultado da validação (02/Out/2026)**

| Cenário | Resultado |
|---|---|
| `updatedAt` com fuso `-03:00` | ✅ `20:29:07-03:00` gravado como `23:29:07Z`; aplicado |
| `updatedAt` UTC mais novo | ✅ Aplicado |
| Reenvio idêntico | ✅ `200`, mesmo resultado |
| Edição mais antiga | ✅ `200`, ignorada; devolve a versão do servidor |
| Web sem `updatedAt` | ✅ Aplicado com "agora" |
| Relógio adiantado 1 dia | ✅ Aplicado; `updatedAt` limitado a "agora" |
| Reenvio do `POST` via helper genérico | ✅ Mesmo `Id`, 1 linha |

**3.6 Riscos residuais do LWW por horário**

| Situação | Consequência |
|---|---|
| Relógio do celular **atrasado** | As edições dele perdem sempre (o servidor não distingue de uma edição offline legítima). Risco aceito (Spec #14, D2). |
| Relógio do celular adiantado | Mitigado: limitado a "agora". |

Alternativa mais robusta (evolução futura): **controle por versão** — o app envia o `RowVersion` da versão que editou; se o servidor já estiver em outra versão, é conflito real. Elimina a dependência de relógio, mas exige estratégia de resolução de conflito (perguntar ao usuário ou merge por campo). A coluna `RowVersion` já existe.

---

### Fase 4 — Inativação idempotente (`DELETE`)

`MilkProductionService.DeactivateAsync`:

```csharp
if (!production.IsActive)
    return true;
```

- Substitui `throw new ConflictException("O lançamento de produção de leite já está inativo.")`.
- O controller continua respondendo `204`.
- Isso também muda o comportamento para a web: inativar duas vezes deixa de dar erro. É coerente com a semântica do `DELETE` no HTTP (idempotente).
- **Exceção ao `CLAUDE.md`**, que manda usar `ConflictException` quando a entidade "já está no estado pedido". Registrar a exceção para as rotas offline (Fase 7).
- **O reenvio não grava nada:** `UpdatedAt` e `RowVersion` ficam iguais, para o registro não "reaparecer" como alterado no pull (Fase 5).

> **Decisão (02/Out/2026): o `DELETE` não passa pelo last-write-wins — a inativação sempre vence.** O `DELETE` não leva `updatedAt`; se o celular inativar offline na segunda e a web editar na terça, a sincronização de sexta inativa o registro (com a edição da web preservada nos campos). Motivos: excluir é intenção explícita do usuário, e com soft delete nada se perde (dá para reativar). A alternativa (`DELETE` com `updatedAt`) exigiria corpo no `DELETE` ou parâmetro na URL e criaria o caso estranho de um registro excluído que "volta". `PATCH` em registro inativo continua aceito (edita sem reativar).

**Resultado da validação (02/Out/2026)**

| Cenário | Resultado |
|---|---|
| `DELETE` em registro ativo | ✅ `204`; `IsActive=0`, `RowVersion` 38026 → 38027 |
| `DELETE` repetido | ✅ `204`; `UpdatedAt` e `RowVersion` (38027) **inalterados** |
| `DELETE` em id inexistente | ✅ `404` "Lançamento de produção de leite com id '999999' não encontrado." |

---

### Fase 5 — Pull incremental (`GET /changes`)

**5.1 DTO** — `Application/DTOs/SyncPageDto.cs` (novo, genérico e reutilizável)

```csharp
public class SyncPageDto<T>
{
    public IEnumerable<T> Items { get; set; } = new List<T>();
    public string NextCursor { get; set; } = string.Empty;
    public bool HasMore { get; set; }
}
```

Os itens reutilizam o `MilkProductionDto` (que já tem `Id`, `IsActive`, `UpdatedAt` e agora `SyncId`).

**5.2 Rota** — `MilkProductionsController`

```
GET /api/milk-productions/changes?since={cursor}&limit=500
```

- `since` ausente → carga inicial completa (paginada).
- `limit` com teto no servidor (ex.: máx. 500), por causa do servidor fraco.
- Retorna **também os inativos** (`IsActive = false` é o tombstone, D6). É isso que avisa o app que algo foi excluído.

**5.3 Repositório** — `GetChangesAsync(ulong since, int limit)`

```sql
WHERE RowVersion > @since AND RowVersion < MIN_ACTIVE_ROWVERSION()
ORDER BY RowVersion
TAKE @limit + 1
```

- **`MIN_ACTIVE_ROWVERSION()` é obrigatório.** Sem ele, uma transação que ainda não fez commit pode gravar uma linha com `rowversion` **menor** que um cursor já entregue, e essa linha nunca seria baixada. Esse é o problema clássico de usar `rowversion` como cursor.
- Pegar `limit + 1` para saber se `HasMore`.
- `NextCursor` = `RowVersion` do último item devolvido.
- **Cursor opaco** para o app: serializar o `rowversion` (8 bytes) como `ulong` em texto. O app só guarda e reenvia.
- Comparar `byte[]` em LINQ não traduz direto. Opções: mapear `RowVersion` como `ulong` com `HasConversion<byte[]>()` (o EF Core suporta), ou usar `FromSqlInterpolated`. **Validar a tradução na implementação.**

**5.4 Serviço** — `GetChangesAsync(string? since, int limit)` no `IMilkProductionService`: converte o cursor, valida (`400` se inválido), chama o repositório e mapeia.

**5.5 Como foi implementado (02/Out/2026)**

> **Decisão: opção B — SQL só no filtro, sem migração.** O C# não tem `>` para `byte[]`, então `m.RowVersion > since` não compila. Trocar `RowVersion` para `ulong` com conversor exigiria uma migração nova (mudança de snapshot). O `MIN_ACTIVE_ROWVERSION()` também só existe em SQL. O filtro vai em `FromSqlRaw`; ordenação, paginação e o filtro de tenant (`HasQueryFilter`) continuam em LINQ/EF.

| Peça | Onde | O que faz |
|---|---|---|
| `SyncPageDto<T>` | `Application/DTOs` | `Items`, `NextCursor` (texto), `HasMore` |
| `SyncPaging` (helper) | `Application/Helpers` | `ResolveLimit` (ausente/inválido → 500; máx. 500; **ajusta, não dá erro**) · `TryDecodeCursor` (vazio → 0 = carga completa; não numérico → `false`) · `ToRowVersionBytes`/`FromRowVersionBytes` (big-endian, ordem do SQL Server) · `BuildPage` (busca `limit + 1` → `HasMore`; `NextCursor` = `RowVersion` do último item; página vazia **mantém** o cursor recebido) |
| `GetChangesSinceAsync<T>` | `Infrastructure/Data/SyncableDbContextExtensions` | `SELECT * FROM [Tabela] WHERE [RowVersion] > @p0 AND [RowVersion] < MIN_ACTIVE_ROWVERSION()` + `OrderBy(RowVersion)` + `Take` + `AsNoTracking`. Nome da tabela vem do modelo do EF (sem injeção); `since` vai como parâmetro. **Não filtra `IsActive`** (tombstones). |
| `GetChangesAsync` | Repositório, service e controller | Rota `GET /api/milk-productions/changes?since=&limit=` |

- **Cursor inválido** (`abc`, `-5`) → `ValidationException` → `400` `{ "error": "Cursor de sincronização inválido." }`. Nenhuma das três exceções de domínio do `CLAUDE.md` descreve um parâmetro malformado.

**Por que não reutilizar o `GET` existente:** as rotas de tela respondem "o que mostrar" e o pull responde "o que mudou desde X".

| | Rotas de tela (`GET /`, `/by-date`) | `GET /changes` |
|---|---|---|
| Formato | Resumo por dia / item enxuto (sem `syncId`, `isActive`, `updatedAt`) | Registro completo |
| Filtro | Data **da produção** (`Date`) — uma correção de um lançamento antigo não aparece | Momento **da mudança** (`RowVersion`) |
| Inativos | Não interessam | Obrigatórios (exclusões) |
| Tamanho | Proporcional ao período | Proporcional ao que mudou |

Um `?updatedSince=` no `GET` atual também não serve: o `UpdatedAt` guarda o momento da edição no celular (Fase 3), mudaria o formato que a web usa e misturaria dois contratos na mesma URL.

**5.6 Resultado da validação (02/Out/2026)**

| Cenário | Resultado |
|---|---|
| Pull inicial sem `since` | ✅ 8 itens (todos inativos de testes anteriores), `nextCursor: 38027`, `hasMore: false` |
| **Isolamento por propriedade** | ✅ A tabela tem 10 registros; vieram só os 8 da propriedade do usuário. O `HasQueryFilter` é aplicado por cima do `FromSqlRaw`. |
| `since=38027` sem mudanças | ✅ `items: []`, cursor mantido em `38027` |
| Cria A, edita A, cria B, inativa B → pull `since=38027` | ✅ Só A (volume 22, ativo) e B (inativo), cada um **uma vez** no estado final; cursor `38031` |
| Paginação `limit=1` desde o início | ✅ 10 páginas, ids 3→12 em ordem, cursores sempre crescentes, sem repetir nem pular; última página com `hasMore: false` |
| `since=abc` e `since=-5` | ✅ `400` "Cursor de sincronização inválido." |
| `limit=0` e `limit=100000` | ✅ `200` (ajustados para 500) |

---

### Fase 6 — Testes

> ✅ **Concluída (02/Out/2026) — 19 testes, todos passando** (`dotnet test MuuBoi.Tests/MuuBoi.Tests.csproj`).

**Origem do projeto de testes — opção B.** O `MuuBoi.Tests.csproj` existia só na branch `feat/tests` (commit `48535dc`, não integrado à `main`), junto com `bin/`/`obj/` commitados, um `UnitTest1.cs` vazio e `Moq` adicionado desnecessariamente ao `MuuBoi.csproj`. Foi recriado **só o necessário**: o `.csproj` com o mesmo conteúdo (net10.0, xUnit 2.9.3, Moq 4.20.72, coverlet, Test SDK 17.14.1) e a inclusão no `MuuBoi.sln` via `dotnet sln add`. A branch `feat/tests` não foi alterada.

**Montagem:** `MuuBoi.Tests/Services/MilkProductionServiceTests.cs` — repositório mockado com Moq; **AutoMapper real** com o `MilkProductionProfile` (nenhum teste verifica campos mapeados, só o comportamento do service); Arrange/Act/Assert sem linhas em branco e sem comentários.

| # | Teste | Verifica |
|---|---|---|
| 1 | `CreateAsync_WithNewSyncId_CreatesProductionWithGivenSyncId` | Cria com o `SyncId` informado |
| 2 | `CreateAsync_WithExistingSyncId_ReturnsExistingWithoutCreating` | Devolve o existente (volume original) sem chamar `CreateAsync` |
| 3 | `CreateAsync_WithoutSyncId_GeneratesSyncId` | Gera `SyncId`; não consulta por `SyncId` |
| 4 | `UpdateAsync_WithNewerClientUpdatedAt_AppliesChanges` | Aplica; `UpdatedAt` = horário do cliente |
| 5 | `UpdateAsync_WithOlderClientUpdatedAt_KeepsServerVersion` | Não aplica nem chama `UpdateAsync`; devolve a versão do servidor |
| 6 | `UpdateAsync_WithSameClientUpdatedAt_AppliesChanges` | Reenvio (empate) aplica |
| 7 | `UpdateAsync_WithFutureClientUpdatedAt_ClampsToNow` | `UpdatedAt` limitado a "agora" |
| 8 | `UpdateAsync_WithOffsetClientUpdatedAt_ConvertsToUtc` | `DateTimeKind.Local` convertido para UTC (máquina de teste em UTC-03:00, então o teste é significativo) |
| 9 | `UpdateAsync_WithoutClientUpdatedAt_UsesNow` | Web: "agora" |
| 10 | `UpdateAsync_WhenNeverEdited_ComparesWithCreatedAt` | Sem `UpdatedAt`, compara com `CreatedAt` |
| 11 | `UpdateAsync_WhenProductionNotFound_ThrowsNotFoundException` | `404` |
| 12 | `DeactivateAsync_WhenActive_DeactivatesAndReturnsTrue` | Inativa e grava |
| 13 | `DeactivateAsync_WhenAlreadyInactive_ReturnsTrueWithoutUpdating` | Idempotente; não grava nem altera `UpdatedAt` |
| 14 | `DeactivateAsync_WhenProductionNotFound_ThrowsNotFoundException` | `404` |
| 15 | `GetChangesAsync_WithInvalidCursor_ThrowsValidationException` | `400`; não consulta o repositório |
| 16 | `GetChangesAsync_WithoutCursor_RequestsChangesFromZero` | Cursor vazio → 0; pede `DefaultLimit + 1` |
| 17 | `GetChangesAsync_WhenMoreThanLimit_ReturnsHasMoreAndLastItemCursor` | `HasMore`; `NextCursor` = último item da página (não o extra) |
| 18 | `GetChangesAsync_WhenNoChanges_KeepsReceivedCursor` | Página vazia mantém o cursor |
| 19 | `GetChangesAsync_WithLimitAboveMax_RequestsMaxPlusOne` | Limite ajustado ao máximo |

Os helpers `SyncTimestampResolver` (testes 4–10) e `SyncPaging` (15–19) ficam cobertos **indiretamente**, pela regra "só services" do `CLAUDE.md`.

**Fora dos testes:** os métodos que já existiam antes do offline (`GetAllAsync`, `GetByDateAsync`, `GetByIdAsync`); e os helpers de infraestrutura (`AddSyncableAsync`, `GetChangesSinceAsync`, `MIN_ACTIVE_ROWVERSION()`), que dependem do SQL Server real e foram validados manualmente nas Fases 2 e 5.

---

### Fase 7 — Documentação

- **Spec 11.1 §8/§9:** marcar o offline como implementado e apontar para este plano.
- **Spec #14:** substituir o push em lote (§5.2) pela decisão A1–A3; registrar A5 (resolução de `Id` no app); fechar as questões §10.3 (origem do `UpdatedAt`: edição do cliente, limitada a "agora") e §10.6 (tamanho de lote → `limit` do pull).
- **`CLAUDE.md`:** registrar a exceção — nas rotas com suporte offline, repetir uma operação já aplicada devolve `2xx`, e não `ConflictException`.

---

### Fase 8 — Validação manual (Swagger/Postman)

| Cenário | Esperado |
|---|---|
| `POST` com `SyncId` X | `201` |
| Mesmo `POST` com `SyncId` X | `201`, **mesmo `Id`**, sem nova linha no banco |
| `POST` sem `SyncId` | `201` com `SyncId` gerado |
| `PATCH` com `UpdatedAt` antigo | `200` com os valores do servidor (inalterados) |
| `PATCH` repetido | `200`, mesmo resultado |
| `DELETE` duas vezes | `204` e `204` |
| `GET /changes` sem `since` | Tudo, paginado |
| `GET /changes?since=<NextCursor>` após editar um registro | Só o registro editado |
| `GET /changes` após `DELETE` | Item com `isActive: false` |
| `GET /changes` com token de outra propriedade | Nada da primeira propriedade |

---

## 5. Contrato para o app (lado cliente)

Esta lista não é implementação do servidor, mas o app precisa seguir isto para o servidor funcionar como planejado.

1. Gerar o `SyncId` **ao salvar** e persistir no Room. Nunca regenerar.
2. Fila (outbox) persistente no Room, esvaziada por **um único worker** (`enqueueUniqueWork` + `KEEP`), em ordem.
3. Montar o corpo da requisição **na hora do envio** (para resolver `ServerId`).
4. No `201`/`200` do `POST`: gravar `ServerId = Id` e remover da fila.
5. `PATCH` leva o `UpdatedAt` do momento da edição, **sempre em UTC com `Z`** (ex.: `"2026-10-02T17:30:00Z"`).
6. Tabela de decisão:

| Resposta | Ação |
|---|---|
| `2xx` | Remove da fila e segue |
| `401` | Renova o token e repete o mesmo item |
| Timeout / sem rede / `5xx` | Para e `Result.retry()` (backoff) |
| `400` / `404` / `409` / `422` | Para, marca `Failed` e avisa o usuário |

7. **Compactação:** criado e excluído offline antes de sincronizar → remove os dois itens da fila sem enviar nada.
8. Ciclo: **esvaziar a fila → pull `/changes` até `HasMore = false`**. Salvar o `NextCursor` **só depois** de gravar a página no Room.
9. **Estado da sincronização no app** — o servidor não sabe quais celulares existem nem quando cada um sincronizou; quem guarda isso é o app (mesmo modelo do WatermelonDB `lastPulledAt` e do *delta token* do Datasync Toolkit):

```
SyncState (Room)
  Resource      → "milk-productions"
  Cursor        → NextCursor da última página aplicada (opaco)
  LastSyncedAt  → momento do último ciclo completo (só para a interface)
```

   - **`Cursor`** é o que importa tecnicamente; o app não interpreta, só guarda e reenvia.
   - **`LastSyncedAt`** é só para exibir ("Sincronizado há 5 min"); pode usar o relógio do celular, pois não participa de nenhuma decisão.
   - **Um cursor por recurso**, porque cada recurso tem sua própria rota `/changes`.
   - Aplicar a mesma página duas vezes é seguro (upsert por `SyncId`), por isso o cursor só avança depois de aplicar.
   - **Zerar os cursores** no logout, na troca de usuário/propriedade e na reinstalação → o próximo pull é completo.

```
1º uso         Cursor vazio → GET /changes                → recebe tudo (paginado)
Seguintes      GET /changes?since={Cursor}                → só o que mudou
               aplica a página no Room → Cursor = NextCursor
               repete enquanto HasMore = true
               LastSyncedAt = agora (ao terminar o ciclo)
```

---

## 6. Arquivos impactados

| Camada | Arquivo | Mudança |
|---|---|---|
| Domain | `Domain/Models/ISyncable.cs` | **Novo** |
| Domain | `Domain/Models/MilkProduction.cs` | `SyncId`, `RowVersion` |
| Infrastructure | `Infrastructure/Data/ApplicationDbContext.cs` | Default `NEWID()`, índice único, `IsRowVersion`, índice do cursor |
| Infrastructure | `Infrastructure/Migrations/*` | **Nova migração** ⚠️ aprovação |
| Infrastructure | `Infrastructure/Data/SyncableModelBuilderExtensions.cs` | **Novo** ✅ — `ConfigureSyncable<T>()` |
| Infrastructure | `Infrastructure/Data/SyncableDbContextExtensions.cs` | **Novo** ✅ — `FindBySyncIdAsync<T>`, `AddSyncableAsync<T>`, `GetChangesSinceAsync<T>` |
| Infrastructure | `Infrastructure/Repositories/MilkProductionRepository.cs` | ✅ `GetBySyncIdAsync`, `CreateAsync` e `GetChangesAsync` via helpers |
| Application | `Application/Helpers/SyncTimestampResolver.cs` | **Novo** ✅ — `ResolveEditedAt`, `IsOutdated` |
| Application | `Application/Interfaces/IMilkProductionRepository.cs` | Novos métodos |
| Application | `Application/Interfaces/IMilkProductionService.cs` | `GetChangesAsync` |
| Application | `Application/Services/MilkProductionService.cs` | Create idempotente, LWW, Deactivate idempotente, changes |
| Application | `Application/DTOs/MilkProductionCreateDto.cs` | `SyncId?` |
| Application | `Application/DTOs/MilkProductionUpdateDto.cs` | `UpdatedAt?` |
| Application | `Application/DTOs/MilkProductionDto.cs` | `SyncId` |
| Application | `Application/DTOs/SyncPageDto.cs` | **Novo** ✅ |
| Application | `Application/Helpers/SyncPaging.cs` | **Novo** ✅ — cursor e paginação |
| Application | `Application/Mappings/MilkProductionProfile.cs` | ✅ Ignorar `SyncId` e `RowVersion` na criação |
| Api | `Api/Controllers/MilkProductionsController.cs` | Rota `GET changes` |
| Tests | `MuuBoi.Tests/MuuBoi.Tests.csproj` | **Novo** ✅ — recriado a partir da branch `feat/tests` |
| Tests | `MuuBoi.Tests/Services/MilkProductionServiceTests.cs` | **Novo** ✅ — 19 testes |
| Solução | `MuuBoi/MuuBoi.sln` | ✅ + projeto `MuuBoi.Tests` |
| Docs | Spec 11.1, Spec #14, `CLAUDE.md` | Fase 7 |

**Não muda:** `Program.cs`, `ExceptionMiddleware`, `LactationService`, demais entidades.

---

## 7. Ordem sugerida e pontos de aprovação

1. Fase 1.1–1.3 (código de domínio e EF).
2. ⚠️ **Aprovação** → Fase 1.4 (migração) → `dotnet ef database update` (⚠️ aprovação).
3. Fases 2 → 3 → 4 (uma de cada vez, testando no Swagger).
4. Fase 5 (pull).
5. Fase 6 (testes) — ou junto de cada fase.
6. Fase 8 (validação manual).
7. Fase 7 (documentação).

---

## 8. Fora do escopo desta fase

- **Reautenticação JWT** durante longos períodos offline (refresh token) — transversal, mas **necessária antes de usar o app offline em campo**.
- **`Lactation`** — depende de `Animal`.
- **Demais entidades** — seguem o mesmo molde, em ordem de dependência: catálogos (`Medication`, `Vaccine`, `StockItem`, `SemenSample`) → `Animal` → eventos do animal (pesagem, ECC, cobertura, gestação, parto, lactação, vacinação, tratamentos).
- **Estoque de sêmen e de insumos** (saldo com consumo concorrente) — Spec #14 §10.1, ainda em aberto.
- **Implementação do app Android.**

---

## 9. Riscos e pontos de atenção

| Risco | Mitigação |
|---|---|
| Linha "pulada" no pull por transação ainda não confirmada | `MIN_ACTIVE_ROWVERSION()` (Fase 5.3) |
| Relógio do celular errado vence o LWW | Limitar a "agora" (Fase 3.2); risco residual aceito (Spec #14, D2) |
| Comparação de `rowversion` não traduzida pelo EF | ✅ Resolvido na Fase 5: filtro em `FromSqlRaw` (opção B), sem migração |
| `DbUpdateConcurrencyException` nova por causa de `IsRowVersion()` | Sem tratamento: vira `500` e o app tenta de novo (decisão da Fase 3.3) |
| Relógio do celular atrasado perde sempre no LWW | Risco aceito (Fase 3.6); evolução futura: controle por versão |
| Índice único fora da convenção `UX_{Tabela}_SyncId` | `AddSyncableAsync` não reconhece a corrida → `500`. Usar sempre `ConfigureSyncable()` (§4.0) |
| Web passa a ver `204` ao inativar duas vezes | Comportamento aceito e documentado (Fase 4) |
| Duplicata se o app regenerar o `SyncId` | Contrato do cliente, item 1 (§5) |

---

## 10. Identidade e idempotência: `SyncId` × chave por requisição

### 10.1 O `SyncId` é o UUID

"UUID" é o **tipo do valor**; "`SyncId`" é o **nome do campo** que guarda esse valor e diz para que ele serve — como `int` e `Id`.

| Onde | Nome / tipo |
|---|---|
| Padrão (RFC 9562) | **UUID** |
| C# / .NET | `Guid` |
| SQL Server | `uniqueidentifier` |
| Kotlin / Room | `Uuid` / `UUID` |
| Contrato da API | Campo **`syncId`** |

Todo registro sincronizável tem **dois identificadores**:

- **`Id` (`int`)** — identidade interna, gerada pelo servidor; usada nas rotas (`/api/milk-productions/42`) e nas FKs.
- **`SyncId` (UUID)** — identidade gerada pelo celular **quando o usuário salva**; existe antes de o registro chegar ao servidor e permite reconhecer o reenvio. O mesmo valor existe no celular e no servidor para sempre.

### 10.2 Versão do UUID

- **UUID v7** (RFC 9562, 2024): 48 bits de timestamp em ms + 74 bits aleatórios; ordenável por tempo. No Kotlin: `Uuid.generateV7()` (stdlib, ainda experimental — `@OptIn(ExperimentalUuidApi::class)`).
- **UUID v4**: totalmente aleatório. No Kotlin: `UUID.randomUUID()`.
- As duas servem para idempotência. Recomendação: **v7 se a versão do Kotlin tiver o método, senão v4**. No Room (SQLite), o v7 mantém fila e índices locais em ordem cronológica.
- **No SQL Server o v7 não traz ganho de índice:** o `uniqueidentifier` compara primeiro os bytes 10–15 e por último os bytes 0–3, onde fica o timestamp do v7 — na prática ele se comporta como o v4. No MuuBoi isso pesa pouco, porque o `SyncId` fica num **índice secundário**, e não no clusterizado (a PK continua `int`).
- `Guid.CreateVersion7()` só existe a partir do **.NET 9** (o MuuBoi está em net8.0). Não faz falta: quem gera o `SyncId` é o celular, e o backfill usa `NEWID()`.
- **O servidor aceita qualquer versão de UUID** e não valida a versão.

### 10.3 `SyncId` (identidade) × `Idempotency-Key` (requisição)

| | `SyncId` | `Idempotency-Key` (padrão Stripe/IETF) |
|---|---|---|
| Identifica | O **registro** | A **tentativa de operação** |
| Duração | Permanente | Temporária (24h na Stripe) |
| Um por | Registro | Requisição (POST, PATCH ou DELETE) |
| Guardado | Na própria tabela | Tabela à parte, com a resposta salva |

### 10.4 Cada operação já está protegida sem chave por requisição

| Tipo de escrita | Rotas do MuuBoi | O que protege o reenvio |
|---|---|---|
| **Cria registro** | Todos os `POST` de criação: animal, pesagem, ECC, cobertura, gestação, parto, vacinação, reforço, caso de saúde e seus medicamentos/testes, movimentações de sêmen e de estoque, catálogos | O **`SyncId`** do registro criado |
| **Define valores** | Todos os `PATCH` de edição | Aplicar duas vezes dá o mesmo resultado; o LWW pelo `UpdatedAt` descarta edições mais antigas |
| **Muda estado** | `DELETE` (inativar), `exit`, `reactivate`, `dry-off` e desfazer, diagnóstico da cobertura (`status`), perda da gestação | Se o registro **já está no estado pedido**, responde `2xx` em vez de `409` |

Fora do offline (exigem conexão): `auth/register`, `auth/login`, `users`.

**Conclusão: o MuuBoi não precisa de chave por requisição em nenhuma rota.** Ela só seria necessária para uma operação **acumulativa** — que soma ou subtrai algo **sem criar registro** (ex.: `POST /stock-items/{id}/consume { quantidade: 1 }`). Isso não existe no MuuBoi: os saldos de estoque e de sêmen são **calculados a partir das movimentações**, e cada movimentação tem seu próprio `SyncId`.

> **Regra para as próximas entidades:** toda escrita offline **cria um registro**, **define valores** ou **muda um estado**. Se surgir a proposta de um campo de saldo atualizado com `+=`/`-=`, ele vira **movimentação**. Só se aparecer uma ação acumulativa que não possa virar registro é que se adiciona `Idempotency-Key`.

**O que a chave por requisição daria a mais (e por que não compensa agora):**

1. **Detectar uso errado da chave** — a Stripe retorna erro se a mesma chave chegar com dados diferentes. No plano, o mesmo `SyncId` com payload diferente devolve o existente sem reclamar; isso só ocorre por bug no app, e o custo (tabela extra, hash do corpo, limpeza de chaves) não compensa.
2. **Rastreamento** — um header `X-Request-Id`, apenas registrado em log (sem banco), ajuda a depurar qual requisição do celular falhou. É **rastreamento**, não idempotência; melhoria opcional.

### 10.5 Condições para o `SyncId` bastar nas próximas entidades

Não afetam a `MilkProduction` (grava um registro só), mas são **obrigatórias** antes de liberar offline para as entidades abaixo.

**Condição 1 — Uma transação por operação.** Várias operações criam **registros derivados**:

| Operação | Também cria |
|---|---|
| Parto (`AnimalCalvingService.cs:73–92`) | Crias/animais, lactação e ECC |
| Cobertura | Movimentação de sêmen / consumo da dose (`SemenSampleMovementService.cs:132`) |
| Diagnóstico positivo | Gestação (`AnimalPregnancyService.cs:196`) |
| Saída do animal | Registro de saída (`AnimalService.cs:184`) |

Hoje **cada `repository.CreateAsync` chama `SaveChangesAsync` separadamente**. Se o servidor cair entre o parto e a lactação, o parto fica gravado sem a lactação; no reenvio, o `SyncId` do parto é encontrado, o servidor responde "já existe" e **a lactação nunca é criada**. O `SyncId` do registro principal só protege os derivados se **tudo for gravado na mesma transação**.

**Condição 2 — O app gera o `SyncId` dos registros derivados que o usuário pode referenciar.** Ex.: parto registrado offline e, em seguida, pesagem da cria, ainda offline — sem o UUID da cria, a pesagem não tem como referenciá-la. O app envia os UUIDs na requisição:

```json
POST /api/pregnancies/42/calvings
{
  "syncId": "parto-uuid",
  "calves": [
    { "syncId": "cria-1-uuid", "tagNumber": "..." },
    { "syncId": "cria-2-uuid", "tagNumber": "..." }
  ]
}
```

Derivados que o usuário não referencia diretamente (lactação aberta pelo parto, movimentação de sêmen gerada pela cobertura) podem ter o `SyncId` gerado pelo servidor (`NEWID()`); o app os recebe pelo pull.

---

## 11. `UpdatedAt` × `RowVersion`

O `UpdatedAt` **já existe** em todas as entidades via `BaseEntity`, exceto `ApplicationUser` e `Property` (fora do offline) e `VaccinationEventAnimal` (tabela de ligação).

Os dois campos têm papéis diferentes:

| Campo | Para que serve | Quem preenche | Ordem garantida? |
|---|---|---|---|
| `UpdatedAt` | Resolver **conflito** (LWW): qual edição é mais recente | O **celular** (momento da edição) ou o servidor (web) | **Não** |
| `RowVersion` | Saber **o que mudou** desde a última sincronização (cursor do pull) | O **SQL Server**, a cada INSERT/UPDATE | **Sim**, sempre crescente |

**Por que o `UpdatedAt` não serve como cursor:**

1. **Ele vem do celular.** Uma edição feita offline na segunda e enviada na sexta chega com o `UpdatedAt` de segunda. Um celular que sincronizou na quarta, ao pedir "o que mudou depois de quarta?", **nunca recebe** essa edição.
2. **Mesmo com horário do servidor, a ordem falha.** Duas gravações simultâneas podem fazer commit em ordem diferente da dos horários — o mesmo problema que o `MIN_ACTIVE_ROWVERSION()` resolve (Fase 5.3).

**O que cada tabela precisa:**

| Tabela | `UpdatedAt` | `RowVersion` + `SyncId` |
|---|---|---|
| Entidades sincronizáveis | Já tem (`BaseEntity`) | **Adicionar** via `ISyncable`, uma entidade por vez |
| `VaccinationEventAnimal` | Não precisa | **Não sincroniza sozinha** — vai dentro do `VaccinationEvent` (lista de animais vacinados); quando a lista muda, o `RowVersion` do evento avança |
| `ApplicationUser`, `Property` | — | Fora do offline |

**Melhoria opcional:** preencher `UpdatedAt = CreatedAt` já na criação, em vez de `null`, para o LWW comparar sempre o mesmo campo sem `?? CreatedAt` espalhado. O plano funciona sem ela.

---

## 12. Resposta perdida (o servidor gravou, mas o app não recebeu)

Do ponto de vista do celular, três situações parecem iguais (timeout ou conexão interrompida):

| # | O que aconteceu | O servidor gravou? |
|---|---|---|
| 1 | A requisição nem chegou ao servidor | Não |
| 2 | Chegou, mas o servidor caiu no meio do processamento | Não (se houver transação) |
| 3 | **Chegou, foi gravada, mas a resposta se perdeu** | **Sim** |

O app **não tem como saber** qual ocorreu. A regra é única: **reenviar exatamente a mesma requisição, com o mesmo `SyncId`**. Quem torna isso seguro é o servidor.

```
App                                   Servidor
 │ POST /milk-productions (SyncId=a1b2) ─►  grava Id=42
 │                ✕ ◄───────────────────  201 {id:42}   (resposta perdida)
 │ timeout → mantém na fila
 │ ... backoff ...
 │ POST /milk-productions (SyncId=a1b2) ─►  encontra SyncId=a1b2 → não grava de novo
 │ ◄──────────────────────────────────── 201 {id:42}
 │ salva ServerId=42, remove da fila
```

### 12.1 Problema no código atual (para as próximas entidades)

No caso 3, o reenvio chega a um servidor onde a operação **já foi aplicada**. Hoje vários services respondem com `ConflictException` (409):

| Operação reenviada | Resposta hoje |
|---|---|
| `POST` animal | `AnimalService.cs:121` — "Já existe um animal com o brinco '...'" |
| `POST` gestação | `AnimalPregnancyService.cs:79` — "O animal já possui uma gestação ativa confirmada." |
| `POST` parto | `AnimalCalvingService.cs:44` — "Esta gestação já possui um parto ativo registrado." |
| Diagnóstico da cobertura | `BreedingEventService.cs:185` — "O diagnóstico desta cobertura já foi registrado." |
| Secar lactação | `LactationService.cs:109` — "Esta lactação já está seca." |
| Inativar (várias entidades) | "... já está inativo(a)." |

Resultado: o app recebe `409`, entende como erro definitivo e **a fila trava, mesmo a operação tendo dado certo**. Se o usuário descartar o item, o app passa a achar que o registro não existe no servidor. Já em pesagem e produção de leite (sem regra de unicidade), o reenvio **duplica o registro silenciosamente**.

### 12.2 Regras no servidor

| Regra | Como |
|---|---|
| Checar o `SyncId` **antes** de qualquer regra de negócio | `GetBySyncIdAsync` → se existe, devolve o recurso com o mesmo corpo do `201` (incluindo o `Id`) |
| Mudança de estado repetida | Se o estado atual já é o pedido, devolve `200` com o recurso (não `409`) — como o `DELETE` no HTTP |
| Uma transação por operação | Um único `SaveChangesAsync` ou transação explícita (§10.5) |
| Corrida entre original e reenvio | Índice único em `SyncId`; capturar a violação e devolver o existente (Fase 2.4) |

### 12.3 Regras no app

1. Gerar o UUID **quando o usuário salva**, gravar no Room e **nunca regenerar** — se for gerado no envio, cada tentativa cria um UUID novo e a idempotência deixa de funcionar (erro mais comum).
2. Timeout ou falha de conexão → o item **continua na fila**; `Result.retry()`.
3. Só remove da fila ao receber `2xx`.
4. Timeout adequado no OkHttp/Retrofit (ex.: 30–60 s) para não desistir cedo numa conexão lenta.

---

## 13. Referência entre entidades criadas offline

**Cenário:** animal criado offline e, em seguida, gestação desse animal, ainda offline. A rota da gestação espera o `Id` (`int`) do animal, que só existe depois que o servidor o grava.

**O app recebe o `Id`:** o `POST` devolve o recurso completo no corpo do `201` (`CreatedAtAction(..., created)` — ex.: `AnimalsController.cs:88`), incluindo o `Id`. Depois do `201`, o app conhece os dois identificadores.

### 13.1 Solução A — o app troca a referência na hora do envio (adotada, A5)

O corpo da requisição **não é montado quando o usuário salva**, e sim **quando o worker vai enviar**.

```
Animal (Room)
  SyncId (Guid)     ← gerado no celular
  ServerId (int?)   ← null até sincronizar

Outbox: { Operation: "CreatePregnancy", AnimalSyncId: "a1b2...", Payload: {...} }
```

1. Worker envia `POST /api/animals` com o `SyncId` → recebe `201 { "id": 42 }`.
2. Grava `ServerId = 42` no animal local e remove o item da fila.
3. Próximo item (gestação): busca no Room o animal pelo `SyncId`, lê `ServerId = 42` e monta a requisição com `animalId = 42`.
4. Envia a gestação.

O **envio sequencial** é o que garante isso: quando a gestação chega na vez dela, o animal já tem `ServerId`. Se o animal falhou, a fila parou antes. Se o `201` do animal se perdeu, o reenvio devolve o mesmo `Id` (§12).

### 13.2 Solução B — servidor aceita referência por `SyncId` (descartada)

A gestação seria enviada com `animalSyncId`, e o service resolveria `Guid → int` pelo repositório.

| | A: troca no app | B: referência por `SyncId` |
|---|---|---|
| Mudança no servidor | Mínima (`SyncId` na criação + retorno idempotente) | Grande (todos os DTOs com FK e rotas com `{id}`) |
| Complexidade no app | Montar o payload no envio e guardar `ServerId` | Menor |
| Depende de envio sequencial | Sim (já adotado) | Não |

A B só compensaria se, no futuro, o envio fosse em paralelo ou em lote.

---

## 14. Bibliotecas

### 14.1 App (Android nativo, Kotlin)

Não existe biblioteca que faça essa sincronização sozinha com a API do MuuBoi — o protocolo é próprio (rotas REST, `SyncId`, `/changes` com cursor). O app monta a sincronização com bibliotecas padrão, o mesmo conjunto recomendado pelo guia oficial de offline-first.

| Biblioteca | Papel |
|---|---|
| **Room** | Banco local (fonte da verdade da tela), **fila de envio** e **`SyncState`** |
| **WorkManager** | Sync em segundo plano: só com rede (`NetworkType.CONNECTED`), sobrevive ao app fechado e ao reinício, **retry com backoff** (`Result.retry()`), worker único (`enqueueUniqueWork`) |
| **Retrofit + OkHttp** | HTTP; no OkHttp: **timeouts**, **`Interceptor`** que coloca o JWT, **`Authenticator`** que trata o `401` renovando o token e repetindo |
| **kotlinx.serialization** (ou Moshi) | JSON |
| **Coroutines + Flow** | Assíncrono; a tela observa o Room e se atualiza quando o pull grava dados novos |
| **Hilt** | Injeção de dependência; `@HiltWorker` para o worker receber repositórios e Retrofit |
| **`kotlin.uuid.Uuid`** (stdlib) | Gera o `SyncId` — sem biblioteca extra |

Opcionais: **DataStore** (token, preferências), **Paging 3** (listas locais grandes).

**Não é necessário:**

- Monitorar conexão manualmente (`ConnectivityManager`, `NetworkCallback`) — a restrição de rede do WorkManager já faz isso; no máximo para um aviso "offline" na tela.
- Frameworks prontos de sync:

| Opção | Por que não serve |
|---|---|
| Datasync Community Toolkit | Cliente é **.NET** (MAUI, WPF...), não Kotlin |
| WatermelonDB | **React Native**, com protocolo próprio (push em lote) |
| PowerSync, Couchbase Lite, Firebase | Exigem o **backend deles** |
| Realm / Atlas Device Sync | **Descontinuado** em 2025 |

**Código próprio do app:**

1. Tabela de **fila (outbox)** no Room — tipo de operação, `SyncId`, status.
2. **`SyncWorker`** — esvazia a fila em ordem (tabela de decisão por código HTTP, §5) e depois faz o pull de cada recurso até `HasMore = false`.
3. **Montar a requisição na hora do envio**, trocando a referência local pelo `ServerId`.
4. **Gravar o pull no Room** — upsert por `SyncId` e cursor atualizado depois de aplicar a página.

**Referência de código:** o app oficial do Google [Now in Android](https://github.com/android/nowinandroid) usa Room + WorkManager + Retrofit + Hilt com `SyncWorker` e pull incremental por versões. Não tem fila de envio (só lê do servidor), mas é a melhor referência para a estrutura do worker, a injeção com Hilt e o pull.

### 14.2 API (MuuBoi)

**Nenhuma biblioteca nova.** Tudo vem dos pacotes já instalados (EF Core 8 + SQL Server + ASP.NET Core 8).

| Necessidade | Já disponível em |
|---|---|
| `SyncId` | `System.Guid` / `NEWID()` |
| Cursor do pull | `rowversion` + `.IsRowVersion()` (EF Core) |
| `MIN_ACTIVE_ROWVERSION()` | `Database.SqlQuery<T>` (EF Core 8) |
| Índice único em `SyncId` | `HasIndex(...).IsUnique()` |
| Corrida no `POST` (erros 2601/2627) | `DbUpdateException` + `SqlException` (`Microsoft.Data.SqlClient`, dependência do provider SQL Server) |
| Concorrência no `PATCH` | `DbUpdateConcurrencyException` |
| Uma transação por operação | `SaveChangesAsync` único ou `Database.BeginTransactionAsync()` |
| Filtro por propriedade | `HasQueryFilter` + `ITenantProvider` existentes |
| Status HTTP | `ExceptionMiddleware` existente |
| Testes | xUnit + Moq |

**Opcionais nativos do ASP.NET Core** (⚠️ mexem no `Program.cs` → requerem aprovação):

1. **Compressão de resposta (`AddResponseCompression`)** — o JSON do `/changes` na carga inicial pode ser grande; gzip/brotli reduz 70–90%. Muito útil com internet fraca, custo baixo de CPU.
2. **Refresh token para o JWT** — sem biblioteca (o ASP.NET Identity já tem o necessário); novo endpoint `POST /api/auth/refresh` + tabela de tokens. Necessário para o app voltar de dias offline sem perder a fila. Fase própria, fora da produção de leite.

**Não adicionar:**

| Biblioteca | Por que não |
|---|---|
| Datasync Community Toolkit (server) | Exige **.NET 10**, `Id` em `string` e protocolo próprio (`/tables/...`) |
| Pacotes de idempotência (ex.: `IdempotentAPI`) | Implementam chave por requisição — o `SyncId` basta (§10) |
| MediatR, Polly, Hangfire | Não resolvem nenhum problema deste plano; sem tabela de chaves, não há limpeza em segundo plano |
| Pacotes de bulk insert | Envio é um registro por requisição |

---

## 15. Fontes

- [Android Developers — Build an offline-first app](https://developer.android.com/topic/architecture/data-layer/offline-first)
- [Now in Android (GitHub)](https://github.com/android/nowinandroid)
- [Kotlin — UUIDs](https://kotlinlang.org/docs/uuids.html) · [`Uuid.generateV7`](https://kotlinlang.org/api/core/kotlin-stdlib/kotlin.uuid/-uuid/-companion/generate-v7.html) · [KT-74411](https://youtrack.jetbrains.com/projects/KT/issues/KT-74411/Introduce-Uuid.generateV4-and-generateV7)
- [RFC 9562 — UUIDs](https://www.rfc-editor.org/rfc/rfc9562)
- [Guid.CreateVersion7() is NOT a sequential guid for SQL Server](https://daily.dev/posts/guid-createversion7-is-not-a-sequential-guid-for-sql-server-rlojn9pdl) · [UUID v7 for SQL Server Indexes: Still a Bad Idea](https://pejmannik.dev/blog/uuid_v7_for_sql_server_indexes_still_a_bad_idea/)
- [Stripe — Idempotent requests](https://docs.stripe.com/api/idempotent_requests)
- [IETF — draft-ietf-httpapi-idempotency-key-header-07](https://datatracker.ietf.org/doc/html/draft-ietf-httpapi-idempotency-key-header-07)
- [WatermelonDB — Sync Backend](https://watermelondb.dev/docs/Sync/Backend)
- [Datasync Community Toolkit](https://communitytoolkit.github.io/Datasync/)
