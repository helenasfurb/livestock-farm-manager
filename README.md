# MuuBoi — Livestock Farm Manager

API REST para gerenciamento de rebanho em propriedades rurais. Desenvolvida como TCC/MVP da matéria de Projeto de Software II.

---

## Funcionalidades

### Autenticação e Usuários
- Registro e login com JWT
- Gerenciamento de usuários por propriedade
- Cada usuário pertence a uma propriedade (tenant)

### Animais
- Cadastro, edição, listagem e remoção de animais
- Informações: nome, sexo, data de nascimento, número de brinco, raça
- Controle de gestação (status de prenhez e data prevista de parto)

### Raças
- Cadastro e gerenciamento de raças

### Registros de Peso
- Histórico de pesagens por animal
- Exibe apenas o último registro nas listagens de animais

### Vacinas e Vacinações
- Cadastro de vacinas disponíveis
- Registro de vacinações por animal (data de aplicação, próxima dose, observações)

### Medicamentos e Medicações
- Cadastro de medicamentos
- Registro de tratamentos por animal (data de início/fim, dose, observações)

### Dashboard
- Cards: total de animais, animais gestantes, tratamentos ativos
- Distribuição por sexo
- Distribuição por raça
- Vacinações por mês
- Previsão de partos

---

## Arquitetura

O projeto segue uma arquitetura em camadas dentro de um único projeto ASP.NET Core:

```
MuuBoi/
├── Api/
│   ├── Controllers/       # Endpoints REST
│   └── Middleware/        # Logging de requisições e tratamento de exceções
├── Application/
│   ├── DTOs/              # Objetos de transferência de dados
│   ├── Interfaces/        # Contratos de serviços e repositórios
│   ├── Mappings/          # Perfis do AutoMapper
│   └── Services/          # Lógica de negócio
├── Domain/
│   ├── Models/            # Entidades de domínio
│   └── Exceptions/        # Exceções customizadas
└── Infrastructure/
    ├── Data/              # DbContext e Seeder
    ├── Migrations/        # Migrations do EF Core
    ├── Repositories/      # Implementações dos repositórios
    └── Services/          # Serviços de infraestrutura (tenant, usuário atual)
```

### Multi-tenancy
Cada propriedade rural é um tenant isolado. O `TenantProvider` extrai o `PropertyId` do token JWT e o injeta automaticamente em todas as queries, garantindo isolamento total dos dados entre propriedades.

---

## Stack

| Camada | Tecnologia |
|---|---|
| Runtime | .NET 8 |
| Framework | ASP.NET Core Web API |
| ORM | Entity Framework Core 8 |
| Banco de dados | SQL Server 2022 |
| Autenticação | ASP.NET Core Identity + JWT Bearer |
| Mapeamento | AutoMapper |
| Documentação | Swagger / Swashbuckle |
| Container | Docker + Docker Compose |

---

## Como executar

### Com Docker (recomendado)

```bash
docker-compose up --build
```

A API ficará disponível em `http://localhost:8080` e o Swagger em `http://localhost:8080/swagger`.

### Localmente

1. Configure a connection string no `appsettings.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=MuuBoiDb;..."
  },
  "Jwt": {
    "Key": "sua_chave_secreta_minimo_32_caracteres"
  }
}
```

2. Execute as migrations e suba a API:
```bash
dotnet ef database update
dotnet run
```

---

## Endpoints principais

| Método | Rota | Descrição |
|---|---|---|
| POST | `/api/auth/register` | Registrar usuário |
| POST | `/api/auth/login` | Login e obtenção do token |
| GET | `/api/animals` | Listar animais da propriedade |
| POST | `/api/animals` | Cadastrar animal |
| PATCH | `/api/animals/{id}` | Atualizar animal |
| DELETE | `/api/animals/{id}` | Remover animal |
| GET | `/api/breeds` | Listar raças |
| GET | `/api/weight-records` | Registros de peso |
| GET | `/api/vaccines` | Listar vacinas |
| GET | `/api/animal-vaccinations` | Vacinações por animal |
| GET | `/api/medications` | Listar medicamentos |
| GET | `/api/animal-medications` | Tratamentos por animal |
| GET | `/api/dashboard` | Dados do dashboard |

> Todos os endpoints (exceto autenticação) requerem o header `Authorization: Bearer {token}`.
