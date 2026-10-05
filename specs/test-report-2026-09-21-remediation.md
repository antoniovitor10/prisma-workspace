# SPEC-TEST-REPORT-2026-09-21: Remediação residual do relatório de testes

**Status:** approved

## Origem e aprovação

O PO solicitou explicitamente em 23/09/2026: “aplique tudo que faltar”, referindo-se ao relatório de testes de 21/09/2026. A auditoria comparou cada apontamento com o produto atual. Esta spec registra somente as lacunas residuais comprovadas; itens já entregues não serão reimplementados.

## Escopo

- Traduzir os tipos de evento que o backend efetivamente grava no histórico do projeto.
- Confirmar visualmente a inclusão ou alteração de papel de um membro do projeto.
- Exibir erros de criação e de ações de equipes como alertas próximos do contexto afetado.
- Preservar o fallback legível para tipos futuros de histórico.
- Exigir confirmação final antes de excluir uma coluna, descrevendo a transferência das tarefas quando houver conteúdo.
- Permitir colar ou arrastar imagens para a descrição rica da tarefa, preservando o upload autenticado já existente.
- Remover da criação de coluna o seletor técnico de classificação apontado no E03; novas colunas começam como “Em andamento” e continuam reclassificáveis no fluxo de edição, que explicita o impacto.
- Renderizar URLs públicas de imagem na descrição da tarefa e impedir a inclusão de uma imagem quebrada quando o endereço não carregar, conforme o E20.
- Apresentar a transferência de tarefa em painel legível e responsivo, deixando explícitos origem, destino, dependência entre quadro e coluna e preservação dos dados.
- Fazer o link do Portal Externo refletir apenas o endereço efetivamente salvo e publicado, sem oferecer uma URL baseada em alterações locais ainda não persistidas.
- Não exibir seletores de papel que não alteram o acesso efetivo: o responsável principal permanece Administrador e perfis administrativos herdados da organização devem indicar claramente essa origem.

## Fora de escopo

- Migration, nova entidade, novo endpoint ou alteração de autorização.
- Reimplementar colunas independentes, timer único, notificações, descrição rica, subtarefas ou demais itens já presentes e cobertos no produto.
- Criar central global de ajuda, documentação extensa ou ajuda campo a campo em formulários que não foram citados no relatório.

## Critérios de aceite

- `member_updated`, `custom_field_saved` e `custom_field_disabled` aparecem em português no histórico.
- Ao adicionar um membro, a seção informa “Membro adicionado ao projeto.”; ao alterar seu papel, informa “Papel do membro atualizado.”.
- A confirmação usa uma região de status acessível e um erro continua usando uma região de alerta.
- Erro ao criar equipe aparece junto do formulário de criação; erro de outra ação aparece antes da lista de equipes, sem o espaçamento de estado vazio.
- Excluir uma coluna exige confirmação que informa o nome da coluna e, quando necessário, a quantidade e o destino das tarefas.
- Cada cabeçalho de coluna exibe uma ação de lixeira nomeada. Ao acioná-la, a opção visual de exclusão e a orientação de transferência dos cartões ficam disponíveis.
- Imagens arrastadas para a descrição são enviadas como anexos autenticados e inseridas no ponto de soltura, sem base64 no HTML.
- O modal “Criar Nova Coluna” contém somente o nome e as ações; não exibe a combobox ambígua. A API recebe a classificação padrão “Em andamento”.
- Uma URL HTTPS direta de imagem válida é baixada, validada, salva como anexo autenticado e renderizada no editor. Se o download falhar, nada é inserido e a interface orienta verificar se a URL é pública e direta.
- Ao usar “Inserir link” sem texto selecionado com uma extensão de imagem conhecida, o conteúdo é inserido como imagem, não como link textual.
- O painel “Transferir tarefa” organiza quadro e coluna em campos rotulados, desabilita a coluna até a escolha do quadro, resume origem e destino e informa que subtarefas e histórico são preservados.
- “Abrir” e “Copiar” usam o caminho retornado pelo portal persistido. Ao editar publicação ou endereço sem salvar, a interface mantém o link vigente e informa que existem alterações não salvas; antes da primeira publicação salva, nenhum link é oferecido.
- O responsável principal e membros elevados pelo perfil da organização exibem acesso administrativo informativo, sem combobox ineficaz. Membros comuns mantêm o seletor funcional, com persistência, feedback e reversão visual em caso de erro. A API rejeita o rebaixamento fictício do responsável principal.

## Impactos

- Dados, entidades, migrations e contratos HTTP: nenhum.
- Segurança: mensagens reutilizam o erro já sanitizado pela camada de API e não ampliam permissões. Imagens por URL exigem HTTPS, limite de 10 MB e tipo `image/*`; o HTML persiste somente a referência opaca do anexo.
- Rollback: reverter os arquivos de interface, testes, história, spec e registro de progresso desta entrega.
