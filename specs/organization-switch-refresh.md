# SPEC-ORGANIZATION-SWITCH-REFRESH: Atualização imediata ao trocar organização

**Status:** superseded

## Superseded By

Esta especificação foi incorporada integralmente por `SPEC-ORGANIZATIONS` em `specs/organizations.md`.

## Historical Scope

Registrava a correção que atualizava os dados do tenant após a seleção de outra organização. A hipótese histórica de usuário comum com múltiplas memberships foi substituída pela D58: hoje essa troca pertence exclusivamente ao Administrador da plataforma.

Cancelamento de respostas pendentes, descarte do contexto anterior e envio correto de `X-Organization-Id` permanecem obrigatórios para a troca administrativa, agora coberta pelo contrato de organização completo. A implementação histórica continua rastreada pela `TASK-019`; associação única, seletor exclusivo do Platform Admin, ciclo de vida e isolamento transversal são rastreados pela `TASK-034`.
