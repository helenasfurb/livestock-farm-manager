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