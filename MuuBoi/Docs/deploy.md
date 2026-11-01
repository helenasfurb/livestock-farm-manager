## Passo a passo
 
### 1. Sincronizar com a main
 
```bash
git checkout main
git pull
git status
```

### 1.1. Aplicar as migrações pendentes no Azure SQL

Em produção o app **não** aplica migrações ao subir (`Database__MigrateOnStartup=false`, ver abaixo). Se o deploy trouxer migração nova, aplique-a **antes** de publicar a imagem — o código novo pode depender da coluna/tabela nova.

1. Libere o seu IP no firewall do servidor: portal Azure → SQL Server `muuboi` → **Rede** → **Adicionar o endereço IPv4 do cliente** → Salvar.
2. Pegue a connection string: portal Azure → banco `db-muu-boi` → **Cadeias de conexão** → aba ADO.NET (substitua `{your_password}` pela senha do usuário do banco).
3. Na pasta `MuuBoi/`, veja o que está pendente e aplique:

```bash
dotnet ef migrations list --connection "<CONNECTION-STRING-DO-AZURE-SQL>"
dotnet ef database update --connection "<CONNECTION-STRING-DO-AZURE-SQL>"
```

As migrações marcadas com `(Pending)` na primeira lista são as que serão aplicadas. Se não houver nenhuma, pule este passo.

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
- `ConnectionStrings__BlobStorage` → secret `blob-connection` (cadeia de conexão da Storage Account das fotos dos animais)
- `BlobStorage__ContainerName` → *opcional*; só se o container de produção for diferente do definido no `appsettings.json`
- **E-mail (Spec #23 — recuperação de senha e confirmação de e-mail):**
  - `Email__FromEmail` e `Email__Username` → conta Gmail que envia os códigos (`contatomuuboi@gmail.com`)
  - `Email__Password` → secret com a **senha de app** do Gmail (16 letras, sem espaços), não a senha normal da conta. Se a senha normal da conta for trocada, o Google apaga as senhas de app: gere outra em `myaccount.google.com/apppasswords` e atualize o secret
  - `Email__SmtpHost`, `Email__SmtpPort` e `Email__FromName` → *opcionais*; já vêm do `appsettings.json` (`smtp.gmail.com`, `587`, `MuuBoi`)
  - Sem essas variáveis o app sobe normalmente, mas nenhum código é enviado (o erro aparece no **Log stream** como `Failed to send email`) — e, com a confirmação de e-mail, **contas novas não conseguem entrar**
- `Database__MigrateOnStartup` → `false`. Impede que o app rode `Database.Migrate()` e o `DatabaseSeeder` a cada vez que sobe. O Container App escala até zero e sobe de novo a qualquer acesso; com a migração na inicialização, cada subida acordava o Azure SQL serverless, que fica ligado (e cobrando) por no mínimo o tempo de auto-pause. Isso esgotou a cota gratuita mensal em out/2026. Para configurar (uma vez, depois de publicar a versão do app que lê essa variável):

  ```bash
  az containerapp update --name muuboi-api --resource-group muu-boi --set-env-vars Database__MigrateOnStartup=false
  ```

  Localmente e no `docker-compose` a variável não é definida, então lá as migrações e o seed continuam rodando ao subir.
> ⚠️ **Nunca coloque valores reais de senha ou chave neste arquivo.** Ele pode ser versionado no Git. As credenciais já estão salvas diretamente no Container App e não precisam ser reenviadas a cada deploy.

> **Migrações em produção são manuais:** com `Database__MigrateOnStartup=false`, o app não aplica migrações nem roda o seed ao subir — use o passo 1.1. O seed não faz falta em produção: o usuário e a propriedade "Sistema" já existem no banco, e o catálogo de vacinas de cada propriedade nova é criado no próprio cadastro.
 
## Sobre a automação via GitHub Actions
 
O deploy automático (push na `main` → build → push da imagem → atualização do Container App) **não está ativo** no momento: a conta de estudante não tem permissão pra criar o Service Principal necessário (erro "Insufficient privileges to complete the operation" no Entra ID da instituição). O workflow do GitHub Actions roda só build + testes a cada push/PR; o deploy em si é manual, seguindo os passos acima.