# Assistente de IA do Prisma

**Status:** active
**Módulo:** ai-assistant
**Decisão:** D91
**Spec vinculada:** `specs/ai-assistant-foundation.md` (fatia 1, `approved` em 2026-10-06); fatias 2 a 6 ainda sem spec

## Origem

PO em 2026-09-24, depois de analisar o assistente do zapmind: "eu quero uma ia trazendo tudo que o cliente quer
como a ia do notion ou o brain do clickup", usando modelo escolhido por quem instala, já que o Prisma é open source.
O escopo foi fechado em perguntas e respostas na mesma sessão e registrado na D91.

## Histórias

### US-AI-001: conectar o modelo da instalação

**Como** Administrador da instalação
**Quero** conectar um modelo por chave de API, endpoint compatível com OpenAI, login OAuth ou, em caráter
experimental, assinatura via CLI
**Para** que todas as organizações usem IA sem que o Prisma embuta ou pague um modelo

- **Dado** uma instalação sem conexão **quando** o admin cadastra e testa uma conexão válida **então** a IA fica
  disponível para as organizações que a ligarem.
- **Dado** a opção de assinatura via CLI **quando** o admin a escolhe **então** a tela avisa que é experimental e
  de uso pessoal.

### US-AI-002: perguntar ao workspace pelo botão flutuante

**Como** pessoa de uma organização com IA ligada
**Quero** abrir um chat por um botão flutuante em qualquer tela e perguntar sobre projetos, tarefas, prazos e
pedidos de cliente
**Para** ter respostas e resumos de status sem montar filtros e relatórios

- **Dado** que estou dentro de um projeto **quando** pergunto "o que está atrasado aqui?" **então** a resposta
  considera esse projeto e mostra as tarefas de onde tirou a informação, quando houver.
- **Dado** uma tarefa que não posso ver **quando** pergunto sobre ela **então** a IA não revela seu conteúdo.

### US-AI-003: controlar consumo

**Como** Administrador da instalação
**Quero** definir teto diário, cotas por organização e por usuário e ver o consumo
**Para** que ninguém esgote a conexão sozinho e o custo não surpreenda

### US-AI-004: pedir mudanças pelo chat com aprovação

**Como** pessoa autorizada
**Quero** pedir ao chat para criar, editar, mover, atribuir, comentar, excluir ou arquivar tarefas
**Para** operar o Prisma em linguagem natural sem perder o controle do que é gravado

- **Dado** um pedido de mudança **quando** a IA entende o pedido **então** ela mostra um cartão com a proposta e
  nada é gravado até eu aprovar.
- **Dado** um pedido de exclusão **quando** o cartão aparece **então** ele mostra exatamente o que será afetado.

### US-AI-005: escrita assistida na tarefa

**Como** pessoa editando uma tarefa
**Quero** gerar ou melhorar descrição e critérios de aceite, resumir comentários e quebrar em subtarefas
**Para** escrever melhor e mais rápido

### US-AI-006: transformar atas, áudios e anexos em sugestões

**Como** gestor de projeto
**Quero** subir uma ata, transcrição, áudio ou vídeo de reunião, ou apontar anexos existentes, e receber as
demandas extraídas numa caixa de sugestões do projeto
**Para** não perder o que o cliente pediu

### US-AI-007: trazer e-mail e fontes externas via MCP

**Como** Administrador da instalação
**Quero** cadastrar servidores MCP (e-mail, documentos, reuniões) para a IA consultar
**Para** que pedidos de cliente fora do Prisma virem sugestões sem integração específica por provedor

### US-AI-008: agentes externos usando o Prisma via MCP

**Como** pessoa que usa agentes externos (Claude, Codex)
**Quero** gerar um token pessoal e conectar o Prisma como servidor MCP
**Para** que meu agente consulte o Prisma e proponha tarefas com as minhas permissões

## Observações humanas

- Toda alteração proposta pela IA passa por aprovação; na V1 nada roda de forma agendada.
- Citar a fonte é desejável, não obrigatório.
- Histórico do chat é privado por usuário.
- Tudo entra na Community Edition.
