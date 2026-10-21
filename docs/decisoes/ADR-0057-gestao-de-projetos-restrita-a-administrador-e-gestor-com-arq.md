---
id: ADR-0057
titulo: "Gestão de projetos restrita a Administrador e Gestor com arquivamento lógico"
status: aceita
data: 2026-08-24
origem: "DECISIONS.md D57"
substitui: ""
---

# ADR-0057 · Gestão de projetos restrita a Administrador e Gestor com arquivamento lógico

## Decisão

- **D57** — Criar, editar, arquivar e restaurar projetos é capacidade-base de **Administrador e Gestor** conforme
  D55; Membro, Visualizador e Externo não a possuem por padrão. Um projeto pode vincular várias equipes e vários
  membros individuais. A criação exige somente nome e descrição; a chave técnica é automática e oculta conforme
  D56. Vincular uma equipe concede automaticamente perfil-base Membro no projeto a todos os seus integrantes;
  ao sair da equipe, a pessoa perde esse acesso herdado, exceto quando ainda possuir vínculo individual ou acesso
  por outra equipe. Projeto possui somente os estados `Ativo` e `Arquivado`, pode nascer sem quadro e não cria
  quadro automático, exclusivo ou padrão; o quadro transversal independente é escolhido depois conforme D52.
  A relação Projeto↔Quadro é exclusivamente derivada pelas tarefas presentes: criar ou mover uma tarefa para um
  quadro exige acesso ao projeto e ao quadro, faz o projeto aparecer nos filtros desse quadro e, quando sua última
  tarefa sair, a relação visual desaparece. Não existe vínculo manual persistente e o quadro não é dono do projeto.
  Arquivar projeto é operação lógica, transacional e recuperável: oculta o projeto e todas as suas tarefas das
  telas normais, sem apagar dados, horas, anexos, sprints ou histórico e sem arquivar os quadros independentes nos
  quais elas estavam. A restauração por Administrador ou Gestor reativa somente recursos arquivados pela mesma
  operação e preserva os que já estavam arquivados. Projeto não possui lixeira nem exclusão permanente; seu ciclo
  de retirada e retorno é exclusivamente arquivar/restaurar. (2026-08-24)
