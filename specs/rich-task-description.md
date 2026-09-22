# SPEC-RICH-TASK-DESCRIPTION

**Status:** approved
**Aprovação humana:** pedido direto do PO em 2026-09-21 para criar a descrição como a referência, “bem grande, fácil de mexer e com essas funcionalidades”; ampliado pelo pedido direto de 2026-09-22: “preciso colar imagem também na parte de descrição, quero algo mais fluído”. Sem migration.

## Contrato

- A aba Descrição abre com editor rico ocupando a largura disponível e altura útil mínima ampla, responsivo no celular.
- Barra de ferramentas fixa no editor: desfazer, refazer, parágrafo/títulos, negrito, itálico, sublinhado, tachado, realce, link, listas com marcadores e numerada, alinhamento, checklist, citação, bloco de código, imagem e atalho para anexos.
- Modo expandido ocupa a janela e restaura o foco/editor ao sair; `Escape` sai do modo expandido antes de fechar o detalhe.
- Atalhos nativos do editor permanecem ativos e todo botão possui nome acessível e estado ativo perceptível.
- Salvamento automático ocorre após pausa breve e também ao perder foco ou desmontar, com estados Salvando, Salvo e Falha ao salvar. A pessoa não perde texto ao clicar na barra.
- Conteúdo é persistido como HTML sanitizado no campo atual `WorkItem.Description`; texto legado abre como parágrafo editável. Sem alteração de schema.
- Imagem aceita URL segura e colagem direta da área de transferência. Ao colar uma ou mais imagens, cada arquivo é enviado pelo mecanismo autenticado de anexos, inserido na posição da edição e exibido sem incorporar base64 ao HTML persistido.
- A interface informa envio, sucesso e falha sem bloquear a continuação da edição. A imagem colada também aparece no módulo Anexos e continua protegida pelas permissões da tarefa.
- Upload de outros arquivos continua no módulo Anexos; o botão da barra navega até essa aba.
- A “Edição com IA” da referência fica fora deste recorte até contrato próprio de IA.

## Segurança e compatibilidade

- HTML é sanitizado no backend antes de persistir.
- A descrição persiste apenas a referência opaca do anexo. O arquivo não recebe URL pública e é carregado pelo endpoint autenticado já existente.
- Outros pontos que exibem descrição como texto devem converter HTML para texto legível, sem renderização insegura.
- Limite atual de 4.000 caracteres permanece vigente.

## Validação

- Testes de componente cobrem formatação, salvamento e expansão.
- E2E cobre edição, colagem e persistência da imagem após reabrir, anexos e viewport desktop/mobile.
- Build, Vitest, xUnit e suíte Playwright completos são obrigatórios antes de concluir ou publicar.
