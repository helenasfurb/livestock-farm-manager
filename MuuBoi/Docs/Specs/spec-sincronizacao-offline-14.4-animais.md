# Spec #14.4: Sincronização Offline — Animais e Saídas do Rebanho

**Módulo:** Infraestrutura / Sincronização — Animais
**Versão:** 1.0
**Data:** 09/Out/2026
**Status:** ✅ **Implementada e validada** (09/Out/2026) — decisões N1–N6 (N7 descartada), Fases 1–7. Branch `feature/offline-animals`. Nenhuma regra de negócio alterada (R1–R9); um bug pré-existente corrigido (§3 item 5) e uma condição nova de corrida tratada (§6.2, Condição 3 da Spec #14 §7.5).
- **Parte A — Cadastro e edição (`Animal`):** ✅ Fases 1, 2, 3 e 5
- **Parte B — Saída e reativação (`AnimalExitRecord`):** ✅ Fase 4

### Histórico de versões

| Versão | Data | Mudança |
|---|---|---|
| 0.1 | 09/Out/2026 | Rascunho: animal e saída planejados juntos, **sem alterar nenhuma regra de negócio**. Decisões N1–N7 propostas; N3, N5 e N6 destacadas para a usuária fechar |
| 0.1.1 | 09/Out/2026 | N1 revisada a pedido da usuária: navegação `Animal.Lactations` (a mudança de modelo entra na migração da Fase 1, sem migração vazia); lactação inicial no mesmo padrão de pesagem e ECC; `CreateAnimalAsync` mantém a assinatura |
| 0.2 | 09/Out/2026 | Decisões N1–N6 aprovadas conforme recomendado. Fase 1 implementada e validada. §3 item 5 (bug da saída) **confirmado**; §3 item 7 **não confirmado** → N7 descartada |
| 1.0 | 09/Out/2026 | Fases 2–7 implementadas e validadas; 142 testes passando (23 novos em `AnimalServiceTests`). Corrida do brinco na criação (§6.2) corrigida e promovida a Condição 3 na Spec #14 §7.5. Bug do histórico de saídas corrigido (Fase 4) |

**Depende de:** Spec #14 (`spec-sincronizacao-offline.md`) — contrato, decisões (D1–D6, A1–A8), helpers (§7.2), receita (§7.4) e Condições 1 e 2 (§7.5). Esta spec **não** redecide nada da #14; aplica a receita ao `Animal` e resolve a Condição 1 para o cadastro e para a saída.
**Relaciona-se com:** Spec #14.2 (S1 e S2 — gravação via navegação; inativação/reativação idempotentes sem LWW) · Spec #14.3 (E1 — derivados do cadastro num só `SaveChanges`) · `spec-animais.md` · `spec-entrada-saida-animais.md` · `spec-producao-11.2-lactacao-secagem.md` (D17 — lactação inicial no cadastro) · `spec-gestacao-parto-6.3-cria.md` (animais criados pelo parto) · `Docs/catalogo-erros-api.md` §4.3

> **Por que animais agora:** os catálogos da Spec #14 §8 terminaram (vacinas, sêmen, estoque; `Medication` saiu do escopo). Todas as entidades que faltam — pesagem, ECC, medicações, cobertura, gestação, parto, lactação, vacinação, casos de saúde — têm `AnimalId`. Sem o animal sincronizável, nenhum evento pode ser criado offline para um animal cadastrado offline (A5, §5.8).
>
> **Por que é um salto de complexidade:** é a primeira entidade com (1) **regra de unicidade** que pode gerar conflito real entre celulares (brinco), (2) **derivados no cadastro** que pertencem a outras entidades ainda não sincronizáveis (pesagem, ECC, lactação), (3) **mudança de estado que cria registro** (saída) e pode ser desfeita (reativação), e (4) **detalhe composto de outros recursos** (status reprodutivo, produtivo, sanitário, genealogia), que torna inviável o "mesmo DTO do detalhe" no pull.
>
> **Por que as duas partes juntas:** a saída é uma mudança de estado do próprio `Animal`, sem rota de edição nem de exclusão própria. Planejar junto permite **uma migração só** (apenas `Animals`) e decidir de uma vez como os registros de saída chegam ao app (N5).

---

## 1. Escopo

| Dentro | Fora |
|---|---|
| `POST /api/animals` e `PATCH /api/animals/{id}` com suporte offline | Rotas de tela: `GET /`, `GET /{id}`, `GET /{id}/exit-records`, históricos de vacinação e saúde (contrato inalterado, exceto o `syncId` aditivo) |
| `PATCH /api/animals/{id}/exit` e `PATCH /api/animals/{id}/reactivate` com suporte offline | Rotas de enum (`/genders`, `/breeds`, `/exit-reasons`...) — referência estática |
| `GET /api/animals/changes` (**nova**), com os registros de saída embutidos | Pesagem, ECC e lactação como entidades sincronizáveis (specs próprias; ver N2) |
| Cadastro com pesagem, ECC e lactação iniciais gravados **juntos** (Condição 1) | Animais criados pelo parto (`AnimalCalvingService`) — continuam sendo criados só online; aparecem no pull como qualquer animal |
| Saída gravada **junto** com a inativação (Condição 1) | Qualquer mudança de regra de negócio (§2) |

**Mudanças nas rotas:**

| Rota | Antes | Depois |
|---|---|---|
| `POST /api/animals` | Sempre cria; `409` no reenvio (brinco já existe — Spec #14 §5.7); lactação inicial em gravação separada | Aceita `syncId`; se já existir → devolve o existente (`201`). Animal, pesagem, ECC e lactação iniciais gravados **juntos** |
| `PATCH /api/animals/{id}` | Aplica e carimba `UpdatedAt = agora` | Aceita `updatedAt`; aplica só se não for mais antigo (LWW). Checagem do brinco **depois** do LWW (N4) |
| `PATCH /api/animals/{id}/exit` | `200`; **`409`** se já inativo; registro de saída e inativação em gravações separadas | `200` **sem gravar** se já inativo; registro e inativação gravados **juntos** |
| `PATCH /api/animals/{id}/reactivate` | `200`; **`409`** se já ativo | `200` **sem gravar** se já ativo |
| `GET /api/animals/changes?since=&limit=` | — | **Nova** — `AnimalSyncDto` com `exitRecords` (N6) |
| `GET /{id}` e respostas de `POST`/`PATCH` | — | Ganham `syncId` (aditivo) |

## 2. Regras de negócio preservadas

**Premissa (como na 14.2 Parte B e na 14.3): nenhuma regra de negócio é alterada.** As únicas mudanças de comportamento são as de "estado repetido" exigidas pela A6 (Spec #14 §3.3).

| # | Regra (código atual) | Onde | Comportamento mantido |
|---|---|---|---|
| R1 | Animal precisa existir | `PATCH /{id}`, `/exit`, `/reactivate` | `404` "Animal com id '{id}' não encontrado." |
| R2 | Brinco único na propriedade, **inclusive entre animais inativos** (`TagNumberExistsAsync` não filtra `IsActive`) | `POST`, `PATCH /{id}` | `409` "Já existe um animal com o brinco '{brinco}' nesta propriedade." |
| R3 | Lactação inicial só para Vaca/Novilha (Spec 11.2 D17) | `POST` | `422` "A lactação inicial só se aplica a vacas e novilhas." |
| R4 | Validações do DTO: brinco com 6 dígitos, sexo e classificação obrigatórios, classificação × sexo, datas futuras (ECC inicial, lactação inicial, saída) | `POST`, `PATCH /{id}`, `/exit` | `400` (formato B) |
| R5 | Pesagem e ECC iniciais sem data → **momento do processamento** (`?? DateTime.UtcNow`) | `POST` | Mantido. Ver §5 item 2 |
| R6 | Mudar o sexo do animal atualiza o sexo da cria ativa vinculada (`AnimalCalvingCalf.Sex`) | `PATCH /{id}` | Mantido, só quando a edição é aplicada pelo LWW |
| R7 | `PATCH` em animal inativo é aceito (edita sem reativar) | `PATCH /{id}` | Mantido (Spec #14 §5.4) |
| R8 | A saída cria um `AnimalExitRecord` (motivo, data, observações) e inativa o animal | `/exit` | Mantido, numa única gravação (N5) |
| R9 | A reativação **não** remove registros de saída (histórico preservado) | `/reactivate` | Mantido |

**Ordem entre `SyncId` e regras:** como nas specs anteriores, o `SyncId` é checado **antes** de R2 e R3. O reenvio de um cadastro já gravado responde `201` com o animal existente — hoje ele recebe `409` de R2, porque o próprio animal já ocupa o brinco (Spec #14 §5.7, primeira linha da tabela). Um `POST` **novo** (outro `syncId`) com brinco em uso continua `409` (Spec #14 §3.3: "Um animal com o mesmo brinco e **outro** `SyncId` continua `409`").

## 3. Situação atual do código e o que muda

| # | Situação atual | Impacto no offline | Tratamento |
|---|---|---|---|
| 1 | `CreateAnimalAsync` grava animal + pesagem + ECC (navegações, um `SaveChanges`) e **depois** a lactação inicial (`SeedInitialLactationAsync` → `_lactationRepository.CreateAsync` → outro `SaveChanges`) | **Condição 1.** Se o servidor cair entre as gravações, o reenvio encontra o `SyncId` e responde "já existe" — **a lactação inicial nunca é criada** | Lactação gravada pela navegação do animal, num **único `SaveChanges`** (N1) |
| 2 | `Animal` não tem navegação `Lactations` (`HasOne(l => l.Animal).WithMany()`) | Não dá para usar a mesma solução de pesagem/ECC (coleção no animal) | Navegação `Animal.Lactations` + `WithMany(a => a.Lactations)` (N1). Sem mudança no banco; só o snapshot muda, dentro da migração da Fase 1 |
| 3 | O reenvio do `POST` cai em R2 (`409`) | Viola a A6: a fila trava num cadastro que deu certo | `SyncId` checado antes de R2/R3 (Fase 2) |
| 4 | `ExitAnimalAsync` grava o registro de saída (`_exitRecordRepository.CreateAsync` → `SaveChanges`) e **depois** inativa o animal (`UpdateAnimalAsync` → outro `SaveChanges`) | **Condição 1.** Uma queda entre as duas deixa um registro de saída de animal **ativo**; o reenvio criaria um segundo registro | Registro adicionado pela navegação `ExitRecords`, num único `SaveChanges` (N5) |
| 5 | **Bug pré-existente — confirmado na Fase 1** (§6.1) em `ExitAnimalAsync` (linha 188): `animal.ExitRecords = new List<AnimalExitRecord> { exitRecord }` substitui a coleção rastreada. Se o animal já saiu antes e foi reativado, o `GetAnimalByIdAsync` carregou o registro anterior (`Include` com `Take(1)`); ao sair da coleção ele vira **órfão** de uma relação obrigatória e o EF o marca para **exclusão** | A segunda saída de um animal apaga fisicamente a primeira do histórico (R9 quebrada) — `DELETE FROM [AnimalExitRecords]` no log | Corrigido na Fase 4 pela N5 (`Add` na coleção, sem substituir) |
| 6 | `ExitAnimalAsync` e `ReactivateAnimalAsync` lançam `ConflictException` se o animal já está no estado pedido | Violam a A6 | Retornam o animal sem gravar (Fase 4) |
| 7 | `AnimalRepository.UpdateAnimalAsync` chama `_context.Animals.Update(animal)` num animal **já rastreado** (veio de `GetAnimalByIdAsync`), marcando o grafo inteiro como modificado: **todas** as pesagens, o último ECC e o último registro de saída | Hoje: `UPDATE`s desnecessários. Quando pesagem/ECC virarem sincronizáveis, **toda edição do animal faria as pesagens reaparecerem no pull** | **Não confirmado na Fase 1:** nenhum `UPDATE` em pesagens, ECC ou saídas no log — o `Update()` não reprocessa entidades já rastreadas. Sem mudança (N7 descartada) |
| 8 | `AnimalProfile` (edição): `ForAllMembers(srcMember != null)` | Com `UpdatedAt` no DTO, o valor bruto do cliente seria copiado antes do LWW | Profile ignora `UpdatedAt`, `SyncId` e `RowVersion`. Os demais pares são anulável → anulável (sem o bug do `RequiresBooster` da 14.1) |
| 9 | `AnimalDto` (detalhe) é composto por ~6 consultas **por animal** (reprodutivo: 3; produtivo: 1; genealogia: 1; sanitário: 1), e os campos derivados vêm de gestação, parto, lactação, casos de saúde e pesagens | No pull: 500 animais por página → ~3.000 consultas; e os derivados mudam sem avançar o `RowVersion` do animal | Pull com DTO próprio, só com os dados do animal e as saídas (N6) |
| 10 | `AnimalExitRecord` e `BodyConditionRecord` **não** têm `PropertyId` nem `HasQueryFilter` (o tenant vem pelo animal) | `ConfigureSyncable()` exige `ITenantEntity`; sincronizá-los sozinhos exigiria migração com backfill de `PropertyId` | Saída **embutida** no pull do animal, como o `VaccinationEventAnimal` (Spec #14 §5.9) — N5. ECC fica para a spec dele (N2) |
| 11 | Animais criados pelo parto (`BuildCalfAnimal`) e inativados junto com o parto (`AnimalCalvingService.InactivateAsync`, linha 155) | Recebem `SyncId` do banco (`NEWID()`) e aparecem no pull como qualquer animal (inclusive o tombstone quando o parto é inativado) | Nenhum agora. A criação offline deles é da spec do parto (Condição 2, exemplo da §7.5) |
| 12 | Não há índice único de brinco no banco; a unicidade é só a consulta do R2 | Dois `POST` simultâneos com brincos iguais e `SyncId` diferentes poderiam passar juntos | Sem mudança (pré-existente; fila única por celular torna o caso raro). Questão Q1 |
| 13 | `AnimalCreateDto` não tem `syncId`; `AnimalDto` não tem `syncId` | — | Receita §7.4 itens 5 e 6 |

## 4. Decisões (N1–N6 aprovadas em 09/Out/2026; N7 descartada)

As decisões marcadas com ⭐ eram as que pediam escolha da usuária (aprovadas conforme a recomendação); as demais seguem precedentes das specs 14.2 e 14.3.

| # | Decisão proposta | Alternativa descartada |
|---|---|---|
| N1 | **Cadastro atômico pela navegação `Animal.Lactations`:** `Animal` ganha `ICollection<Lactation>? Lactations` e a relação passa a `WithMany(a => a.Lactations)` (FK, índices e `Restrict` inalterados — **sem mudança no banco**; a mudança do snapshot entra na migração da Fase 1, sem migração vazia). O service preenche `animal.Lactations` com a lactação inicial (`CreateInitialLactation`, mesmos campos do `SeedInitialLactationAsync` atual, sem `PropertyId` — o override do `SaveChangesAsync` preenche), **no mesmo padrão** de `CreateWeightRecord`/`CreateBodyConditionRecord`. `CreateAnimalAsync(Animal)` mantém a assinatura e passa a usar `AddSyncableAsync` — **um `SaveChanges`** para animal, pesagem, ECC e lactação. `SeedInitialLactationAsync` é removido | Repositório recebendo a lactação à parte (`CreateAnimalAsync(animal, initialLactation)` com `_context.Lactations.Add`) — evitava mudar o modelo, mas criava um caminho diferente só para a lactação e mudava a assinatura do repositório. Transação explícita no service (exigiria expor transação no repositório) |
| N2 | **Sem `SyncId` gerado pelo app para pesagem, ECC e lactação iniciais agora** (mesma escolha da S1 da 14.2): elas ainda não são sincronizáveis, então o app não pode editá-las offline. Recebem `SyncId` no backfill da migração de cada uma, e o `POST` do animal ganha `initialWeightSyncId` / `initialBodyConditionSyncId` / `initialLactationSyncId` na spec de cada entidade (aditivo, como a M2 da 14.2 / E1 da 14.3) | Tornar as três sincronizáveis agora — três tabelas a mais, `PropertyId` novo no ECC (§3 item 10), e o escopo dobraria |
| N3 ⭐ | **Brinco duplicado entre celulares continua `409`** (já decidido na Spec #14 §3.3). Fluxo no app: o item vira `Failed`, a fila para (A3), o usuário troca o brinco no animal local e o app **reenvia o mesmo item com o mesmo `syncId`** e o brinco novo — permitido porque nada foi gravado. Os itens dependentes (eventos desse animal) esperam na fila | Aceitar e marcar o animal como "brinco em conflito" — regra nova (R2 deixaria de ser garantida). Gerar brinco automático — muda o domínio |
| N4 | **No `PATCH`: R1 → LWW → R2.** Uma edição mais antiga é descartada **sem** checar o brinco. Com R2 antes do LWW, uma edição que seria descartada poderia devolver `409` e travar a fila | R2 antes do LWW (padrão da 14.3 para R1/R2 de lá, que são `404` de existência, não conflito) |
| N5 ⭐ | **Saída e reativação como na S2 da 14.2:** mudanças de estado **sem LWW**, as duas idempotentes (`200` com o animal, **sem gravar**, se já estiver no estado pedido). A saída grava o registro pela navegação (`animal.ExitRecords.Add`, sem substituir a coleção — §3 item 5) num único `SaveChanges`. O **registro de saída não ganha `SyncId`**: não tem rota de edição nem de exclusão, então o app nunca precisa endereçá-lo; ele chega ao app **embutido** no pull do animal (a saída e a reativação atualizam o animal, então o `RowVersion` dele avança). **Não contradiz a D6:** o `Animal` não tem `DELETE`; `IsActive = false` aqui é "fora do rebanho", reversível, exatamente como a inativação/reativação da amostra de sêmen. `PATCH` em animal inativo continua editando sem reativar (R7) | Saída com LWW (`updatedAt` no corpo) — regra diferente da Spec #14 §5.4 para o mesmo campo. `SyncId` + `ISyncable` no `AnimalExitRecord` — exigiria `PropertyId` com backfill (§3 item 10) para um registro que o app não endereça |
| N6 ⭐ | **Pull com DTO próprio, `AnimalSyncDto`:** só os dados do próprio animal (`id`, `syncId`, campos do cadastro com `EnumValueDto`, `isActive`, `createdAt`, `updatedAt`) e `exitRecords` (**todos** os registros de saída, inclusive de saídas anteriores a uma reativação). **Sem** os derivados do detalhe (status reprodutivo, produtivo, sanitário, DEL, previsão de parto, genealogia, última pesagem/ECC): eles vêm de outros recursos, que terão o próprio pull; o app recalcula com as mesmas funções puras do servidor (`ReproductiveStatusResolver`, `ProductiveStatusResolver`), como a 14.3 §5 item 4 já exigiu para o estoque. Até esses recursos serem sincronizáveis, as telas que mostram derivados continuam usando `GET /api/animals` e `GET /{id}` online. **2 consultas por página**, constante (pull + saídas dos animais da página) | (a) **Mesmo `AnimalDto`, com derivados em lote** (como a E3 da 14.3): exigiria lotes novos para partos, previsão de parto e genealogia, e os derivados envelheceriam sem o animal reaparecer no pull. (b) **Híbrido:** `AnimalSyncDto` + os status da listagem (`reproductiveStatus`, `productiveStatus`, `daysInMilk`, `sanitaryStatus`, `milkWithheldUntil`) em lote, com os métodos que o `GetAllAnimalsAsync` já usa (+3 consultas por página) — dá badges à lista offline desde já, mas como "valor inicial" que fica desatualizado até as specs dos eventos |
| ~~N7~~ | **Descartada após a validação da Fase 1** (§3 item 7 não se confirmou; `UpdateAnimalAsync` fica como está). Proposta original: **`UpdateAnimalAsync` sem `_context.Animals.Update(...)`**: o animal já está rastreado, então basta `SaveChangesAsync` — só o que mudou é gravado (§3 item 7). O único chamador é o `AnimalService` | Manter o `Update()` e tratar na spec da pesagem (o problema reapareceria ali, mais caro de diagnosticar) |

> **Sobre a N6 e a regra "mesmo DTO do detalhe" (Spec #14 §5.5):** é a primeira exceção. Justificativa: no estoque, os derivados eram calculados a partir de dados que chegavam **no mesmo ciclo de pull** (as movimentações); no animal, eles dependem de cinco recursos que **ainda não** são sincronizáveis, e o custo por animal (~6 consultas) é incompatível com o servidor fraco. A exceção deve ser registrada na §5.5 da Spec #14 na Fase 7.

## 5. Contrato do app (complemento da Spec #14 §6)

1. **Ordem:** o animal criado offline é enviado **antes** de qualquer item que o referencie (saída, reativação, e futuramente os eventos); o `Id` do `201` é usado nas rotas (A2, A5).
2. **Pesagem, ECC e lactação iniciais** vão **dentro** do `POST` do animal (um único item na fila). O app **deve** enviar `initialWeightDate` e `initialBodyConditionDate` com o momento do cadastro: se omitidas, o servidor usa o momento do processamento (R5), que no offline pode ser dias depois.
3. **Enquanto pesagem, ECC e lactação não forem sincronizáveis (N2):** os registros iniciais existem localmente só como parte do animal; editá-los, inativá-los ou criar outros continua exigindo conexão.
4. **`409` de brinco (N3):** conflito real. O item vira `Failed`, o usuário é avisado e corrige o brinco; o app atualiza o corpo **do mesmo item** (mesmo `syncId`) e retoma a fila. Vale também para o `PATCH` com brinco em uso.
5. **Saída e reativação:** itens de fila sem `updatedAt`. No `200`, o app sobrescreve o animal local com a resposta. Se a resposta mostrar um estado diferente do que o usuário registrou (ex.: outro celular registrou a saída antes, com outro motivo), vale o do servidor (Spec #14 §3.3, efeito colateral aceito).
6. **Pull (`GET /api/animals/changes`):** upsert por `syncId`; os registros de saída do animal são **substituídos** pela lista `exitRecords` recebida (coleção do animal, sem identidade própria no app). Status reprodutivo, produtivo e sanitário **não** vêm no pull (N6).
7. **Animais criados pelo parto** chegam pelo pull com `syncId` gerado pelo servidor; o app os trata como qualquer animal (podem ser editados, sair e ser reativados offline).
8. **Datas não futuras** (R4): saída, ECC inicial e lactação inicial com relógio do celular adiantado podem gerar `400` definitivo — mesmo risco da 14.3 §5 item 8.

## 6. Fases de implementação e validação

| Fase | Parte | Status |
|---|---|---|
| 1 — Domínio e banco (`Animals`) | A | ✅ Migração `20261009230440_Offline_Animal_SyncId_RowVersion` aplicada |
| 2 — Criação idempotente e atômica (N1, N2, N3) | A | ✅ Implementada e validada (+ correção da corrida com o brinco) |
| 3 — Edição com last-write-wins (N4, R6) | A | ✅ Implementada e validada |
| 4 — Saída e reativação idempotentes e atômicas (N5 + §3 item 5) | B | ✅ Implementada e validada (bug do histórico corrigido) |
| 5 — Pull incremental (N6) | A + B | ✅ Implementada e validada |
| 6 — Testes | — | ✅ 23 testes em `AnimalServiceTests`; 142 no projeto |
| 7 — Documentação | — | ✅ Spec #14 (v1.3), `catalogo-erros-api.md` §4.3, `spec-animais.md`, `spec-entrada-saida-animais.md` |

Branch: `feature/offline-animals`, a partir do `dev` (já com o estoque, PR #39). Cada fase é proposta com o código exato e só implementada após aprovação. Validação manual na API local com uma conta de teste própria (`teste.offline.animais@muuboi.local`), registros de teste inativados (saída) ao final.

### 6.1 Fase 1 — Domínio e banco

- `Animal : BaseEntity, ITenantEntity, ISyncable` — `Guid SyncId`, `byte[] RowVersion = Array.Empty<byte>()`.
- `Animal`: navegação `public ICollection<Lactation>? Lactations { get; set; }` (N1).
- `ApplicationDbContext`:
  - `builder.Entity<Animal>().ConfigureSyncable();` (o `IX_Animals_PropertyId` existente permanece);
  - relação da lactação com o animal passa a `WithMany(a => a.Lactations)` (N1):

```csharp
builder.Entity<Lactation>()
    .HasOne(l => l.Animal)
    .WithMany(a => a.Lactations)
    .HasForeignKey(l => l.AnimalId)
    .OnDelete(DeleteBehavior.Restrict);
```

- `AnimalProfile` (criação): `.ForMember(dest => dest.Lactations, opt => opt.Ignore())`, como `WeightRecords` e `BodyConditionRecords`.
- As **tabelas** `AnimalExitRecords`, `WeightRecords`, `BodyConditionRecords` e `Lactations` **não** mudam (N2, N5).
- Migração `Offline_Animal_SyncId_RowVersion` (⚠️ **requer aprovação**). O snapshot ganha `WithMany("Lactations")`, sem SQL correspondente. SQL esperado:

```sql
ALTER TABLE [Animals] ADD [RowVersion] rowversion NOT NULL;
ALTER TABLE [Animals] ADD [SyncId] uniqueidentifier NOT NULL DEFAULT (NEWID());
CREATE INDEX [IX_Animals_PropertyId_RowVersion] ON [Animals] ([PropertyId], [RowVersion]);
CREATE UNIQUE INDEX [UX_Animals_SyncId] ON [Animals] ([SyncId]);
```

| Validação (09/Out/2026) | Resultado |
|---|---|
| Animais existentes (inclusive crias criadas pelo parto) ganham `SyncId` distintos, nenhum vazio, e `RowVersion` preenchido | ✅ 23 animais (3 deles crias de parto); 23 `SyncId` distintos, nenhum vazio; `RowVersion` 38281–38303 |
| Índices novos criados; existentes mantidos | ✅ `PK_Animals`, `IX_Animals_PropertyId`, `IX_Animals_PropertyId_RowVersion`, `UX_Animals_SyncId` |
| Nenhuma outra tabela alterada (inclusive `Lactations`: FK `FK_Lactations_Animals_AnimalId` com `Restrict` igual) | ✅ SQL gerado só toca `Animals`; FK da lactação continua `NO_ACTION` (o `Restrict` do EF) |
| Snapshot: única mudança fora de `Animals` é a navegação `Lactations` | ✅ `WithMany("Lactations")` + `b.Navigation("Lactations")`; nada mais |
| Cadastro, edição, saída, reativação e parto com cria continuam funcionando, com `SyncId` gerado pelo banco | ✅ Vaca 25 com pesagem (450), ECC (3) e lactação inicial seca → `201`; `PATCH` → `200`; gestação retroativa + parto com cria (animal 26) → `201`; saída → reativação → `DELETE` do parto (cria inativada) → saída final, todos `2xx`. Animais 24–26 com `SyncId` e `RowVersion`; lactações com o `PropertyId` do animal |
| Testes existentes | ✅ 119/119 |

- SQL gerado conferido com `dotnet ef migrations script` antes do `database update`: idêntico ao esperado.
- O primeiro roteiro (vaca 24) usou lactação inicial **aberta** e o parto foi barrado com `422` "O animal possui uma lactação em aberto..." — regra existente, correta; refeito com lactação seca (vaca 25).
- **§3 item 5 confirmado** nas vacas 24 e 25 (sair → reativar → sair): cada uma ficou com **1** registro de saída; o log do EF mostra `INSERT` seguido de `DELETE FROM [AnimalExitRecords]` na segunda saída. Corrigido na Fase 4.
- **§3 item 7 não confirmado:** no log inteiro do roteiro (9 `UPDATE [Animals]`), **nenhum** `UPDATE [WeightRecords]`, `[BodyConditionRecords]` ou `[AnimalExitRecords]`. O `Update()` do EF não reprocessa entidades que já estão rastreadas no grafo. **N7 descartada.**
- Conta de teste: `teste.offline.animais@muuboi.local` (propriedade "Fazenda Teste Offline Animais"). Animais 24, 25 e 26 terminam inativos.

### 6.2 Fase 2 — Criação idempotente e atômica

- `AnimalCreateDto`: `Guid? SyncId` + validação de `Guid.Empty` no `Validate` existente ("O identificador de sincronização não pode ser vazio.").
- `AnimalDto`: `Guid SyncId`.
- `AnimalProfile` (criação): ignora `SyncId` e `RowVersion`.
- `IAnimalRepository`/repositório:
  - `GetBySyncIdAsync` com os mesmos `Include` do `GetAnimalByIdAsync` (o reenvio devolve o mesmo corpo do `201`);
  - `CreateAnimalAsync` → `AddSyncableAsync` (mesma assinatura; animal, pesagem, ECC e lactação vão pelas navegações — N1):

```csharp
public async Task<Animal> CreateAnimalAsync(Animal animal)
{
    return await _context.AddSyncableAsync(animal);
}
```

- `AnimalService.CreateAnimalAsync`:

```csharp
if (dto.SyncId.HasValue)
{
    var existing = await _animalRepository.GetBySyncIdAsync(dto.SyncId.Value);
    if (existing != null)
        return _mapper.Map<AnimalDto>(existing);
}

if (await _animalRepository.TagNumberExistsAsync(dto.TagNumber))
    throw new ConflictException($"Já existe um animal com o brinco '{dto.TagNumber}' nesta propriedade.");

if (dto.InitialLactation != null
    && dto.Classification != AnimalClassification.Cow
    && dto.Classification != AnimalClassification.Heifer)
    throw new BusinessRuleException("A lactação inicial só se aplica a vacas e novilhas.");

var animal = _mapper.Map<Animal>(dto);
animal.SyncId = dto.SyncId ?? Guid.NewGuid();
CreateWeightRecord(dto, animal);
CreateBodyConditionRecord(dto, animal);
CreateInitialLactation(dto, animal);

var created = await _animalRepository.CreateAnimalAsync(animal);
return _mapper.Map<AnimalDto>(created);
```

- `CreateInitialLactation` (substitui `SeedInitialLactationAsync`), no padrão de `CreateWeightRecord`:

```csharp
private static void CreateInitialLactation(AnimalCreateDto dto, Animal animal)
{
    if (dto.InitialLactation == null) return;

    animal.Lactations = new List<Lactation>
    {
        new()
        {
            StartDate = dto.InitialLactation.StartDate,
            EndDate = dto.InitialLactation.EndDate,
            CalvingId = null,
            Origin = LactationOrigin.InitialSeed,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        }
    };
}
```
- `IAnimalService` não muda; `ILactationRepository` continua injetado (usado nos status).

| Validação (09/Out/2026) | Resultado |
|---|---|
| `POST` com `syncId`, pesagem, ECC e lactação iniciais | ✅ `201`, animal 27 com o `syncId` do app; 1 pesagem (410), 1 ECC, 1 lactação, todos com o `PropertyId` do animal |
| Mesmo `POST` repetido | ✅ `201`, mesmo `Id` (27), mesmo corpo (pesagem e ECC); nada duplicado |
| Mesmo `syncId`, payload diferente (nome e brinco) | ✅ `201`, devolve o existente sem alterar ("F2 A") |
| `POST` novo (outro `syncId`) com brinco em uso | ✅ `409` "Já existe um animal com o brinco '...' nesta propriedade." (R2, mantido) |
| Reenvio de `syncId` existente com lactação para um macho | ✅ `201` com o existente — `SyncId` checado antes de R3 |
| `POST` sem `syncId` (Swagger/chamada direta) | ✅ `201`, animal 28, `syncId` gerado |
| `syncId` vazio | ✅ `400` (formato B) no campo `SyncId` |
| R3 com `syncId` novo | ✅ `422` "A lactação inicial só se aplica a vacas e novilhas." |
| Falha simulada na lactação (trigger temporário `THROW` em `Lactations`, removido após o teste) | ✅ `500`; **nenhum** animal com o `syncId` enviado, nenhuma pesagem órfã (rollback) |
| Reenvio do `POST` que falhou (duas vezes) | ✅ `201`, animal 32 com 1 pesagem, 1 ECC e 1 lactação; segundo reenvio devolve o mesmo |
| 20 envios simultâneos (mesmo `syncId`) — antes da correção | ⚠️ 19 × `201` e **1 × `409`** de brinco; 1 animal (29). Ver nota abaixo |
| 10 rodadas × 20 envios simultâneos — depois da correção | ✅ **200/200 `201`**; 10 animais, 10 `SyncId`, 10 pesagens (1 por rodada). Identity com saltos: o `catch` do `UX_Animals_SyncId` exercitado várias vezes |
| Brinco realmente duplicado (outro `syncId`) depois da correção | ✅ Continua `409` |
| Testes existentes | ✅ 119/119 |

- **Achado da corrida (novo em relação às specs anteriores):** o animal é a primeira entidade com regra de unicidade **entre** a checagem do `SyncId` e o `INSERT`. Se o original grava (commit) depois que o reenvio passou pela checagem do `SyncId` e antes da checagem do brinco, o reenvio encontra o **próprio** brinco e responde `409` — erro definitivo para o app (A3), embora a operação tenha dado certo. As demais requisições caíram nos dois caminhos esperados: `SyncId` encontrado pelo service, ou `INSERT` barrado no `UX_Animals_SyncId` e existente devolvido pelo `AddSyncableAsync` (identity pulou o 30 — o `catch` foi exercitado). No app, a janela é a de um reenvio por timeout enquanto o original ainda é processado num servidor lento. **Correção (aprovada e aplicada em 09/Out/2026):** quando R2 encontra o brinco em uso e o `POST` tem `syncId`, o service busca o `SyncId` de novo antes de lançar o `409`; se existir, devolve o existente. Se o brinco foi visto, o original já fez commit, então a segunda busca o encontra. A consulta extra só roda no caminho de erro; R2 não muda.

```csharp
if (await _animalRepository.TagNumberExistsAsync(dto.TagNumber))
{
    if (dto.SyncId.HasValue)
    {
        var concurrent = await _animalRepository.GetBySyncIdAsync(dto.SyncId.Value);
        if (concurrent != null)
            return _mapper.Map<AnimalDto>(concurrent);
    }

    throw new ConflictException($"Já existe um animal com o brinco '{dto.TagNumber}' nesta propriedade.");
}
```

- **Teste novo na Fase 6:** `CreateAnimalAsync_WhenTagInUseBySameSyncId_ReturnsExisting`.
- **Lição para as próximas specs:** toda regra de unicidade/estado checada entre o `SyncId` e o `INSERT` precisa do mesmo tratamento (ex.: "gestação ativa" na spec da gestação, "parto ativo" na do parto).
- Validação com script Node (`fetch`) na conta `teste.offline.animais@muuboi.local`. Animais de teste (27, 28, 29, 32, 33, 49, 59, 61, 66, 72, 77, 81, 86, 91) inativados por saída ao final.

### 6.3 Fase 3 — Edição com last-write-wins

- `AnimalUpdateDto`: `DateTime? UpdatedAt`.
- `AnimalProfile` (edição): ignora `UpdatedAt`, `SyncId` e `RowVersion` (§3 item 8).
- `AnimalService.UpdateAnimalAsync` (N4):

```csharp
var animal = await _animalRepository.GetAnimalByIdAsync(id)
    ?? throw new NotFoundException($"Animal com id '{id}' não encontrado.");

var editedAt = SyncTimestampResolver.ResolveEditedAt(dto.UpdatedAt, DateTime.UtcNow);
if (SyncTimestampResolver.IsOutdated(editedAt, animal))
    return _mapper.Map<AnimalDto>(animal);

if (dto.TagNumber != null && await _animalRepository.TagNumberExistsAsync(dto.TagNumber, excludeAnimalId: id))
    throw new ConflictException($"Já existe um animal com o brinco '{dto.TagNumber}' nesta propriedade.");

var previousGender = animal.Gender;
_mapper.Map(dto, animal);
animal.UpdatedAt = editedAt;
```

- R6 (sexo da cria) continua igual, com `calf.UpdatedAt = editedAt`.

| Validação (09/Out/2026) | Resultado |
|---|---|
| `updatedAt` UTC mais novo | ✅ Aplicado; `updatedAt` = horário do cliente (`23:20:59.703Z`) |
| `updatedAt` com fuso `-03:00` | ✅ `20:21:00.782-03:00` gravado como `23:21:00.782Z`; aplicado |
| Sem `updatedAt` (Swagger/chamada direta) | ✅ Aplicado com "agora" |
| Relógio adiantado (+1 dia) | ✅ Aplicado; `updatedAt` limitado a "agora" |
| Reenvio idêntico | ✅ `200`, mesmo resultado e mesmo `updatedAt` |
| Edição mais antiga (−1 h) | ✅ `200`, ignorada; nome mantido |
| Edição mais antiga com brinco de outro animal | ✅ `200`, ignorada — **sem** `409`; brinco mantido (N4) |
| Edição nova com brinco em uso | ✅ `409` "Já existe um animal com o brinco '...' nesta propriedade." (R2) |
| `PATCH` só `notes` | ✅ Nome, brinco, raça, classificação e sexo preservados |
| Troca de sexo de cria vinculada a parto (M → F) | ✅ Animal e `AnimalCalvingCalves.Sex` = F; `UpdatedAt` da cria = `UpdatedAt` do animal (`editedAt`) (R6) |
| Troca de sexo mais antiga (F → M, −1 h) | ✅ `200`, ignorada; animal e cria continuam F |
| `PATCH` em animal inativo | ✅ `200`, edita e continua inativo (R7) |
| R1 | ✅ `404` "Animal com id '999999' não encontrado." |
| Testes existentes | ✅ 119/119 |

- O primeiro roteiro usou horários de edição (−20 s a −1 s) **anteriores à criação** do animal, feita milissegundos antes; o LWW descartou essas edições corretamente (`IsOutdated` compara com `CreatedAt` quando não há `UpdatedAt`). Refeito com pausa após a criação e horários entre a criação e "agora".
- Como nas specs anteriores, quando a edição é ignorada o `updatedAt` da resposta sai sem `Z` (lido do banco) — Spec #14 §10.2.
- Animais de teste (97–104) inativados por saída ao final.

### 6.4 Fase 4 — Saída e reativação idempotentes e atômicas

```csharp
public async Task<AnimalDto> ExitAnimalAsync(int id, AnimalExitDto dto)
{
    var animal = await _animalRepository.GetAnimalByIdAsync(id)
        ?? throw new NotFoundException($"Animal com id '{id}' não encontrado.");

    if (!animal.IsActive)
        return _mapper.Map<AnimalDto>(animal);

    animal.ExitRecords ??= new List<AnimalExitRecord>();
    animal.ExitRecords.Add(new AnimalExitRecord
    {
        ExitReason = dto.ExitReason,
        ExitDate = dto.ExitDate,
        ExitNotes = dto.ExitNotes,
        CreatedAt = DateTime.UtcNow
    });

    animal.IsActive = false;
    animal.UpdatedAt = DateTime.UtcNow;

    var updated = await _animalRepository.UpdateAnimalAsync(animal);
    return _mapper.Map<AnimalDto>(updated);
}
```

- `LastExitRecord` da resposta: o profile pega `ExitRecords.FirstOrDefault()`; com o `Add`, o novo registro fica no **fim** da coleção. A resposta precisa ordenar por `ExitDate` (ou `Id`) decrescente — ajuste no profile (`OrderByDescending(e => e.ExitDate).ThenByDescending(e => e.Id)`), sem efeito nas outras rotas, que já carregam só um registro.
- `IAnimalExitRecordRepository.CreateAsync` deixa de ser usado pelo `AnimalService` (remover se não houver outro chamador). O `AnimalService` continua dependendo do repositório para `GetByAnimalIdAsync`.
- `ReactivateAnimalAsync`: `if (animal.IsActive) return _mapper.Map<AnimalDto>(animal);`.
- As mensagens "Não é possível registrar saída de um animal já inativo." e "Não é possível reativar um animal que já está ativo." deixam de existir.

| Validação (09/Out/2026) | Resultado |
|---|---|
| **Antes da mudança:** sair → reativar → sair → `GET /{id}/exit-records` | ✅ Bug confirmado já na Fase 1 (vacas 24 e 25: 1 registro; `DELETE FROM [AnimalExitRecords]` no log) |
| **Depois:** mesmo roteiro (animal 105) | ✅ 2 registros (29 "Consumo próprio", 28 "Venda"); `lastExitRecord` = 29 no `PATCH` e no `GET /{id}`; **nenhum** `DELETE` no log |
| Saída em animal ativo | ✅ `200`; animal inativo, registro 28 criado. Log: `INSERT [AnimalExitRecords]` e `UPDATE [Animals]` no **mesmo comando** |
| Saída repetida | ✅ `200`, mesmo `lastExitRecord`; nada gravado |
| Saída em animal já inativo, com outro motivo | ✅ `200` com a saída original ("Venda") (§5 item 5) |
| Reativação / reativação repetida | ✅ `200`; a repetida não grava. No roteiro inteiro: 3 `UPDATE [Animals]` (saída, reativação, segunda saída) e 2 `INSERT` de saída — as 3 repetições não gravaram |
| Falha simulada na inativação (trigger `THROW` em `Animals` para `UPDATE` do animal 106, removido após o teste) | ✅ `500`; animal continua ativo, **nenhum** registro de saída |
| Reenvio depois da falha | ✅ `200`; animal inativo com 1 registro |
| R1 | ✅ `404` nas duas rotas |
| Testes existentes | ✅ 119/119 |

- `CreateAsync` removido de `IAnimalExitRecordRepository`/`AnimalExitRecordRepository` (o `AnimalService` era o único chamador). DI sem mudança.
- O `LastExitRecord` ordenado vale só para o `AnimalDto`; a listagem (`AnimalListItemDto`) já carrega um registro só (`Include` com `Take(1)`).
- Animais de teste 105 e 106 terminam inativos.

### 6.5 Fase 5 — Pull incremental

- `AnimalSyncDto` (**novo**, N6): `Id`, `SyncId`, `Name`, `TagNumber`, `PropertyTagNumber`, `Gender`, `BirthDate`, `Breed`, `Classification`, `Purpose`, `Origin` (`EnumValueDto`, como no `AnimalDto`), `Notes`, `IsActive`, `CreatedAt`, `UpdatedAt`, `IEnumerable<AnimalExitRecordDto> ExitRecords`.
- `AnimalProfile`: `CreateMap<Animal, AnimalSyncDto>()` com os mesmos `MapFrom` de enum do `AnimalDto` e `ExitRecords` ordenados por `ExitDate` decrescente.
- `IAnimalRepository.GetChangesAsync(ulong since, int take)` → `GetChangesSinceAsync<Animal>`.
- `IAnimalExitRecordRepository.GetByAnimalIdsAsync(IReadOnlyCollection<int> ids)` — os ids vêm da página (já filtrada por tenant), então a falta de `HasQueryFilter` no registro de saída não abre vazamento.
- `IAnimalService.GetChangesAsync(string? since, int? limit)` → `SyncPageDto<AnimalSyncDto>`: `TryDecodeCursor` → `ResolveLimit` → repositório com `take + 1` → saídas da página em lote, atribuídas a `animal.ExitRecords` → `BuildPage`.
- `AnimalsController`: `[HttpGet("changes")]` — sem colisão com `{id:int}`.

| Validação (09/Out/2026) | Resultado |
|---|---|
| Pull inicial | ✅ 27 animais (todos inativos de testes anteriores, inclusive as crias 26, 100 e 104 do parto), em ordem de `RowVersion`; `nextCursor` `38447`, `hasMore: false` |
| Pull × `GET /{id}` e `GET /{id}/exit-records`, item a item | ✅ **Nenhuma divergência** — campos do cadastro, enums, `isActive`, `createdAt`, `updatedAt`; quantidade de saídas igual ao histórico e `exitRecords[0]` igual ao `lastExitRecord` |
| Isolamento por propriedade | ✅ A tabela tinha 54 animais; vieram só os 31 da propriedade do usuário (27 + 4 criados no roteiro) |
| Cursor sem mudanças | ✅ `items: []`, `nextCursor` mantido, `hasMore: false` |
| Cria A, edita A, cria B, sai B, reativa B → pull | ✅ Só A (107, "F5 A editado", 0 saídas) e B (108, ativo, 1 saída), cada um **uma vez** |
| Pesagem nova em A → pull | ✅ `items: []` — pesagem não avança o animal (esperado; vem na spec da pesagem) |
| Inativar um parto com cria → pull | ✅ Só a cria (110), inativa, 0 saídas — a mãe não muda (o parto altera gestação e lactação, não o animal) |
| Paginação `limit=1` | ✅ 31 páginas, 31 itens distintos, mesma ordem do pull completo |
| `since=abc` / `since=-5` | ✅ `400` "Cursor de sincronização inválido." |
| `limit=0` / `limit=100000` | ✅ `200` (ajustados para 500) |
| Comandos SQL por página (log do EF) | ✅ **2** com 1 item e **2** com 31 itens — pull + saídas da página, constante |
| Testes existentes | ✅ 119/119 |

- Animais de teste (107–110) inativados ao final.

### 6.6 Fase 6 — Testes

`MuuBoi.Tests/Services/AnimalServiceTests.cs` (**novo**) — padrão de `StockItemServiceTests` (repositórios mockados, AutoMapper real com `AnimalProfile`, sem testar mapeamento). O `IHealthCaseService` também é mockado.

| # | Teste |
|---|---|
| 1 | `CreateAnimalAsync_WithNewSyncId_CreatesAnimalWithGivenSyncId` |
| 2 | `CreateAnimalAsync_WithExistingSyncId_ReturnsExistingWithoutCreating` |
| 3 | `CreateAnimalAsync_WithExistingSyncId_DoesNotCheckTagNumber` |
| 4 | `CreateAnimalAsync_WithoutSyncId_GeneratesSyncId` |
| 5 | `CreateAnimalAsync_WithTagNumberInUse_ThrowsConflictException` (R2) |
| 6 | `CreateAnimalAsync_WithInitialLactationForMale_ThrowsBusinessRuleException` (R3) |
| 7 | `CreateAnimalAsync_WithInitialLactation_AttachesLactationToAnimal` (N1) |
| 8 | `CreateAnimalAsync_WithInitialWeightAndBodyCondition_AttachesRecordsToAnimal` |
| 9 | `UpdateAnimalAsync_WithNewerClientUpdatedAt_AppliesChanges` |
| 10 | `UpdateAnimalAsync_WithOlderClientUpdatedAt_KeepsServerVersion` |
| 11 | `UpdateAnimalAsync_WithOlderClientUpdatedAt_DoesNotCheckTagNumber` (N4) |
| 12 | `UpdateAnimalAsync_WithNewerClientUpdatedAtAndTagInUse_ThrowsConflictException` |
| 13 | `UpdateAnimalAsync_WhenGenderChanges_UpdatesLinkedCalfSex` (R6) |
| 14 | `UpdateAnimalAsync_WhenAnimalNotFound_ThrowsNotFoundException` (R1) |
| 15 | `ExitAnimalAsync_WhenActive_AddsExitRecordAndDeactivatesInSameCall` |
| 16 | `ExitAnimalAsync_WhenAlreadyInactive_ReturnsWithoutUpdating` |
| 17 | `ExitAnimalAsync_WhenPreviousExitExists_KeepsPreviousRecord` (§3 item 5) |
| 18 | `ReactivateAnimalAsync_WhenInactive_Reactivates` |
| 19 | `ReactivateAnimalAsync_WhenAlreadyActive_ReturnsWithoutUpdating` |
| 20 | `GetChangesAsync_WithInvalidCursor_ThrowsValidationException` |
| 21 | `GetChangesAsync_WhenMoreThanLimit_ReturnsHasMoreAndLastItemCursor` |
| 22 | `GetChangesAsync_FillsExitRecordsForPageItems` |
| 23 | `CreateAnimalAsync_WhenTagInUseBySameSyncId_ReturnsExisting` (corrida da Fase 2) |

- Os testes 5, 6, 13 e 14 fixam regras que já existem e hoje não têm cobertura (não há `AnimalServiceTests`).
- O mapper dos testes usa `cfg.AddMaps(typeof(AnimalProfile).Assembly)` (o `AnimalDto` também mapeia pesagem e ECC, cujos mapas estão em outros profiles), em vez de só o `AnimalProfile`.
- **Limite do teste 17:** com o repositório mockado, ele garante que o service **adiciona** à coleção sem substituí-la; a exclusão física pelo EF (§3 item 5) só aparece com banco real e foi coberta pela validação manual da Fase 4.
- **Resultado (09/Out/2026):** 23/23 em `AnimalServiceTests`, na primeira execução; projeto inteiro **142/142**.

### 6.7 Fase 7 — Documentação

- **Esta spec:** status das fases e resultados.
- **Spec #14:** §8 (`Animal`, `AnimalExitRecord` → ✅); §5.5 (exceção da N6 ao "mesmo DTO do detalhe"); §5.7 (linha do `POST` animal resolvida).
- **`spec-animais.md` / `spec-entrada-saida-animais.md`:** nota apontando para esta spec.
- **`Docs/catalogo-erros-api.md` §4.3:** remover os dois `409` de estado repetido; acrescentar `400` de `syncId` vazio e do cursor; nota de rotas com suporte offline.

**Feito (09/Out/2026):**
- Spec #14 → v1.3: cabeçalho e histórico; §5.5 (exceção do `AnimalSyncDto`); §5.7 (linha do `POST` animal resolvida); §7.5 (**Condição 3** — rechecar o `SyncId` antes do `409` de regra de unicidade/estado); §8 (`Animal` e `AnimalExitRecord` ✅).
- `catalogo-erros-api.md`: §4.3 sem os dois `409` de estado repetido, com o `400` do cursor, nota de rotas offline e registro da correção do histórico de saídas; `AnimalCreateDto` com a mensagem de `syncId` vazio; item 6 da tabela de inconsistências atualizado.
- `spec-animais.md` e `spec-entrada-saida-animais.md`: nota no topo apontando para esta spec.

**Pendências observadas (fora desta spec):**
- A lactação inicial e o parto: a regra "lactação em aberto" bloqueia o parto de uma vaca cadastrada com lactação inicial aberta (comportamento correto, observado na validação da Fase 1) — o app precisa permitir a secagem offline antes do parto quando a lactação ficar sincronizável.
- Próxima spec sugerida: pesagem (`WeightRecord`) — resolver antes a questão em aberto 4 da Spec #14 (hard delete) e acrescentar `initialWeightSyncId` ao `POST` do animal (N2).

## 7. Arquivos impactados

| Camada | Arquivo | Mudança |
|---|---|---|
| Domain | `Domain/Models/Animal.cs` | `ISyncable`; navegação `Lactations` (N1) |
| Infrastructure | `Infrastructure/Data/ApplicationDbContext.cs` | `ConfigureSyncable()` no `Animal`; `WithMany(a => a.Lactations)` (N1) |
| Infrastructure | `Infrastructure/Migrations/*_Offline_Animal_SyncId_RowVersion*.cs` + snapshot | **Nova migração** (⚠️ aprovação) |
| Infrastructure | `Infrastructure/Repositories/AnimalRepository.cs` | `GetBySyncIdAsync`, `CreateAnimalAsync` via `AddSyncableAsync`, `GetChangesAsync` |
| Infrastructure | `Infrastructure/Repositories/AnimalExitRecordRepository.cs` | `GetByAnimalIdsAsync`; `CreateAsync` removido se sem uso |
| Application | `Application/DTOs/AnimalCreateDto.cs` | `SyncId?` + validação |
| Application | `Application/DTOs/AnimalUpdateDto.cs` | `UpdatedAt?` |
| Application | `Application/DTOs/AnimalDto.cs` | `SyncId` |
| Application | `Application/DTOs/AnimalSyncDto.cs` | **Novo** (N6) |
| Application | `Application/Mappings/AnimalProfile.cs` | Ignora `SyncId`/`RowVersion`/`UpdatedAt`; ordenação do `LastExitRecord`; mapa para `AnimalSyncDto` |
| Application | `Application/Interfaces/IAnimalRepository.cs`, `IAnimalExitRecordRepository.cs`, `IAnimalService.cs` | Novos métodos |
| Application | `Application/Services/AnimalService.cs` | Criação idempotente e atômica, LWW, saída/reativação idempotentes e atômicas, pull; `SeedInitialLactationAsync` → `CreateInitialLactation` |
| Api | `Api/Controllers/AnimalsController.cs` | Rota `GET changes` |
| Tests | `MuuBoi.Tests/Services/AnimalServiceTests.cs` | **Novo** |

**Não muda:** regras R1–R9, `Program.cs`/DI, `ExceptionMiddleware`, helpers de sincronização, `AnimalCalvingService` (crias), `WeightRecord`/`BodyConditionRecord`/`Lactation`/`AnimalExitRecord` no banco, rotas de tela.

## 8. Riscos

| Risco | Mitigação |
|---|---|
| Brinco duplicado entre celulares trava a fila até o usuário corrigir (N3) | Esperado (conflito real, A3). O app avisa e permite corrigir no próprio item; os dependentes esperam |
| LWW por registro inteiro: uma saída/reativação carimba `UpdatedAt = agora` e descarta edições offline mais antigas de outro celular, mesmo em campos diferentes | Mesmo comportamento já aceito na S2 da 14.2. Evolução possível: controle por versão (Spec #14 §5.3) |
| Saída reenviada depois de outro celular reativar o animal cria uma segunda saída | "Vence quem chega por último" — efeito colateral já aceito na S2 da 14.2 |
| Telas com status reprodutivo/produtivo/sanitário continuam online até as specs dos eventos (N6) | Incremental por desenho; cada spec de evento passa a alimentar o cálculo local |
| Corrida no `AddSyncableAsync`: devolve o animal pelo `FindBySyncIdAsync` puro (sem pesagens) e a lactação continua `Added` no `ChangeTracker` | Inofensivo (nenhum outro `SaveChanges` na requisição; mesmo ponto de atenção da 14.3 §6.2). Resposta com menos dados só no caso raro da corrida |

## 9. Questões em aberto

| # | Questão | Situação |
|---|---|---|
| Q1 | **Índice único de brinco no banco** (§3 item 12): `(PropertyId, TagNumber)` filtrado por `TagNumber IS NOT NULL` fecharia a corrida entre `SyncId` diferentes | ⏳ Fora desta spec (mudança de regra/esquema). Avaliar se houver relato real |
| Q2 | **Status derivados offline** (N6): quando os eventos forem sincronizáveis, o app calcula tudo localmente; até lá, online | ⏳ Reavaliar a alternativa (b) da N6 se a lista offline sem status incomodar no uso |
| Q3 | **Hard delete do `WeightRecord`** (Spec #14 §13, questão 4) | ⏳ Pré-requisito da spec da pesagem (próxima depois desta) |
| Q4 | **`PropertyId` no `BodyConditionRecord`** (§3 item 10) | ⏳ Pré-requisito da spec do ECC |
