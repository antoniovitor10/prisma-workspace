---
id: ADR-0105
titulo: "Risco aceito: hotfix do Prisma segue pela skill hotfix-producao sem rastreador quando o próprio Prisma cai"
status: aceita
data: 2026-10-21
origem: "onboarding"
substitui: ""
---

# ADR-0105 · Risco aceito: hotfix do Prisma segue pela skill hotfix-producao sem rastreador quando o próprio Prisma cai

## Contexto

Com a adoção do método 0.5.1 configurado com `rastreador: prisma` (`ADR-0103`), o próprio Prisma WorkSpace em produção (`https://prisma.nordevs.com.br`) passa a ser o rastreador oficial das Histórias de Usuário e tasks do projeto. Isso cria uma dependência circular em cenários de indisponibilidade severa: se o próprio Prisma em produção cair, o rastreador não estará acessível para abrir ou atualizar a task de correção antes do início do reparo.

## Decisão

Aceitar formalmente o risco de dependência circular e definir o protocolo de contorno para incidentes de indisponibilidade do próprio Prisma:

1. Quando o Prisma em produção estiver indisponível ou degradado a ponto de impedir o registro prévio da task no rastreador, o reparo emergencial segue diretamente o rito da skill `metodo/nucleo/skills/hotfix-producao/SKILL.md` apoiando-se apenas nos artefatos Git (`docs/trabalho/`, branch e PR) durante o incidente.
2. Assim que o serviço do Prisma for restabelecido e validado em produção, a ocorrência, a HU/task correspondente e o vínculo com o commit/PR do hotfix são registrados retroativamente no rastreador Prisma, restabelecendo a paridade de auditoria.

## Alternativas descartadas

- **Usar outro rastreador externo (`git` puro ou `github-projects`) apenas para evitar a dependência circular:** descartada porque o objetivo estratégico é operar o método sobre o próprio Prisma WorkSpace (dogfooding real da integração Git e do quadro do método).
- **Bloquear qualquer hotfix até que o rastreador volte ao ar:** inviável, pois o próprio hotfix pode ser a ação necessária para restaurar o rastreador.

## Consequências

### Positivas

- Permite resposta imediata a incidentes críticos do Prisma sem violar a governança do método, mantendo o rito técnico de `hotfix-producao` (isolamento de causa, teste de regressão, revisão e evidência).
- Preserva a rastreabilidade completa no Prisma por meio do registro retroativo obrigatório após a recuperação.

### Negativas / custos

- Durante a janela do incidente, o estado da task existe apenas no Git e no relatório do PR até que a sincronização posterior seja concluída.

## Como reverter

Alterar `rastreador` em `metodo/projeto/config.yaml` para `git` ou `github-projects` caso se deseje desacoplar o rastreamento operacional da instância em produção do Prisma.
