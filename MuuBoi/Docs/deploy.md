## Passo a passo
 
### 1. Sincronizar com a main
 
```bash
git checkout main
git pull
git status
```

### 2. Buildar a imagem Docker
 
```bash
docker build -t ghcr.io/helenasfurb/muuboi:latest .
```

### 3. Publicar a imagem no GitHub Container Registry
 
```bash
docker push ghcr.io/helenasfurb/muuboi:latest
```

### 4. Atualizar o Container App na Azure
 
```bash
az containerapp update --name <NOME-DO-CONTAINER-APP> --resource-group muu-boi --image ghcr.io/helenasfurb/muuboi:latest
```
 
Não lembra o nome exato do Container App? Rode:
 
```bash
az containerapp list --resource-group muu-boi --query "[].name" -o tsv
```

### 5. Confirmar que subiu
 
- Acesse a URL pública do Container App (campo "URL do aplicativo" na Visão Geral do portal) e teste um endpoint conhecido, ou `/swagger`.
- Pra ver logs em tempo real: portal Azure → Container App → **Log stream**.
- Se a revisão não subir, confira em **Revisions** se o status é "Running" ou se travou em "Failed"/"Provisioning".

## Variáveis de ambiente (já configuradas no Container App — não precisa repetir a cada deploy)
 
- `ConnectionStrings__DefaultConnection` → aponta para o Azure SQL (`muuboi.database.windows.net` / `db-muu-boi`)
- `Jwt__Key` → chave de assinatura JWT do ambiente cloud
> ⚠️ **Nunca coloque valores reais de senha ou chave neste arquivo.** Ele pode ser versionado no Git. As credenciais já estão salvas diretamente no Container App e não precisam ser reenviadas a cada deploy.
 
## Sobre a automação via GitHub Actions
 
O deploy automático (push na `main` → build → push da imagem → atualização do Container App) **não está ativo** no momento: a conta de estudante não tem permissão pra criar o Service Principal necessário (erro "Insufficient privileges to complete the operation" no Entra ID da instituição). O workflow do GitHub Actions roda só build + testes a cada push/PR; o deploy em si é manual, seguindo os passos acima.
## Portal administrativo

Painel do operador da plataforma em `https://<URL do Container App>/portal/`. Mostra totais de propriedades, usuários, animais e acessos, e uma lista por propriedade. Não tem relação com o papel Admin da propriedade: é uma conta única, configurada só por variáveis de ambiente.

| Variável | Origem | Conteúdo |
|---|---|---|
| `Portal__Username` | valor direto | usuário do operador |
| `Portal__PasswordHash` | secret `portal-password-hash` | hash da senha |
| `Portal__JwtKey` | secret `portal-jwt-key` | chave aleatória de assinatura do token do portal |

Sem as três variáveis a API sobe normalmente, mas o login do portal sempre responde 401.

> ⚠️ Use senha e chave diferentes das de desenvolvimento. Nunca coloque os valores neste arquivo.

### Criar a conta do portal na Azure (uma vez, depois do deploy)

#### 1. Gerar o hash da senha e a chave

No seu computador, na raiz do repositório:

```powershell
dotnet run --project tools/PortalPasswordHash -- 'SUA-SENHA'
```

Use aspas **simples**: com aspas duplas, um `$` na senha vira variável do PowerShell e o hash sai errado. O comando imprime duas linhas:

```
PasswordHash: AQAAAAIAAYag...
JwtKey:       7Jx0...
```

Guarde os dois valores só até o passo 3. A `JwtKey` muda a cada execução, o que não é problema: use a da mesma execução do hash.

#### 2. Criar os secrets

Portal do Azure → grupo de recursos `muu-boi` → Container App → **Settings → Secrets → + Add**:

| Key | Type | Value |
|---|---|---|
| `portal-password-hash` | Container Apps Secret | valor de `PasswordHash` |
| `portal-jwt-key` | Container Apps Secret | valor de `JwtKey` |

Clique em **Add** para cada um.

#### 3. Criar as variáveis de ambiente

**Application → Containers → Edit and deploy** → selecione o container → **Edit** → aba **Environment variables** → **+ Add**:

| Name | Source | Value |
|---|---|---|
| `Portal__Username` | Manual entry | o usuário que você vai digitar no login |
| `Portal__PasswordHash` | Reference a secret | `portal-password-hash` |
| `Portal__JwtKey` | Reference a secret | `portal-jwt-key` |

Confira os **dois** underscores em cada nome. Clique em **Save** e depois em **Create**: isso cria uma revisão nova, com as variáveis.

<details>
<summary>Alternativa: passos 2 e 3 pelo Azure CLI</summary>

```powershell
$app = az containerapp list --resource-group muu-boi --query "[0].name" -o tsv

az containerapp secret set --name $app --resource-group muu-boi `
  --secrets "portal-password-hash=<HASH>" "portal-jwt-key=<CHAVE>"

az containerapp update --name $app --resource-group muu-boi `
  --set-env-vars "Portal__Username=<USUARIO>" "Portal__PasswordHash=secretref:portal-password-hash" "Portal__JwtKey=secretref:portal-jwt-key"
```

Os valores ficam no histórico do terminal. Se preferir evitar isso, use o portal do Azure.
</details>

#### 4. Conferir a revisão

**Application → Revisions and replicas**: a revisão mais nova deve estar **Running** e com 100% do tráfego. Se ficar em **Failed**, veja o **Log stream**.

#### 5. Entrar no portal

Abra `https://<URL do Container App>/portal/` e entre com o usuário do passo 3 e a senha em texto puro (não o hash).

| Resposta | Causa provável |
|---|---|
| "Usuário ou senha inválidos" com a senha certa | revisão antiga ainda ativa, nome de variável com um underscore só, ou hash gerado com aspas duplas e `$` na senha |
| "Muitas tentativas" | 5 tentativas em 15 min a partir do mesmo IP; aguarde ou reinicie a revisão |
| Página não encontrada | a imagem publicada ainda não tem o portal; refaça o deploy a partir da `main` |

### Trocar a senha

Gere um hash novo (passo 1), atualize o valor do secret `portal-password-hash` em **Settings → Secrets** e reinicie a revisão ativa (**Revisions and replicas → Restart**). Secret alterado só vale depois do reinício.

### Ambiente local

```powershell
dotnet user-secrets set "Portal:Username" "<usuario>" --project MuuBoi
dotnet user-secrets set "Portal:PasswordHash" "<hash>" --project MuuBoi
dotnet user-secrets set "Portal:JwtKey" "<chave>" --project MuuBoi
```

Reinicie a API e abra `http://localhost:5290/portal/`.

### Cuidados

- **Réplicas:** o limite de 5 tentativas de login a cada 15 min fica na memória de cada réplica. Mantenha o Container App com 1 réplica, ou o limite se multiplica pelo número de réplicas.
- **Contagem de uso:** logins, envios (push) e consultas (pull) ficam em memória e são gravados em `UsageDaily` a cada 1 min. Um reinício abrupto pode perder até 1 min de contagem.
- **Retenção:** `LoginEvents` com mais de 12 meses são apagados automaticamente, uma vez por dia.
- **Exclusão manual de um membro** (pedido por e-mail, seção 2.2 de `delete-account.html`): apague os logins dele antes do usuário, ou a FK impede a exclusão:

  ```sql
  DELETE FROM LoginEvents WHERE UserId = '<id do usuário>';
  DELETE FROM AspNetUsers WHERE Id = '<id do usuário>';
  ```
