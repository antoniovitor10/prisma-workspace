# Operação e integrações

Andar Maestri: `f032bd6c-cc3c-4bba-9e62-9f6c3ec03567`. Atualizado em 2026-10-05.

## Operação — objetivo e ambientes

Objetivo: manter configuração, diagnóstico, dados e publicação rastreáveis. Componentes: API/SQL Server/SMTP, Docker/scripts e integrações documentadas do Prisma. Ambientes documentados: produção prisma.nordevs.com.br; desenvolvimento npm run dev; Docker localhost:8080; Context Explorer localhost:5174/API3847. Última evidência documental de deploy em 2026-09-22; não tratar documentação antiga como verificação atual do servidor. Credenciais são configuradas em .env, user-secrets e variáveis do ambiente; nenhum valor é registrado aqui.

## Operação — acessos e próximo resultado

Próximo resultado: conferir acesso a SQL Server, SMTP PortalEmail, GitHub e hospedagem, mapa de configuração e procedimento de backup/restauração. Registrar falhas e modo de continuar sem expor segredos. Infra cuida de publicação e diagnóstico; Dados/integrações de consistência/sincronização; Segurança de autenticação, permissões e exposição. Publicação exige sua autorização específica e gates aplicáveis. Recuperação antiga privada em .local/recovery/20261005; validar configurações antes de reutilizar. ai-memory ativo em 127.0.0.1:49374, escopo default/prisma-workspace. Nenhuma mensagem externa enviada.

As notas estão no canvas deste andar. Nenhum terminal temporário ou agente de IA permanece aqui.
