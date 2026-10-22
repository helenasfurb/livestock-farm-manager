# Spec #14.12: Sincronização Offline — Foto do Animal

**Módulo:** Infraestrutura / Sincronização — Animais (Foto)
**Versão:** 1.0
**Data:** 21/Out/2026
**Status:** ✅ **Implementada** (Fases 1–8) na branch `feature/animal-photos`. F1, F4, F5, F14 e F15 escolhidas pela usuária em 19/Out/2026; as demais aprovadas em 21/Out/2026 ao iniciar a implementação, junto com as propostas da §9.

### Histórico de versões

| Versão | Data | Mudança |
|---|---|---|
| 0.1 | 19/Out/2026 | Rascunho: uma foto por animal (com miniatura gerada no app), rota própria dentro de animais, armazenamento no Azure Blob Storage, leitura por redirecionamento para URL SAS, LWW próprio da foto, fila de fotos separada no app |
| 0.2 | 19/Out/2026 | Versão da foto pelo **hash do conteúdo** (SHA-256) no lugar de um `PhotoId` aleatório — sugestão do Pedro: o app compara o hash e só baixa quando muda; reenvio da mesma foto vira `200` sem gravar; nome no Blob pelo hash. O `PUT` passa a exigir o hash da foto e da miniatura, conferido pelo servidor (F15) |
| 1.0 | 21/Out/2026 | Implementada: Fases 1–8. Questões da §9 fechadas com as propostas. **Achado na Fase 5 e corrigido (aprovado):** sem a connection string do Blob, todas as rotas de animais respondiam `500`; o cliente do Blob passou a ser criado no primeiro uso. `AccountServiceTests` criado (não existia). 24 testes novos (316 no total) |

**Depende de:** Spec #14 (`spec-sincronizacao-offline.md`) — §5.3 (LWW), §5.4 (mudanças de estado não alteram `UpdatedAt`), §5.5 (pull), §5.8 (`ServerId`), §5.10 (erros) · **Spec #14.4** (animal sincronizável). Esta spec **não** redecide nada da #14.
**Relaciona-se com:** `spec-animais.md` · Spec #21 `spec-exclusao-conta-perfil.md` (exclusão de conta apaga os dados da propriedade) · `Docs/catalogo-erros-api.md`

> **Por que é diferente das outras filhas da #14:** até aqui, tudo o que sincroniza é JSON pequeno gravado no SQL Server. A foto é um **arquivo binário** de centenas de KB, guardado **fora do banco**. O registro do animal continua leve e sincroniza como hoje; a foto tem rota, fila e armazenamento próprios.

---

## 1. Escopo

| Dentro | Fora |
|---|---|
| **Uma** foto por animal, com miniatura | Galeria (várias fotos por animal) |
| `PUT /api/animals/{id}/photo` (enviar/substituir) | Foto em outras entidades (casos de saúde, estoque) — a `IFileStorageService` fica reutilizável |
| `DELETE /api/animals/{id}/photo` | Processamento de imagem no servidor (redimensionar, converter) |
| `GET /api/animals/{id}/photo` e `GET /api/animals/{id}/photo/thumbnail` | Limpeza automática de arquivos órfãos no Blob (§8) |
| Campos de foto em `AnimalDto`, `AnimalListItemDto` e `AnimalSyncDto` | Foto no `POST`/`PATCH` do animal (F2) |
| Apagar as fotos na exclusão de conta (Spec #21) | |

**Rotas novas:**

| Rota | Corpo / parâmetros | Resposta |
|---|---|---|
| `PUT /api/animals/{id}/photo` | `multipart/form-data`: `photo` (arquivo), `thumbnail` (arquivo), `photoHash` e `thumbnailHash` (SHA-256 em hexadecimal), `takenAt` (opcional, UTC com `Z`) | `200` `{ photoHash, photoUrl, thumbnailUrl, photoUpdatedAt }` |
| `DELETE /api/animals/{id}/photo` | `?updatedAt=` (opcional, UTC com `Z`) | `204` |
| `GET /api/animals/{id}/photo` | `?v=` (ignorado pelo servidor; só para cache) | `302` para URL SAS da foto |
| `GET /api/animals/{id}/photo/thumbnail` | `?v=` | `302` para URL SAS da miniatura |

**Mudanças nas rotas existentes:** nenhuma mudança de comportamento. `GET /api/animals`, `GET /api/animals/{id}` e `GET /api/animals/changes` ganham os campos de foto (F9).

## 2. Regras de negócio

**Premissa: nenhuma regra de negócio existente é alterada.** Regras novas:

| # | Regra | Rota | Resposta |
|---|---|---|---|
| R1 | O animal precisa existir na propriedade (filtro de tenant do repositório) | todas | `404` |
| R2 | Foto e miniatura obrigatórias; JPEG ou WebP (conferido pelo `Content-Type` **e** pelos bytes iniciais); foto ≤ 2 MB, miniatura ≤ 200 KB | `PUT` | `400` |
| R3 | Animal **inativo** aceita foto (precedente: `PATCH` em inativo é aceito, §5.4 da #14) | `PUT`, `DELETE` | Mantido |
| R4 | Animal sem foto | `GET` | `404` |
| R5 | Animal sem foto | `DELETE` | `204` sem gravar (estado já é o pedido) |
| R6 | `photoHash` e `thumbnailHash` obrigatórios, 64 caracteres hexadecimais; precisam bater com o SHA-256 calculado pelo servidor sobre os arquivos recebidos | `PUT` | `400` |
| R7 | Foto com o **mesmo hash** da atual | `PUT` | `200` com a foto atual, sem gravar e sem LWW (reenvio) |

## 3. Situação atual do código

| # | Situação atual | Tratamento |
|---|---|---|
| 1 | `Animal` é `BaseEntity, ITenantEntity, ISyncable`; o `RowVersion` avança a cada `UPDATE` | Gravar a foto atualiza o animal → aparece no `/changes` existente, sem rota de pull nova (F9) |
| 2 | Não existe armazenamento de arquivos; o container do Azure Container Apps tem disco **efêmero** | Azure Blob Storage (F3) |
| 3 | Storage Account criada em 19/Out/2026 (Central US, Standard, LRS, sem namespace hierárquico, sem acesso anônimo), container `animal-photos` privado; connection string no secret `blob-connection` do Container App, exposta como `ConnectionStrings__BlobStorage` | Usado pela F3 |
| 4 | `AccountRepository.DeletePropertyAsync` apaga fisicamente todos os dados da propriedade | Fotos ficariam no Blob depois da exclusão da conta → F12 |
| 5 | `AnimalListItemDto` não tem campos de foto | Ganha `photoHash` e `thumbnailUrl` (F9) |

## 4. Decisões propostas

As marcadas com ⭐ foram escolhidas pela usuária em 19/Out/2026.

| # | Decisão proposta | Alternativa descartada |
|---|---|---|
| F1 ⭐ | **Rota de foto dentro de animais** (`/api/animals/{id}/photo`); duas colunas em `Animal` | Controller genérico de arquivos (`/api/files` + tabela `StoredFile`) — reuso entre entidades, mas exige `SyncId` próprio do arquivo e validação de dono por tipo; desnecessário para uma foto por animal |
| F2 | **Foto fora do `POST`/`PATCH` do animal.** O cadastro continua JSON leve; a foto é enviada depois, quando o animal já tem `ServerId` (§5.8) | Multipart no `POST` — o cadastro ficaria pesado e falharia mais com sinal fraco, travando a fila |
| F3 | **Azure Blob Storage**, container privado, acessado por `IFileStorageService` (`Application/Interfaces`) com implementação `AzureBlobStorageService` (`Infrastructure/Services`), autenticada pela **connection string** | (a) Disco do container — efêmero no Container Apps, perde as fotos a cada revisão. (b) `varbinary` no Azure SQL — incha o banco e o tier é cobrado por armazenamento. (c) Managed Identity — sem segredo, mas só funciona dentro da Azure e a SAS exige *user delegation key*; fica como melhoria |
| F4 ⭐ | **Miniatura gerada e enviada pelo app**, junto com a foto. O servidor não processa imagem | Servidor gerar a miniatura (ImageSharp) — CPU no servidor fraco e mais uma dependência |
| F5 ⭐ | **Versão da foto pelo hash do conteúdo** (sugestão do Pedro). Modelo: `Animal.PhotoHash` (`string?`, SHA-256 em hexadecimal minúsculo, 64 caracteres) e `Animal.PhotoUpdatedAt` (`DateTime?`). Chaves no Blob derivadas do hash: `{propertyId}/animals/{animalSyncId}/{photoHash}/photo` e `.../thumbnail` — a mesma foto cai sempre no mesmo nome. A miniatura acompanha o hash da foto (mesma captura). Ganhos: (a) o app compara o hash local com o do pull e **só baixa quando muda**; (b) reenvio da mesma foto → `200` sem gravar (R7); (c) dois envios simultâneos da mesma foto gravam no mesmo nome, sem órfãos | (a) `PhotoId` aleatório por envio — o celular que tirou a foto a baixaria de novo no pull, e cada reenvio geraria arquivos novos. (b) Sobrescrever sempre o mesmo nome — o cache do app continuaria mostrando a foto antiga |
| F6 | **LWW próprio da foto:** `takenAt` (momento em que o usuário tirou/escolheu a foto) comparado com `PhotoUpdatedAt` pelo `SyncTimestampResolver` (mesmas regras de fuso, futuro e ausência da §5.3). Só se aplica quando o hash é diferente do atual (R7 vem antes). Mais antigo → não aplica e devolve `200` com a foto atual; empate aplica. **Não altera `Animal.UpdatedAt`** | Usar o `UpdatedAt` do animal — uma foto tirada depois de uma edição offline faria a edição dos campos ser descartada no sync (mesmo motivo da §5.4) |
| F7 | **`DELETE` também com LWW** (`?updatedAt=`): mais antigo que `PhotoUpdatedAt` → `204` sem apagar; senão `PhotoHash = null`, `PhotoUpdatedAt = updatedAt`. **Desvio consciente da §5.4** ("inativação sempre vence"): lá o soft delete permite desfazer; aqui o arquivo é apagado de verdade | Sempre vencer — um `DELETE` antigo, sincronizado depois, apagaria sem volta a foto mais nova tirada em outro celular |
| F8 | **Leitura por redirecionamento:** a API valida o tenant, gera uma URL SAS de leitura válida por **10 minutos** e responde `302`. O app usa sempre a URL estável da API, com `?v={photoHash}` | (a) A API transmitir os bytes — consome banda e CPU do container. (b) SAS no DTO — expiraria dentro do Room e quebraria o cache de imagem |
| F9 | **Campos nos DTOs**, com URLs como caminhos relativos (`/api/animals/42/photo?v=...`), `null` sem foto: `AnimalDto` → `photoHash`, `photoUrl`, `thumbnailUrl`; `AnimalListItemDto` → `photoHash`, `thumbnailUrl`; `AnimalSyncDto` → `photoHash`, `photoUrl`, `thumbnailUrl`, `photoUpdatedAt`. Trocar ou remover a foto avança o `RowVersion` → chega pelo `GET /api/animals/changes` existente | Rota de pull própria das fotos — uma requisição a mais por sincronização |
| F10 | **Validação → `400`** (R2 e formato do R6) num atributo `ImageFileAttribute` em `Application/Helpers` (precedente do `ValidEnumAttribute`), aplicado no `AnimalPhotoUploadDto`; `[RequestSizeLimit]` de 3 MB na rota | Validar no service — misturaria validação de entrada com regra de negócio |
| F11 | **Ordem no `PUT`:** (1) valida a entrada (F10); (2) calcula o SHA-256 dos dois arquivos e confere com o enviado (F15); (3) busca o animal (`404`); (4) hash igual ao atual → `200` (R7); (5) LWW (F6); (6) envia os arquivos ao Blob; (7) grava o animal; (8) apaga os arquivos do hash anterior (melhor esforço, falha só gera log). Uma falha entre 6 e 8 deixa **arquivo órfão** no Blob, nunca um animal apontando para arquivo inexistente | Apagar antes de gravar — uma falha deixaria o animal sem a foto antiga e sem a nova |
| F12 | **Exclusão de conta** (Spec #21): depois de `DeletePropertyAsync`, o `AccountService` apaga o prefixo `{propertyId}/` do Blob | Deixar as fotos — dados da propriedade sobrariam depois da exclusão pedida pelo usuário |
| F13 | **Container separado por ambiente:** `BlobStorage:ContainerName` = `animal-photos` em `appsettings.json` e `animal-photos-dev` em `appsettings.Development.json` (ou Azurite local) | Mesmo container — testes locais gravariam junto com as fotos de produção |
| F14 ⭐ | **Fila de fotos separada** no app (§5) | Mesma fila — um upload lento com sinal fraco seguraria vacinações, pesagens etc. |
| F15 ⭐ | **Hash enviado pelo app e conferido pelo servidor:** o `PUT` exige `photoHash` e `thumbnailHash`; o servidor recalcula o SHA-256 de cada arquivo e responde `400` se não bater. Protege a **integridade**: upload truncado ou corrompido, ou bug do app enviando o arquivo errado, nunca é gravado. **Não** é autenticação — quem tem o arquivo calcula o hash; o acesso continua protegido pelo JWT e pelo filtro de tenant. Campos **obrigatórios** (diferente do `syncId`/`updatedAt`, opcionais para o Swagger): para testar pelo Swagger, o hash sai de `Get-FileHash -Algorithm SHA256 <arquivo>` (PowerShell) ou `sha256sum <arquivo>` | (a) Só o servidor calcular — sem conferência, um arquivo corrompido seria gravado como versão válida. (b) Hash opcional — a conferência deixaria de ser garantida |

## 5. Contrato do app (complemento da Spec #14 §6)

1. **Captura:** `ActivityResultContracts.TakePicture` (câmera) ou `PickVisualMedia` (galeria). Não exige CameraX.
2. **Processamento no celular, antes de salvar:**
   - aplicar a orientação EXIF e **remover os metadados** (inclusive GPS);
   - **foto:** 1080 px no lado maior, JPEG ~75% (~100–200 KB);
   - **miniatura:** 240 px no lado maior, JPEG ~70% (~10–20 KB);
   - calcular o **SHA-256 dos arquivos já processados** (os bytes exatos que serão enviados), em hexadecimal minúsculo. Nunca da foto original da câmera — senão o hash nunca bate com o do servidor.
3. **Armazenamento local:** os dois arquivos no armazenamento interno do app; no Room, uma pendência por animal: `PendingPhoto(AnimalSyncId, Operation = Upload | Delete, PhotoPath, ThumbnailPath, PhotoHash, ThumbnailHash, TakenAt)`. O `PhotoHash` também fica gravado no animal local.
4. **Fila separada:** WorkManager com trabalho único (`photo-sync`, `ExistingWorkPolicy.KEEP`), `NetworkType.CONNECTED`. Só envia se o animal já tiver `ServerId`; senão, `Result.retry()`. A fila principal não espera a de fotos.
5. **Compactação:** foto nova substitui o upload pendente do mesmo animal; remover a foto troca o upload pendente por um `Delete`.
6. **Envio:** `PUT` com `photoHash`, `thumbnailHash` e `takenAt` em UTC com `Z`; `DELETE` com `updatedAt` do momento da remoção. Decisão pelo status (§5.10 da #14); `413` → `Failed` (não deve ocorrer com o item 2). `400` de hash divergente → recalcular o hash dos arquivos locais e reenviar uma vez; se ainda divergir, `Failed` (arquivo local corrompido).
7. **Exibição:** Coil usando o mesmo OkHttp do app (com o `Interceptor` do JWT). O Coil segue o `302`, e o OkHttp **remove o `Authorization`** ao redirecionar para outro host — o Blob recebe só a SAS. A URL com `?v=` serve de chave de cache. Miniatura nas listas, foto no detalhe; enquanto a pendência existir, mostrar o arquivo local.
8. **Pull:** se o `photoHash` recebido for **igual** ao local, nada é baixado (é a própria foto deste celular, ou uma já baixada). Se for diferente, apaga os arquivos locais antigos e baixa **sob demanda** (quando a tela pede), pelas URLs com `?v={photoHash}`. `photoHash = null` → apaga a foto local.

## 6. Fases de implementação e validação

Cada fase é proposta com o código exato e só implementada após aprovação. Branch proposta: `feature/animal-photos`, a partir do `dev`.

| Fase | Status |
|---|---|
| 1 — Armazenamento (F3, F13) | ✅ 21/Out/2026 — pacote `Azure.Storage.Blobs` 12.30.1 (nessa versão a listagem usa `GetBlobsOptions`); container `animal-photos-dev` criado privado. Contra o `animal-photos-dev`: envio e reenvio do mesmo nome (sobrescreve) ok; download pela SAS → `200`, `Content-Type` preservado, bytes idênticos; SAS vencida → `403`; sem SAS → `409` (acesso público desabilitado na conta); apagar → `true`, de novo → `false`; apagar por prefixo → 2 arquivos, nenhum restante |
| 2 — Domínio e banco (F5) | ✅ 21/Out/2026 — migração `20261021230211_Animal_Photo` aplicada no banco local (`PhotoHash nvarchar(64) NULL`, `PhotoUpdatedAt datetime2 NULL`); 158 animais existentes com as duas colunas nulas |
| 3 — Envio com hash e LWW (F5, F6, F10, F11, F15) | ✅ 21/Out/2026 — duas contas novas no banco local, contra o `animal-photos-dev`. Primeiro envio → `200`, foto e miniatura em `.../{photoHash}/` com `image/jpeg`, `PhotoHash` igual ao `sha256sum`, `UpdatedAt` do animal continua `NULL`, `RowVersion` avança; reenvio da mesma foto com `takenAt` mais antigo → `200` sem gravar (`RowVersion` igual); foto diferente com `takenAt` mais antigo → `200` com a atual, Blob intacto; mais nova → substitui e apaga a pasta do hash anterior; empate → aplica; `-03:00` → convertido para UTC; futuro → limitado a "agora"; sem `takenAt` → "agora"; animal inativo → `200`. `400`: hash da foto ou da miniatura divergente, hash ausente, hash fora do formato, sem miniatura, PDF renomeado para `.jpg`, JPEG declarado como `image/png`, foto de 2,5 MB, corpo de 7 MB (o `[RequestSizeLimit]` vira `400` "Request body too large" pelo model binding, não `413` nem `500`). Outra conta e animal inexistente → `404`; nenhuma dessas tentativas alterou o animal. 10 envios simultâneos da mesma foto → 1 × `200` e 9 × `500` (o `ExceptionMiddleware` não registra a exceção; o esperado pela §8 é a concorrência do `RowVersion`), **um único** par de arquivos no Blob; o reenvio seguinte → `200` sem gravar. O `photoUpdatedAt` da resposta sai com `Z` quando vem do `takenAt` recebido e sem `Z` quando vem do banco — mesmo comportamento das outras datas da API (`createdAt` no `/changes`); pelo contrato (§5.3 da #14), data sem fuso é UTC |
| 4 — Remoção com LWW (F7) | ✅ 21/Out/2026 — `updatedAt` mais antigo que a foto → `204`, foto e arquivos mantidos, `RowVersion` igual; outra conta e animal inexistente → `404`; remover sem `updatedAt` → `204`, `PhotoHash = NULL`, `PhotoUpdatedAt` = momento da remoção, pasta do hash apagada no Blob, `UpdatedAt` do animal continua `NULL`; repetido → `204` sem gravar; envio com `takenAt` **anterior** à remoção → `200` com `photoHash: null` (a foto não volta) e nada enviado ao Blob; envio depois da remoção → foto gravada; `updatedAt` no futuro → limitado a "agora" e aplicado |
| 5 — Leitura e DTOs (F8, F9) | ✅ 21/Out/2026 — `GET .../photo` → `302` para `https://<conta>.blob.core.windows.net/animal-photos-dev/{propertyId}/animals/{syncId}/{hash}/photo` com SAS (`sv`, `se`, `sr`, `sp`, `sig`); download pela SAS sem JWT → `200 image/jpeg`, bytes idênticos; `.../photo/thumbnail` seguindo o redirecionamento **com** o JWT (como o OkHttp/Coil) → `200`, miniatura idêntica (o `Authorization` não vai para o Blob); sem JWT → `401`; outra conta, animal sem foto (foto e miniatura) e inexistente → `404`. Detalhe, lista e `/changes` com `photoHash` e URLs `?v={hash}`, `null` sem foto; cadastro novo (`AnimalCreatedDto`) com os campos `null`. Trocar a foto → o animal reaparece no `/changes?since=` com o hash novo; a SAS antiga passa a `404` (arquivo apagado). Suíte de testes 292/292. **Achado (correção proposta junto com a Fase 6):** sem a connection string do Blob, **todas** as rotas de `AnimalsController` respondiam `500` (o DI monta o `BlobServiceClient` ao construir o controller), não só as de foto |
| 6 — Exclusão de conta (F12) | ✅ 21/Out/2026 — conta nova com 2 animais com foto (4 arquivos em `{propertyId}/`); `DELETE /api/auth/me` → `204`, nenhum arquivo restante no prefixo, propriedade e animais removidos do banco; arquivos de outra conta intactos. **Correção do achado da Fase 5 (aprovada):** o `AzureBlobStorageService` passou a criar o cliente do Blob só no primeiro uso (`Lazy`), lendo a connection string do `IConfiguration`; o registro do `BlobServiceClient` saiu do `Program.cs`. Com a connection string vazia: `GET /api/animals` e o detalhe → `200`, `PUT .../photo` → `500`, exclusão de conta → `204` com a propriedade removida e o aviso "Failed to delete stored files of property ..." no log. Suíte 292/292 |
| 7 — Testes | ✅ 21/Out/2026 — `AnimalPhotoServiceTests` (20, novo): hash divergente (foto e miniatura), hash em maiúsculas, animal inexistente, primeiro envio (arquivos, hash, `PhotoUpdatedAt`, `UpdatedAt` intacto), mesmo hash, `takenAt` mais antigo, empate, foto mais nova (grava antes de apagar os arquivos antigos), falha ao apagar, falha no envio (não grava), remoção (inexistente, sem foto, mais antiga, mais nova com a ordem banco → arquivos) e leitura (inexistente, sem foto, foto e miniatura com validade de 10 min). `AccountServiceTests` (4, novo — a classe não existia): sessões invalidadas, arquivos apagados depois do banco, falha no Blob não impede a exclusão, falha no banco não apaga arquivos. Suíte 316/316 |
| 8 — Documentação | ✅ 21/Out/2026 — Spec #14 (v1.12: status, §8, sumário), `catalogo-erros-api.md` (§4.3, foto do animal), `spec-animais.md` (nota no topo), esta spec (v1.0) |

### 6.1 Fase 1 — Armazenamento

- Pacote `Azure.Storage.Blobs`.
- `IFileStorageService`: enviar, apagar, apagar por prefixo, gerar URL de leitura (métodos assíncronos retornando `Task<bool>` quando não há valor).
- `AzureBlobStorageService` + seção `BlobStorage` (`ContainerName`) e `ConnectionStrings:BlobStorage` (user-secrets / secret do Container App).
- Registro no `Program.cs` (⚠️ **requer aprovação**).

| Validação planejada | Esperado |
|---|---|
| Enviar, gerar SAS e baixar um arquivo de teste no `animal-photos-dev` | Download idêntico ao enviado |
| SAS depois da validade | `403` do Blob |
| Apagar por prefixo | Nenhum arquivo restante com o prefixo |

### 6.2 Fase 2 — Domínio e banco

- `Animal`: `PhotoHash` (`string?`, `MaxLength(64)`), `PhotoUpdatedAt` (`DateTime?`).
- Migração `Animal_Photo` (⚠️ **requer aprovação**): duas colunas anuláveis, sem backfill.

| Validação planejada | Esperado |
|---|---|
| Animais existentes | `PhotoHash` e `PhotoUpdatedAt` nulos |

### 6.3 Fase 3 — Envio com hash e LWW

| Validação planejada | Esperado |
|---|---|
| Primeiro envio | `200`; dois arquivos no Blob em `.../{photoHash}/`; `PhotoHash` (igual ao do `Get-FileHash`) e `PhotoUpdatedAt` gravados; `UpdatedAt` do animal **inalterado**; `RowVersion` avança |
| Substituir | `PhotoHash` novo; arquivos do hash anterior apagados |
| Reenvio da mesma foto (mesmo hash), com qualquer `takenAt` | `200` com a foto atual; nada enviado ao Blob; `RowVersion` **não** avança (R7) |
| `photoHash` ou `thumbnailHash` divergente do arquivo | `400`; nada gravado |
| Hash ausente ou com formato inválido | `400` |
| 10 envios simultâneos da mesma foto | Todos `200`, ou `500` com reenvio bem-sucedido; um único conjunto de arquivos no Blob |
| `takenAt` mais antigo que o atual (foto diferente) | `200` com a foto atual; nada enviado ao Blob |
| Foto diferente com o mesmo `takenAt` | Aplica (empate) |
| Sem `takenAt` / `-03:00` / futuro | "Agora" / convertido / limitado a "agora" |
| Animal inativo | `200` (R3) |
| Sem miniatura, PDF renomeado para `.jpg`, foto de 3 MB | `400` / `400` / `400` ou `413` |
| Animal de outra propriedade | `404` |

### 6.4 Fase 4 — Remoção com LWW

| Validação planejada | Esperado |
|---|---|
| Remover | `204`; arquivos apagados; `PhotoHash = null`; `RowVersion` avança |
| Repetido / animal sem foto | `204` sem gravar |
| `updatedAt` mais antigo que a foto atual | `204`; foto mantida |

### 6.5 Fase 5 — Leitura e DTOs

| Validação planejada | Esperado |
|---|---|
| `GET .../photo` e `.../photo/thumbnail` | `302` para SAS; download funciona sem JWT |
| Animal sem foto / de outra propriedade | `404` |
| Detalhe, lista e `/changes` | `photoHash` e URLs com `?v={photoHash}`; `null` sem foto |
| Trocar a foto | Animal reaparece no `/changes` com `photoHash` novo |

### 6.6 Fase 6 — Exclusão de conta

| Validação planejada | Esperado |
|---|---|
| Excluir conta com animais com foto | Nenhum arquivo restante em `{propertyId}/` |
| Falha do Blob na exclusão | Conta excluída mesmo assim; falha registrada em log |

### 6.7 Fase 7 — Testes

`AnimalPhotoServiceTests` (novo; `IAnimalRepository` e `IFileStorageService` mockados) e `AccountServiceTests` (novo — previsto como ajuste, mas a classe não existia).

### 6.8 Fase 8 — Documentação

Spec #14 (§8: linha da foto do animal), `catalogo-erros-api.md`, nota no topo de `spec-animais.md`.

## 7. Arquivos impactados

| Arquivo | Mudança |
|---|---|
| `MuuBoi.csproj` | Pacote `Azure.Storage.Blobs` |
| `Application/Interfaces/IFileStorageService.cs` | **Novo** |
| `Application/Interfaces/IAnimalPhotoService.cs` | **Novo** |
| `Application/Services/AnimalPhotoService.cs` | **Novo** |
| `Application/Options/BlobStorageOptions.cs` | **Novo** |
| `Application/DTOs/AnimalPhotoUploadDto.cs` (arquivos, hashes e `takenAt`), `AnimalPhotoDto.cs` | **Novos** |
| `Application/Helpers/ImageFileAttribute.cs` | **Novo** |
| `Application/Helpers/AnimalPhotoPaths.cs` | **Novo** — URLs da API, chaves no Blob e prefixo da propriedade |
| `Application/DTOs/AnimalDto.cs`, `AnimalListItemDto.cs`, `AnimalSyncDto.cs` | Campos de foto |
| `Application/Mappings/AnimalProfile.cs` | `AnimalPhotoDto` e URLs de foto nos três DTOs |
| `Application/Interfaces/IAnimalRepository.cs`, `Infrastructure/Repositories/AnimalRepository.cs` | `GetAnimalForPhotoAsync` (sem `Include`) e `UpdatePhotoAsync` (só `SaveChanges`, para não marcar o grafo inteiro como modificado) |
| `Application/Services/AccountService.cs` | Apagar o prefixo da propriedade (F12) |
| `Infrastructure/Services/AzureBlobStorageService.cs` | **Novo** — cliente do Blob criado no primeiro uso (`Lazy`) |
| `Domain/Models/Animal.cs` | `PhotoHash`, `PhotoUpdatedAt` |
| `Infrastructure/Migrations/` | ⚠️ `20261021230211_Animal_Photo` |
| `Api/Controllers/AnimalsController.cs` | Quatro rotas novas |
| `Program.cs` | ⚠️ `BlobStorageOptions`, `IFileStorageService` (singleton) e `IAnimalPhotoService` |
| `appsettings.json`, `appsettings.Development.json` | Seção `BlobStorage` |
| `MuuBoi.Tests/Services/AnimalPhotoServiceTests.cs`, `AccountServiceTests.cs` | **Novos** |

## 8. Riscos

| Risco | Consequência | Tratamento |
|---|---|---|
| Falha entre o envio ao Blob e a gravação no banco, ou ao apagar o arquivo antigo | Arquivo órfão no Blob | Aceito: ~200 KB cada, custo irrisório. O reenvio da mesma foto reaproveita o mesmo nome (F5). Limpeza fora do escopo (questão 3) |
| Dois `PUT` simultâneos no mesmo animal | `DbUpdateConcurrencyException` → `500`; o app reenvia (§5.3 da #14). Mesma foto: mesmo nome no Blob, sem órfãos (F5). Fotos **diferentes**: o perdedor pode deixar órfãos | Aceito, como na #14 |
| Relógio do celular atrasado | A foto dele perde no LWW | Risco herdado (D2 da #14) |
| URL SAS vazada | Acesso só àquela foto, por até 10 minutos | Aceito |
| Connection string vazada | Acesso total à Storage Account | Fica só no secret do Container App e no user-secrets; nunca no `appsettings.json` nem no Git. Managed Identity como melhoria (F3) |
| Download do Blob para a internet | Cobrança de saída de dados acima da franquia mensal | Irrelevante no volume do TCC; a miniatura nas listas e a comparação de hash no app reduzem o tráfego |

| Blob Storage sem configuração ou fora do ar | Rotas de foto respondem `500`; o resto da API funciona; a exclusão de conta conclui e registra o aviso | Cliente do Blob criado no primeiro uso (achado da Fase 5) |
| Exceções não registradas no log | O `ExceptionMiddleware` devolve `500` sem registrar a exceção — a causa dos `500` concorrentes da Fase 3 não pôde ser confirmada | Fora do escopo; melhoria sugerida: registrar a exceção no middleware |

## 9. Questões em aberto

Fechadas em 21/Out/2026 com as propostas:

1. ✅ Limites de 2 MB (foto) e 200 KB (miniatura).
2. ✅ SAS válida por 10 minutos.
3. ✅ Limpeza de órfãos fica como trabalho futuro.
4. ✅ Miniaturas baixadas só sob demanda.
