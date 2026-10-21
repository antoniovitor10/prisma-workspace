---
id: ADR-0058
titulo: "Isolamento por organização, membership única e Administrador da plataforma"
status: aceita
data: 2026-08-24
origem: "DECISIONS.md D58"
substitui: ""
---

# ADR-0058 · Isolamento por organização, membership única e Administrador da plataforma

## Decisão

- **D58** — `Organization` é o tenant raiz equivalente à conta/empresa: dentro dela ficam usuários, equipes,
  projetos, quadros, tarefas, configurações e relatórios, sempre sob isolamento estrito. Usuário comum pertence
  a exatamente uma organização: o mesmo login/e-mail não pode possuir memberships em tenants diferentes e não
  vê seletor de organizações. **Administrador da plataforma** é uma autoridade de control plane externa aos
  perfis da D55, pode alternar tenants pelo seletor superior e é o único que pode criar, editar, arquivar e
  restaurar organizações; essa autoridade não pode ser concedida por perfil ou permissão do tenant. Na troca
  administrativa, contexto, cache e respostas pendentes do tenant anterior são descartados. Organização
  arquivada preserva todos os dados, fica indisponível aos membros e opera somente em leitura para Administradores
  da plataforma até ser restaurada. Não existe exclusão física de organização. A D58 especializa D17/D55,
  substitui a hipótese anterior de múltiplas memberships comuns e incorpora a antiga
  `SPEC-ORGANIZATION-SWITCH-REFRESH` à `SPEC-ORGANIZATIONS`. (2026-08-24)
