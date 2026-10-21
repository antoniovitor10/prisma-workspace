---
id: ADR-0091
titulo: "Assistente de IA no Prisma WorkSpace com modelo trazido pelo instalador"
status: aceita
data: 2026-09-24
origem: "DECISIONS.md D91"
substitui: ""
---

# ADR-0091 · Assistente de IA no Prisma WorkSpace com modelo trazido pelo instalador

## Decisão

- **D91** — Assistente de IA no Prisma, no estilo Notion AI / ClickUp Brain. O PO decidiu em 2026-09-24,
  em sessão de fechamento de escopo, e **sucede parcialmente a D37** somente quanto a "IA fora do MVP";
  finanças, chat/wiki e os demais itens excluídos pela D37 continuam fora. Contrato:
  1. **Modelo trazido por quem instala.** O Prisma é open source e não embute nem paga modelo. O
     Administrador da instalação cadastra uma conexão global, válida para todas as organizações, por
     chave de API (OpenAI, Anthropic, Gemini, OpenRouter), endpoint compatível com OpenAI (Ollama, vLLM,
     LM Studio) ou login OAuth onde o provedor oferecer. **Assinatura via CLI** (Codex, Claude,
     Antigravity) é aceita **somente como opção experimental**, sinalizada na tela, porque os termos dessas
     assinaturas são de uso pessoal e ela exige um contêiner-ponte à parte.
  2. **Transcrição de áudio e vídeo tem conexão própria**, escolhida por quem instala entre as opções
     oferecidas (Whisper local, OpenAI, Groq, Gemini ou endpoint compatível). Sem ela configurada, o
     upload de áudio e vídeo fica desabilitado; o restante da IA funciona.
  3. **Acesso:** o admin da organização liga ou desliga a IA para a organização inteira. A IA só lê o que
     o usuário que pergunta já pode ver pelas regras de tenant (D17/D58) e permissões (D18/D55).
  4. **Consumo:** teto diário da instalação, cotas por organização e por usuário e painel de uso para o
     Administrador da instalação. Toda chamada é medida; estourou o teto, a IA para e avisa.
  5. **Privacidade:** senhas, tokens, cartões e chaves Pix são mascarados antes de qualquer envio ao
     modelo, sempre, qualquer que seja o provedor. O histórico de conversas é salvo e privado por usuário;
     o admin vê consumo, nunca o conteúdo.
  6. **Autonomia:** a IA lê e redige livremente, mas toda alteração (criar tarefa/subtarefa, editar, mover,
     atribuir, comentar, excluir, arquivar) é **proposta num cartão e só grava após aprovação** de quem pediu,
     com a permissão que essa pessoa já tem. O cartão de exclusão ou arquivamento mostra exatamente o que
     será afetado (item, subtarefas, anexos, horas). A auditoria (D34/D47) registra a ação aprovada com o
     autor humano e a marca "via IA".
  7. **Fontes:** dados do próprio Prisma, anexos, upload de ata/transcrição e de áudio/vídeo. E-mail,
     reuniões e documentos externos entram **somente via MCP**. MCP vale nos dois sentidos: o Prisma consome
     servidores MCP cadastrados e também se expõe como servidor MCP, autenticado por token pessoal com
     escopo e validade, com as permissões do dono do token e escritas convertidas em sugestões.
  8. Demandas extraídas de fontes vão para uma **caixa de sugestões por projeto**, nunca direto ao backlog.
     Citar a fonte é exibido quando existir, mas não é obrigatório. Na V1 nada roda sozinho: não há resumo
     agendado, triagem periódica nem alerta automático.
  9. **Busca da V1:** busca textual com as mesmas regras de visibilidade do usuário, mais ferramentas de
     consulta tipadas. Embeddings ficam atrás de uma interface para entrarem depois como opção.
  10. **Edição:** tudo entra na Community Edition.
  11. **Entrega em seis fatias**, cada uma com história, spec e G-SPEC próprios: (1) conexões, chat de
      leitura com botão flutuante, uso e cotas; (2) ações com aprovação; (3) escrita assistida na tarefa;
      (4) upload de ata/áudio/anexos e caixa de sugestões; (5) Prisma consumindo MCPs; (6) Prisma como
      servidor MCP.
  Histórias em `stories/ai-assistant.md`; spec da fatia 1 em `specs/ai-assistant-foundation.md` (`draft`).
  G-SPEC e G-MIGRATION de cada fatia continuam pendentes. (2026-09-24)

  Nota de reconciliação (2026-10-05): esta decisão estava identificada como D90 no trabalho local de 2026-09-24 e na memória. D90 já identifica acesso prévio ao projeto em main (2026-09-22). O alias histórico é preservado na recuperação privada; a numeração atual é D91. Nenhum G-SPEC/G-MIGRATION de IA foi aprovado nesta organização.

  Atualização de 2026-10-06: PO Vitor aprovou a SPEC-AI-001 e autorizou sua implementação explicitamente no
  chat. G-SPEC registrado no painel; a escolha técnica da identidade administrativa (TASK-117/G-SCOPE)
  continua pendente e foi enviada ao PO. Não foi concedido G-MIGRATION neste registro. A implementação
  começou somente pelo mascaramento, independente dessa identidade; a IA ainda não está disponível no produto.

  Atualização posterior de 2026-10-06: PO escolheu flag `IsPlatformAdministrator` no usuário Identity,
  independente dos perfis da organização (G-SCOPE/TASK-117). Aprovou a migration concreta
  `20261006134545_Add_AiAssistant_Foundation` e aplicação primeiro no banco E2E (G-MIGRATION).
  Fundação da fatia 1 implementada em `feat/ai-assistant-foundation`; produção aguarda publicação própria.
  TASK-117 mantém o restante do control plane no backlog; a escolha de identidade está resolvida.

  Publicação autorizada pelo PO em 2026-10-06 após validação: "e pode subir já" (G-DEPLOY).
  Aplicar a mesma migration aditiva com backup verificado e atualizar o manifesto operacional.
  Resultado: aplicada com backup e manifesto de 27 migrations; main/produção `1f2fe20` saudável,
  CI com seis jobs e 109 E2E aprovados sem retry. Conta indicada promovida explicitamente;
  modelo continua escolhido e configurado pelo instalador. (2026-10-06)
