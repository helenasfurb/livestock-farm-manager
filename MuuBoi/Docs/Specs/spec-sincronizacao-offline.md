# Spec #14: Sincronização Offline (Contrato de Servidor) — PROPOSTA

**Módulo:** Infraestrutura / Sincronização — transversal a toda a aplicação
**Versão:** 0.1 (rascunho para discussão)
**Data:** 05/Set/2026
**Fonte:** Definição de escopo do TCC — app deve funcionar offline com sincronização posterior; internet e servidores instáveis mesmo quando online. Desmembrada da Spec #13 (§6), que apenas mapeou o impacto no cadastro de gestação.
**Status:** 🟡 **Em discussão — NÃO aprovado para implementação.**
**Abrangência:** **Somente o lado servidor/API** do MuuBoi (contrato de sincronização, identidade, versionamento, tombstones, idempotência, tenant). O cliente offline (armazenamento local, fila de envio) é tratado como **consumidor deste contrato** e está fora de escopo.
**Relaciona-se com:** Spec #13 (Cadastro Retroativo de Gestação) — primeiro caso concreto que motivou os ganchos; e **todos** os specs de entidade (#2, #5, #6, #7, #10, #11, sanitário), pois a sincronização é transversal.

---

## 1. Contexto e Objetivo

Hoje a API é **online-only** e **server-authoritative**: toda escrita passa pelo servidor, que gera a identidade (`BaseEntity.Id` é `int` identity) e é a única fonte da verdade. O escopo do TCC prevê que o app **funcione offline** e **sincronize** depois, e que **mesmo online** a conexão/servidor sejam **instáveis**.

Isso quebra três premissas atuais:
1. **Identidade** — um registro criado offline não pode esperar o `int` do servidor para existir nem para ser referenciado por dependentes locais.
2. **Fonte única da verdade** — duas origens (app offline + web) podem editar o mesmo dado; é preciso **reconciliar**.
3. **Entrega confiável** — sob instabilidade, requisições são reenviadas; sem **idempotência** geram duplicatas.

O objetivo desta spec é **definir o contrato de servidor** que suporta sincronização, **sem** ainda construir o motor de sync do cliente. As decisões arquiteturais principais já foram tomadas (§2); o restante é proposta a refinar.

---

## 2. Decisões Registradas

| # | Decisão | Motivo |
|---|---------|--------|
| D1 | **Identidade: `SyncId` (`Guid`) secundário.** Mantém a PK `int` interna; adiciona coluna `Guid` única, **gerada no cliente**. Sync referencia entidades pelo `SyncId`; FKs internas continuam `int`. | Menos invasivo que trocar todas as PKs para `Guid`; preserva migrações/relacionamentos existentes; migração é **aditiva**. Permite criar e referenciar registros offline antes de sincronizar. |
| D2 | **Conflito: last-write-wins (LWW) por timestamp.** Vence a versão com `UpdatedAt` mais recente. | Simplicidade — adequado a um TCC. A maioria das entidades é append-only (D5), o que reduz conflitos reais. Risco de perda silenciosa de edições concorrentes é aceito e documentado (§5.4). |
| D3 | **Escopo: somente servidor/API.** Endpoints de pull/push, versionamento, tombstones e idempotência. | O repositório é a API. O cliente é consumidor do contrato. |
| D4 | **Cursor de mudanças por `rowversion`.** Coluna `rowversion` (SQL Server, monotônica) usada como marcador incremental do delta; distinta de `UpdatedAt` (que é domínio + LWW). | `rowversion` é monotônica e à prova de skew de relógio — cursor confiável para "mudanças desde X". `UpdatedAt` do cliente não serve como cursor (relógio não confiável). |
| D5 | **Isolamento por tenant preservado.** Toda sincronização é escopada por `PropertyId`; repositórios filtram por tenant (regra do projeto). | Um cliente só puxa/empurra dados da própria `Property`. `PropertyId` já é `Guid` (estável no cliente). |
| D6 | **Exclusão via soft delete = tombstone natural.** `IsActive = false` + `UpdatedAt` propaga a exclusão pelo mesmo delta; sem tabela de tombstone para o caso comum. | Soft delete já é o padrão do projeto. Hard delete, se houver, exige tratamento à parte (§10). |

---

## 3. Casos de Uso Motivadores

| # | Cenário | Desafio de sync |
|---|---------|-----------------|
| CU-1 | Produtor cadastra animais/eventos **offline** no curral e sincroniza ao voltar ao sinal. | Identidade cliente (D1) + push idempotente. |
| CU-2 | Cadastro retroativo de gestação offline (Spec #13) e, na sequência, **parto** da mesma gestação, ainda offline. | Dependente (parto) referencia a gestação **pelo `SyncId`** antes de ela existir no servidor. |
| CU-3 | Web e app editam o **mesmo animal** enquanto o app está offline. | Reconciliação por LWW (D2) no momento do push. |
| CU-4 | `POST` reenviado porque o `201` não chegou (link instável), mesmo online. | Idempotência por `ClientRequestId`/`SyncId` (§5.3). |
| CU-5 | App fica dias offline e depois puxa tudo que mudou na web nesse período. | Pull incremental por cursor `rowversion` (D4). |

---

## 4. O que muda no modelo (transversal)

### 4.1 `BaseEntity` — colunas de sincronização
Adicionar a **todas as entidades sincronizáveis** (ver §6):

| Campo | Tipo | Origem | Papel |
|-------|------|--------|-------|
| `SyncId` | `Guid` (único) | **Cliente** (ou servidor no backfill) | Identidade estável e portável entre offline/online (D1). |
| `RowVersion` | `byte[]` (`rowversion`/`timestamp`) | **Servidor** | Cursor monotônico do delta (D4). |
| `Id` | `int` (existente) | Servidor | PK interna; **não** exposta no contrato de sync. |
| `UpdatedAt` | `DateTime?` (existente) | Cliente/servidor | Base do LWW (D2). |
| `IsActive` | `bool` (existente) | — | Tombstone via soft delete (D6). |

> Decisão de encaixe: introduzir uma interface **`ISyncable`** (expõe `SyncId`/`RowVersion`) em vez de forçar tudo em `BaseEntity`, para poder **excluir** entidades server-only (§6) da sincronização de forma explícita.

### 4.2 Multi-tenancy
`SyncId` é único **globalmente**, mas todo delta é filtrado por `PropertyId` (D5) no repositório — nunca no serviço (regra do projeto). Entidades sincronizáveis são, na prática, as que implementam `ITenantEntity`.

---

## 5. Contrato de sincronização (API)

### 5.1 Pull incremental (servidor → cliente)
```
GET /api/sync/changes?since={cursor}
```
- Retorna, **para o tenant atual**, todas as entidades sincronizáveis com `RowVersion > cursor` — criações, atualizações **e** exclusões (via `IsActive = false`, D6).
- `cursor` opaco (representa o último `RowVersion` visto). Ausente = carga inicial completa.
- Resposta: envelope com a lista de mudanças (agrupadas por tipo de entidade) + **novo cursor** (maior `RowVersion` do lote). Paginável para lotes grandes.

### 5.2 Push em lote (cliente → servidor)
```
POST /api/sync/changes
```
- Corpo: **lote** de mudanças locais, cada uma com `SyncId`, tipo de entidade, payload e `ClientRequestId`.
- Servidor aplica em **ordem de dependência** (§5.5), resolve referências por `SyncId`, aplica LWW (D2) e responde **por item**: `Applied` / `ConflictResolved` (quem venceu) / `Rejected` (motivo) + mapeamento `SyncId → Id` do servidor.
- **Em lote** para minimizar round-trips em link instável (diretriz de ambiente restrito).

### 5.3 Idempotência
- `ClientRequestId` (`Guid`, por operação) **e** `SyncId` (por entidade) permitem deduplicar reenvios (CU-4). Reenvio do mesmo `ClientRequestId` **não** cria duplicata: retorna o resultado já aplicado.
- Este é o mesmo gancho proposto na Spec #13 §6.3 (Caminho 1) — aqui ele é generalizado para toda a API.

### 5.4 Reconciliação — LWW (D2)
- No push, se o registro já existe (por `SyncId`), compara `UpdatedAt` do payload com o do servidor: **vence o mais recente**; o perdedor é descartado (resposta marca `ConflictResolved`).
- **Risco documentado:** edições concorrentes em campos diferentes do mesmo registro podem se perder (não há merge por campo). Mitigadores: (a) a maioria das entidades é **append-only** (D5/§5.6) → conflito raro; (b) `UpdatedAt` deve ser **carimbado no servidor no momento da escrita original** sempre que possível, para reduzir dependência do relógio do cliente (§9, I-5 da Spec #13).

### 5.5 Ordem de dependências no push
Um lote pode conter pai e filho (CU-2: `Animal` → `AnimalPregnancy` → `AnimalCalving`). O servidor deve:
1. Resolver referências **por `SyncId`** dentro do próprio lote e contra o banco.
2. Aplicar entidades em **ordem topológica** (pais antes de filhos).
3. Se uma referência (`SyncId` de pai) não existe nem no lote nem no banco → item `Rejected` com motivo, sem abortar o lote inteiro.

### 5.6 Entidades append-only reduzem conflito
Eventos e lançamentos são, na prática, **imutáveis após criados**: `BreedingEvent`, `VaccinationEvent`, `MilkProduction`, `WeightRecord`, `BodyConditionRecord`, `SemenSampleMovement`, `AnimalCalving`. Para esses, o push é essencialmente **insert idempotente** — LWW quase nunca dispara. O LWW importa mesmo para **cadastros mutáveis**: `Animal`, `Medication`, `Vaccine`, `SemenSample`. (Não muda a política D2 — apenas dimensiona o risco.)

---

## 6. Entidades no escopo da sincronização

| Sincronizável (`ISyncable` + `ITenantEntity`) | Server-only (fora do sync offline) |
|-----------------------------------------------|-------------------------------------|
| `Animal`, `AnimalExitRecord`, `WeightRecord`, `BodyConditionRecord`, `AnimalMedication`, `Medication` | `ApplicationUser` (autenticação/identidade) |
| `BreedingEvent`, `AnimalPregnancy`, `AnimalCalving`, `AnimalCalvingCalf` | `Property` (provisionamento de tenant) |
| `SemenSample`, `SemenSampleMovement` *(ver §10 — estoque)* | — |
| `MilkProduction`, `Lactation` | — |
| `VaccinationEvent`, `VaccinationEventAnimal`, `Vaccine` | — |

> A lista definitiva de sincronizáveis vs. server-only é **questão em aberto** (§10) — em especial estoque de sêmen, autenticação e mídias.

---

## 7. Impactos e camadas

| Camada | Arquivo | Ação |
|--------|---------|------|
| `Domain/Models` | `BaseEntity.cs` / novo `ISyncable.cs` | Adicionar `SyncId` (`Guid`) e `RowVersion` (`byte[]`) às entidades sincronizáveis. |
| `Infrastructure/Data` | `ApplicationDbContext.cs` | Índice único em `SyncId`; mapear `RowVersion` como `IsRowVersion()`; filtro de tenant nos deltas. |
| `Infrastructure/Migrations` | *(nova)* | **Requer aprovação** — colunas `SyncId`/`RowVersion` (aditivas), índice único, **backfill de `SyncId` das linhas existentes** (`NEWID()`). |
| `Application/DTOs` | `SyncEnvelopeDto`, `SyncChangeDto`, `SyncPushResultDto` (novos) | Contrato de pull/push. |
| `Application/Interfaces` + `Application/Services` | `ISyncService` / `SyncService` (novos) | Delta pull, push em lote, LWW, ordenação topológica, idempotência. |
| `Application/Interfaces` (repos) | interfaces de repositório | Método de change-feed por `RowVersion` escopado por tenant. |
| `Api/Controllers` | `SyncController` (novo) | `GET/POST /api/sync/changes`. |
| `Program.cs` | — | **Requer aprovação** — registrar `ISyncService` no DI. |

---

## 8. O que continua funcionando sem mudança

- **`ITenantProvider`** já injeta o tenant nos repositórios → deltas escopados por `PropertyId` reusam o mecanismo existente (D5).
- **Soft delete (`IsActive`)** já é o padrão → serve de tombstone sem estrutura nova (D6).
- **`PropertyId` já é `Guid`** → identidade de tenant é estável no cliente sem migração.
- **`UpdatedAt` em `BaseEntity`** já existe → base do LWW sem coluna nova (D2).

---

## 9. Relação com a Spec #13 (gestação retroativa)

A Spec #13 §6 já mapeou os impactos e propôs ganchos que **esta spec generaliza**:
- I-1 (identidade offline) → resolvido por **D1 (`SyncId`)**.
- I-2 (reenvio/idempotência) → resolvido por **§5.3 (`ClientRequestId`/`SyncId`)**.
- I-3 (ordem de dependências) → resolvido por **§5.5 (ordenação topológica por `SyncId`)**; o cadastro direto de gestação (sem cobertura) já **minimiza** a dependência (Spec #13 §6.1).
- I-4 (conflito de duas origens) → resolvido por **D2 (LWW)**, com a ressalva de §5.4.
- I-6 (round-trips na compra gestante) → **§5.2 push em lote** cobre; a questão de aninhar gestação na entrada continua na Spec #13 §11.4.

Nenhuma mudança adicional é exigida da Spec #13 além de suas entidades passarem a implementar `ISyncable` como qualquer outra.

---

## 10. Questões em aberto (para discutir com o parceiro de TCC)

1. **Estoque de sêmen offline:** `SemenSampleMovement` **consome dose**. Dois dispositivos offline consumindo a última dose → estoque negativo no sync. LWW **não** resolve saldo. Precisa de tratamento especial (rejeitar no push, reconciliar saldo, ou impedir consumo offline)?
2. **Lista definitiva de sincronizáveis (§6):** autenticação (`ApplicationUser`), `Property` e eventuais **mídias/fotos** entram no sync? Fotos exigem estratégia de payload grande (link instável).
3. **Origem do `UpdatedAt` para LWW (§5.4):** carimbar no servidor na escrita original vs. confiar no relógio do cliente — como conciliar com registros criados 100% offline (que não passaram pelo servidor)?
4. **Hard delete (D6):** existe algum caso de hard delete no sistema? Se sim, precisa de tabela de tombstone dedicada — senão a exclusão "some" sem propagar.
5. **Autenticação na hora do sync:** JWT pode expirar durante o período offline; como o cliente reautentica antes de empurrar a fila acumulada?
6. **Tamanho do lote / paginação (§5.1–5.2):** limites para não estourar memória do servidor fraco nem o tempo de conexão instável.

---

## 11. Fora do Escopo desta Spec

- **Cliente offline** — armazenamento local, fila de envio, detecção de conectividade, UX de conflito. Tratado como consumidor do contrato (D3).
- **Modelagem de cada entidade** — permanece em seus specs (#2, #5, #6, #10, #11, sanitário). Aqui só se adiciona `ISyncable`.
- **Regras de negócio de domínio** (RN-18/RN-19, consumo de dose, etc.) — inalteradas; a sincronização as **respeita** ao aplicar o push.
- **Índices zootécnicos** → Spec #7.
