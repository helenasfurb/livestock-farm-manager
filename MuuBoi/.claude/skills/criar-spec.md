# Criar Spec de Funcionalidade

Você é um analista de sistemas criando um arquivo de especificação para o projeto MuuBoi (ASP.NET Core 8, livestock farm manager). Siga rigorosamente o processo abaixo.

## Processo obrigatório

### 1. Leitura e contexto
- Leia o arquivo da ata de reunião relevante em `C:\Users\helen\OneDrive\Documentos\TCC\Aplicação\`.
- Leia o spec anterior mais relacionado em `MuuBoi\Docs\Specs\` para entender dependências.
- Leia `MuuBoi\CLAUDE.md` para relembrar convenções do projeto.
- Identifique os campos, entidades e regras que a ata menciona para o módulo em questão.

### 2. Tirar TODAS as dúvidas antes de escrever
Antes de criar qualquer arquivo, use `AskUserQuestion` para perguntar tudo que for ambíguo ou não explícito na ata. Nunca assuma decisões de modelagem sem confirmar. Exemplos do que perguntar:
- Modelagem de entidades (campos, tipos, obrigatoriedade)
- Relacionamentos e dependências entre módulos
- Comportamento de campos em diferentes fluxos
- Decisões de UX que impactam a API (ex: filtros, paginação)
- O que acontece em casos-limite (exclusão, reativação, duplicatas)

### 3. Estrutura do arquivo de spec
Crie o arquivo em `MuuBoi\Docs\Specs\spec-<nome-kebab-case>.md` com **exatamente** estas seções:

```
# Spec: <Nome do Módulo>

**Módulo:** <Nome>
**Versão:** 1.0
**Data:** <data atual>
**Fonte:** <ata ou origem>
**Status:** Aprovado para implementação

## 1. Contexto e Objetivo
## 2. Decisões Registradas       ← tabela com #, Decisão, Motivo
## 3. Histórias de Usuário        ← US-XX com critérios de aceite
## 4. Casos de Uso                ← CU-XX com fluxo principal e alternativos
## 5. Especificação Técnica de Modelagem
   ### 5.1 Entidade(s)            ← tabela de campos + bloco de código C#
   ### 5.2 Enums                  ← membros em inglês, [Description] em português
   ### 5.3 DTOs                   ← CreateDto, UpdateDto, ResponseDto, ListItemDto, FilterDto
   ### 5.4 Endpoints da API       ← tabela Método | Rota | Descrição | Retorno
   ### 5.5 Regras de Negócio      ← tabela RN-XX | Regra | Onde aplicar
   ### 5.6 Camadas impactadas     ← tabela Camada | Arquivo | Ação
## 6. Notas de Migração           ← lista numerada, sempre com aviso de aprovação prévia
## 7. Fora do Escopo deste Spec   ← referências aos outros specs
```

### 4. Convenções obrigatórias

**Enums:**
- Membros em inglês (ex: `BornOnFarm`, `Purchased`)
- `[Description("...")]` em português (ex: `[Description("Nascido na Propriedade")]`)
- Localização: `Domain/Enums/`

**Namespaces** (conforme CLAUDE.md):
- Controllers → `MuuBoi.Api.Controllers`
- Services → `MuuBoi.Application.Services`
- DTOs → `MuuBoi.Application.DTOs`
- Models → `MuuBoi.Domain.Models`
- Repositories → `MuuBoi.Infrastructure.Repositories`

**HTTP:**
- POST → `201 Created` com `CreatedAtAction`
- PATCH → `200 OK` com recurso atualizado
- DELETE → `204 No Content` (sempre soft delete salvo exceção explícita)
- Não encontrado → `404 Not Found` (nunca `null`)

**Exceções de domínio (service layer):**

| Exceção | HTTP | Quando usar |
|---------|------|-------------|
| `NotFoundException` | `404` | Entidade não existe |
| `ConflictException` | `409` | Conflito de estado — campo único duplicado, entidade já no estado alvo |
| `BusinessRuleException` | `422` | Violação de regra de negócio que exige consulta ao banco |

Todas em `Domain/Exceptions/`. Nunca retornar `null` para sinalizar erro — sempre lançar. Na coluna "Onde aplicar" das RNs, especificar qual exceção é lançada (ex: `Service → lança ConflictException`).

**Entidades:**
- Herdam de `BaseEntity` (Id, IsActive, CreatedAt, UpdatedAt)
- Com escopo de tenant implementam `ITenantEntity` (PropertyId)
- Soft delete preferido: `IsActive = false`

**DTOs por operação:**
- `<Entity>CreateDto` — input POST
- `<Entity>UpdateDto` — input PATCH (todos os campos opcionais)
- `<Entity>Dto` — output detalhe
- `<Entity>ListItemDto` — output listagem (mais leve)
- `<Entity>FilterDto` — query params

**Filtro de status ativo:**
- Usar `bool? IsActive` no FilterDto: `true` = só ativos, `false` = só inativos, `null` = todos

**Migrations:**
- Nunca criar ou executar sem aprovação explícita da usuária
- Sempre listar as mudanças esperadas em §6

### 5. O que NÃO fazer
- Não implementar código real — apenas escrever o spec
- Não criar migrations
- Não assumir decisões sem perguntar
- Não omitir seções da estrutura
- Não usar nomes de membros de enum em português
