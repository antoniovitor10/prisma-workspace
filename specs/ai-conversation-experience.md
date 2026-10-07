# SPEC-AI-004: Chat inspirado no Zapmind e escolha por conversa

**Status:** draft
**G-SPEC:** pendente; comportamento solicitado pelo PO em 2026-10-07.
**G-SCOPE:** escolha de provedor/modelo por conversa confirmada pelo PO: "Visual e escolha de provedor/modelo por conversa".
**G-MIGRATION:** pendente para duas colunas opcionais de preferência em `AiConversations`.
**Story:** US-AI-011 (`stories/ai-conversation-experience.md`).
**Fase:** 15, evolução da experiência de conversa da D91.
**Relação:** estende SPEC-AI-001/002/003. Substitui somente a obrigatoriedade de usar a conexão global em toda chamada; a conexão global continua como padrão. Desktop pode expandir o painel por ação explícita.

## Objective

Adaptar a composição do chat do Zapmind ao Prisma, com seleção de provedor/modelo por conversa, mantendo autenticação oficial, identidade D68 e isolamento de dados.

## Scope

- Painel lateral com modo expandido, histórico privado, contexto automático e compositor confortável.
- Seletores de conexão/provedor e modelo dentro do chat; preferência persistida por conversa.
- Catálogos consultados no servidor, com credenciais das conexões existentes, sem expor segredo ou realizar inferência para listar modelos.
- Refinar visualmente a seção de conexões: estados claros, poucos campos principais e detalhes secundários expansíveis.
- Referência de composição: arquivos estáticos e ponte do Zapmind na mesma VPS, conferidos sem ler conversas ou credenciais. Nenhuma conta do Zapmind será importada.
- Fora deste recorte: anexos, escrita em tarefas, financeiro, Notion, Antigravity, autenticação individual por usuário e limites reais da assinatura do provedor. Esses recursos do Zapmind não são copiados nesta entrega.

## Functional Requirements

1. Manter botão flutuante, painel lateral inicial, Escape/retorno de foco e contexto da rota da SPEC-AI-001. Acrescentar Expandir/Recolher no desktop; em até 768px, largura completa. Expansão não interrompe resposta nem altera contexto.
2. Inspirar-se no Zapmind: cabeçalho de conversa, histórico acessível, área de leitura ampla, compositor fixo na base e controles de provedor/modelo junto à pergunta. Preservar tokens D68, contraste, temas claro/escuro e tipografia do Prisma. Sem novas bibliotecas, ícones ou textos com emojis.
3. Histórico continua privado por organização e dono. Selecionar conversa mostra sua preferência salva, mensagens e referências existentes; Nova conversa começa com Usar padrão da instalação. Expansão usa histórico lateral; modo compacto/mobile oferece o mesmo histórico sem transbordamento.
4. Somente conexões de finalidade Chat com teste bem-sucedido e configuração válida podem ser escolhidas. A ativa continua sendo o padrão; outras conexões testadas ficam disponíveis para escolha explícita. Preservar a exigência atual de IA habilitada e conexão padrão ativa para disponibilizar o chat.
5. Se houver várias conexões do mesmo provedor, identificar o nome da conexão para evitar escolher outra conta/endpoint. Usuários comuns não recebem credencial, sufixo, URL privada, instrução de login ou controles administrativos.
6. Consultar modelos da conexão selecionada pelos adaptadores existentes. CLI usa autenticação oficial e catálogo instalado; API usa credencial salva. Modelos manuais são limitados ao modelo já configurado/testado quando o endpoint não oferece catálogo; não aceitar identificador arbitrário do navegador.
7. Trocar conexão limpa o modelo incompatível, cancela a consulta anterior e carrega apenas seu catálogo. O modelo configurado pode ser oferecido como opção inicial compatível; nenhuma resposta tardia sobrescreve outra seleção. Mostrar carregamento, falta de login, lista vazia, erro e nova tentativa com prazo limitado.
8. Persistir a escolha no servidor por conversa/dono/organização. Antes da primeira mensagem, criar a conversa ao salvar a escolha ou ao enviar, sem criar conversas adicionais por render. Trocar modelo depois de mensagens preserva o histórico; as próximas chamadas usam a nova escolha. Controles ficam desabilitados durante uma chamada em andamento.
9. Preferência explícita não altera conexão/modelo/conta globais, não ativa provedores e não dispara login ou teste de inferência automaticamente. Catálogo não garante acesso; a própria chamada pode ser recusada pelo provedor e deve mostrar a recusa. O teste administrativo continua obrigatório para tornar a conexão elegível.
10. Validar no servidor, antes de persistir/enviar: vínculo da conversa, organização habilitada, conexão ainda testada e método válido, modelo do catálogo permitido ou modelo configurado no fallback manual. Conexão apagada/inválida ou modelo removido exige nova escolha; não migrar silenciosamente para outro modelo/provedor, inclusive API paga.
11. Escolha nula mantém a resolução do padrão global a cada chamada. Conversas anteriores continuam assim, sem backfill. Oferecer Usar padrão da instalação para remover a preferência explícita.
12. Todas as chamadas usam os mesmos controles da fundação: contexto/autorização, mascaramento, protocolo de leitura, tempo/saída, cancelamento e cotas da instalação/organização/usuário. Registrar conexão/modelo efetivamente utilizados no uso; reserva não pode ser contornada pela troca de provedor.
13. Não reutilizar preço ou capacidade de ferramentas de um modelo diferente sem evidência. Preço do catálogo quando disponível; se o modelo mudou e o preço não é conhecido, custo desconhecido. Para modelos sem capacidade testada, usar envelope de ferramentas de leitura no backend.
14. Configuração permanece exclusiva ao Administrador da instalação. A seção apresenta conexão, provedor/modelo e estado; uma ação principal conforme estado, edição e ações secundárias. Formulário mantém Provedor, Como conectar, login/chave e Modelo, com risco/aceite CLI e avançadas preservados.

## Contracts

- `GET /api/ai/options`: conexões elegíveis e padrão, somente metadados necessários ao chat, sem segredo/endpoint/conta.
- `GET /api/ai/options/{connectionId}/models`: catálogo dessa conexão elegível usando a credencial no servidor; exige usuário/membership e IA habilitada, não autoridade administrativa global.
- `PATCH /api/ai/conversations/{id}/selection`: `{ connectionId, model }`; ambos nulos removem preferência. Validar antes de gravar e devolver preferência normalizada. Proibir alteração enquanto a conversa estiver gerando.
- Listagem/detalhe das conversas devolvem preferência salva e estado de disponibilidade. Mensagens resolvem a preferência persistida no servidor, sem confiar em provedor/modelo/URL/segredo enviados pelo cliente.
- Controllers continuam orquestrando MediatR. Implementação em Application/Infrastructure mantém os controles existentes e transporte CLI com tamanho conhecido.
- Catálogos têm limite de tamanho/tempo e podem ter cache curto vinculado à conexão e seu estado de teste. Alterar/testar conexão ou iniciar login invalida o cache; nunca compartilhar credenciais ou resposta entre conexões distintas.

## Data Impact

Migration aditiva proposta `Add_AiConversation_Selection`: adicionar somente:

```sql
ALTER TABLE [AiConversations] ADD
    [SelectedConnectionId] uniqueidentifier NULL,
    [SelectedModel] nvarchar(200) NULL;
```

- Sem backfill, FK, índice novo, exclusão ou alteração de tabelas de trabalho. Escolhas anteriores permanecem nulas e usam o padrão global.
- ID opcional preserva a preferência mesmo se a conexão for excluída; o servidor informa indisponibilidade e exige escolha, preservando conversa/mensagens.
- Aplicação primeiro ao SQL E2E dedicado, testes e manifesto incremental atualizado. Produção somente após checks, backup e autorização de publicação já concedida pelo PO.
- Rollback de imagem mantém as duas colunas; não executar Down em produção nem apagar preferências automaticamente.

## Acceptance Criteria

- Painel, expansão e mobile mostram histórico, contexto, pergunta e seletores legíveis; Escape/foco, streaming e interrupção continuam corretos.
- Dois provedores/conexões testados podem ser usados por conversas distintas sem trocar a conexão padrão da instalação.
- Modelo acompanha a conexão; consulta atrasada/falha não libera modelo errado nem trava indefinidamente.
- Preferência volta após recarregar/reabrir a conversa; outra pessoa/organização não consegue consultar, modificar ou utilizar seu histórico.
- Revogar teste, apagar conexão ou remover modelo bloqueia a chamada e exige escolha explícita; nenhum fallback pago/silencioso.
- Catálogos e opções não expõem segredo, conta, endpoint privado ou funções administrativas a usuários comuns.
- Uso e cotas refletem a escolha efetiva; modelo sem preço aparece como custo desconhecido.
- Migration não modifica mensagens/dados de trabalho e conversas anteriores continuam no padrão.
- E2E geral desktop/mobile aprovado antes da publicação.

## Tests and Gates

G-SPEC e G-MIGRATION explícitos antes de código/schema novos. Backend: permissões, seleção, catálogo, persistência, revogação, custo e cotas. Frontend: troca/consulta tardia/erros/expansão e build `tsc -b`. E2E com dois provedores falsos controlados, reabertura, outro usuário/organização, interrupção e regressões de configuração. Conferência visual desktop/mobile em uma rodada conjunta e no máximo uma confirmação após correções. Nenhuma autenticação humana ou inferência paga pelo agente.
