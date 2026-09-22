# Rodar local com atualização automática

Instale Git, Node.js 20+ (com npm) e Docker Desktop com containers Linux, ou Docker Engine com Compose v2.
Abra o Docker antes de começar. Reserve pelo menos 4 GB livres para os containers.
O banco atual é SQL Server Developer e requer host x86-64/AMD64; não é necessário instalá-lo no computador.
Este banco é exclusivo para desenvolvimento e testes. Ao iniciar, você aceita a
[EULA da Microsoft](https://go.microsoft.com/fwlink/?linkid=857698).

```bash
git clone https://github.com/antoniovitor10/prisma-workspace.git
cd prisma-workspace
npm run dev
```

Não precisa de `npm install` na raiz nem de SDK .NET no host. O comando gera `.env.dev` uma vez,
sobe o banco, executa a API com `dotnet watch` e inicia o React com Vite.
As dependências são instaladas nos containers. Aguarde a primeira compilação nos logs.

Abra **http://localhost:5450/setup**, consulte `PRISMA_SETUP_TOKEN` no arquivo local `.env.dev`
e crie sua organização e sua conta. Depois entre em **http://localhost:5450**.
O banco nasce vazio; não existem senhas padrão nem dados de produção. O setup fecha permanentemente após o primeiro uso.

Deixe o terminal rodando e edite no seu editor:

- React/CSS: salve e veja a atualização no navegador pelo Vite.
- C#: salve e aguarde o `dotnet watch` atualizar/reiniciar a API.
- Dependências npm: reinicie o ambiente para executar `npm ci` novamente.
- Migration nova: reinicie a API para aplicá-la ao banco local.

Erros de compilação aparecem no terminal e precisam ser corrigidos antes da atualização.
Não é necessário rodar testes para instalar ou iniciar este ambiente.

Para parar, pressione Ctrl+C ou execute `npm run dev:stop` em outro terminal.
Para voltar, use `npm run dev`. Os volumes preservam os dados.

## Se algo não abrir

- Confira se Docker está iniciado e se as portas 5400 e 5450 estão livres.
- A API responde em http://localhost:5400/health; Swagger em http://localhost:5400/swagger.
- Logs: `docker compose --env-file .env.dev -f compose.dev.yaml logs --tail=80`.
- Não apague `.env.dev` enquanto usar o mesmo banco: a senha gerada deve continuar igual à do volume.
- Não use `down --volumes` se quiser preservar dados.
- Não envie `.env.dev` ao Git nem compartilhe seus segredos.

O ambiente de desenvolvimento tem volumes e nome Compose próprios e não usa a infraestrutura de produção.
Não execute o Compose convencional e este ambiente simultaneamente a partir da mesma configuração esperando compartilhar dados.
