# US-TEST-REPORT-2026-09-21-001: Concluir lacunas objetivas do relatório de testes

**Status:** active
**Módulo:** configurações, navegação, quadro e detalhe da tarefa
**Spec vinculada:** `specs/test-report-2026-09-21-remediation.md`

## História

**Como** pessoa que administra projetos e equipes
**Quero** receber feedback contextual e ler o histórico em português
**Para** concluir as ações sem dúvida sobre o resultado ou a permissão necessária

## Cenários esperados

- **Dado** um evento administrativo emitido pela API
  **Quando** o histórico do projeto é aberto
  **Então** seu rótulo conhecido é apresentado em português.
- **Dado** um membro adicionado ou atualizado no projeto
  **Quando** a operação termina
  **Então** a interface confirma o resultado junto da seção de membros.
- **Dado** uma ação de equipe recusada
  **Quando** a API devolve o erro
  **Então** a mensagem aparece como alerta perto do contexto da ação.
- **Dado** uma coluna existente
  **Quando** a pessoa solicita sua exclusão
  **Então** um ícone de lixeira visível abre a opção de exclusão, e a interface exige confirmação final e explica o destino das tarefas contidas nela.
- **Dado** uma imagem disponível no computador
  **Quando** a pessoa a arrasta para a descrição da tarefa
  **Então** o arquivo é anexado e inserido no conteúdo com o mesmo fluxo seguro da colagem.
- **Dado** o modal de criação de coluna
  **Quando** a pessoa informa o nome da nova coluna
  **Então** nenhuma seleção ambígua de coluna ou classificação é exibida, e a nova coluna começa como “Em andamento”.
- **Dado** um endereço público que aponta diretamente para uma imagem
  **Quando** a pessoa o insere na descrição pelo controle de imagem, ou pelo controle de link sem texto selecionado
  **Então** a imagem é validada e renderizada no conteúdo, sem virar apenas um link.
- **Dado** um endereço que não entrega uma imagem carregável
  **Quando** a pessoa tenta inseri-lo como imagem
  **Então** o editor não inclui conteúdo quebrado e apresenta orientação para corrigir a URL.
- **Dado** uma tarefa que precisa mudar de quadro
  **Quando** a pessoa expande a transferência
  **Então** origem, quadro de destino, coluna e impacto aparecem organizados em um painel responsivo, e a coluna só fica disponível após escolher o quadro.
- **Dado** o endereço ou a publicação do Portal Externo alterados no formulário
  **Quando** a pessoa ainda não salvou a configuração
  **Então** a tela não oferece uma URL inexistente, preserva o link publicado vigente e avisa que há alterações não salvas.
- **Dado** um responsável principal ou membro com acesso administrativo herdado da organização
  **Quando** a lista de membros do projeto é exibida
  **Então** a origem do acesso é informada e não aparece um seletor incapaz de alterar a permissão efetiva; membros comuns continuam com edição persistente de papel.

## Observações humanas

Origem: solicitação explícita do PO em 23/09/2026 para aplicar tudo que ainda faltava no relatório de testes de 21/09/2026. O PO esclareceu no mesmo dia, com a imagem do item S05, que “ajuda” significa ícones clicáveis com explicações curtas sobre cada aba e função. Após a releitura visual apontar E05, S05 e S12 como parciais, o PO respondeu “faça”, aprovando a complementação descrita nestes cenários. O documento continua sendo evidência de teste; não autoriza funcionalidades alheias aos itens apontados.

Em 24/09/2026, o PO enviou a imagem do item E03 e solicitou explicitamente “remova essa combo box”. A remoção se limita ao modal de criação; a edição preserva a classificação funcional e suas confirmações de impacto.

Também em 24/09/2026, o PO retomou o E05 e solicitou explicitamente um ícone de lixeira com a opção visível. A ação direta reutiliza o fluxo seguro de transferência e confirmação já aprovado.

Ainda em 24/09/2026, o PO enviou a evidência do E20 e solicitou renderizar a imagem em vez de adicionar somente o link. A correção valida o carregamento no navegador antes de persistir o elemento.

Em seguida, o PO enviou novamente a imagem do S05 e solicitou “desfaça essa alteração”. Os ícones e painéis de ajuda contextual foram retirados de configurações, navegação global, abas do projeto e detalhe da tarefa; os demais itens permanecem autorizados.

Na sequência, o PO solicitou melhorar a visualização de “Transferir tarefa”. A alteração é estritamente visual e preserva o endpoint e as regras atuais de transferência.

Depois, o PO reapresentou a evidência “Link Externo não funciona” e solicitou a correção. O link público passa a representar exclusivamente o estado persistido do portal.

Por fim, o PO apresentou a evidência “Permissões que não alteram nada” e solicitou a correção. A interface deixa de prometer alterações sobre acessos fixos ou herdados, e o backend protege o papel obrigatório do responsável principal.
