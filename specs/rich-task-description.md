# SPEC-RICH-TASK-DESCRIPTION

**Status:** approved
**Aprovação humana:** pedido direto do PO em 2026-09-21 para criar a descrição como a referência, “bem grande, fácil de mexer e com essas funcionalidades”. Sem migration.

## Contrato

- A aba Descrição abre com editor rico ocupando a largura disponível e altura útil mínima ampla, responsivo no celular.
- Barra de ferramentas fixa no editor: desfazer, refazer, parágrafo/títulos, negrito, itálico, sublinhado, tachado, realce, link, listas com marcadores e numerada, alinhamento, checklist, citação, bloco de código, imagem e atalho para anexos.
- Modo expandido ocupa a janela e restaura o foco/editor ao sair; `Escape` sai do modo expandido antes de fechar o detalhe.
- Atalhos nativos do editor permanecem ativos e todo botão possui nome acessível e estado ativo perceptível.
- Salvamento automático ocorre após pausa breve e também ao perder foco ou desmontar, com estados Salvando, Salvo e Falha ao salvar. A pessoa não perde texto ao clicar na barra.
- Conteúdo é persistido como HTML sanitizado no campo atual `WorkItem.Description`; texto legado abre como parágrafo editável. Sem alteração de schema.
- Imagem aceita URL segura. Upload de arquivo continua no módulo Anexos; o botão da barra navega até essa aba.
- A “Edição com IA” da referência fica fora deste recorte até contrato próprio de IA.

## Segurança e compatibilidade

- HTML é sanitizado no backend antes de persistir.
- Outros pontos que exibem descrição como texto devem converter HTML para texto legível, sem renderização insegura.
- Limite atual de 4.000 caracteres permanece vigente.

## Validação

- Testes de componente cobrem formatação, salvamento e expansão.
- E2E cobre edição, persistência após reabrir, anexos e viewport desktop/mobile.
- Build, Vitest, xUnit e suíte Playwright completos são obrigatórios antes de concluir ou publicar.

