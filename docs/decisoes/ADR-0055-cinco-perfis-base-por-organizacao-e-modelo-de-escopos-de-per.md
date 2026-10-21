---
id: ADR-0055
titulo: "Cinco perfis-base por organização e modelo de escopos de permissão"
status: aceita
data: 2026-08-24
origem: "DECISIONS.md D55"
substitui: "ADR-0012"
---

# ADR-0055 · Cinco perfis-base por organização e modelo de escopos de permissão

## Decisão

- **D55** — A autorização funcional passa a ter cinco perfis-base por organização: **Administrador, Gestor,
  Membro, Visualizador e Externo**. Administrador possui autoridade funcional total no tenant; Gestor administra
  a operação, projetos, quadros/colunas, equipes, sprints, relatórios, custos e aprovadores nos escopos
  autorizados, mas não controla autoridade máxima nem segredos; Membro executa o trabalho autorizado;
  Visualizador somente lê; Externo usa exclusivamente portal/protocolo/token. Administradores e gestores podem
  criar perfis personalizados reutilizáveis por conjunto de permissões, sem conceder autoridade que não possuem.
  `ScrumMaster`, `ProductOwner`, `Developer` e `ProjectManager` deixam de conceder acesso como perfis-base e
  podem existir apenas como função/cargo. Não haverá hierarquia fixa duplicada por projeto: permissões usam os
  escopos organização, equipe, projeto, **quadro**, tarefa, relatório, formulário e solicitação. Acesso ao quadro
  deriva para os projetos representados no mesmo nível conforme D52. A resolução valida tenant e limites
  estruturais, aplica qualquer deny explícito antes de allow explícito, perfis personalizados e perfil-base, e
  usa default deny. Esta decisão sucede D12 e especializa D18, preservando seu modelo híbrido, menor privilégio e
  prevalência de negação explícita. (2026-08-24)
