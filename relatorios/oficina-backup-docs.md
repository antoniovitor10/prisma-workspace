# Oficina — TASK-504: roteiro de backup e restauração Community

Base exata: a2797a7. Branch docs/community-backup-20261020. US-OPEN-SOURCE-001 e SPEC-OPEN-SOURCE-DISTRIBUTION aprovadas. Mudança somente de documentação, sem migration. PR parcial: o gate clean-install da TASK-504 ainda está pendente; não é ENTREGA final.

## Entrega

Novo docs/installation/backup-restore.md e referências no guia Docker. O roteiro reúne banco, anexos, chaves e configuração do mesmo ponto; explica captura com a aplicação parada, COPY_ONLY/CHECKSUM/VERIFYONLY, cópia para o host e recuperação em destino isolado. A restauração exige nomes lógicos consultados por FILELISTONLY, arquivos novos e recusa banco já existente, sem REPLACE. Preserva imagem/commit correspondente e diferencia recuperação de downgrade de schema.

Não foram modificados aplicação, CI, Compose de produto, serviços institucionais ou contratos. Foram lidos o contexto, história, spec e configurações do Compose/Dockerfile para conferir caminhos e volumes. As referências oficiais estão junto ao roteiro.

## Verificação real

- Os cinco blocos shell passaram em bash-n.
- Backup SQL próprio:11653120 bytes, COPY_ONLY/CHECKSUM e VERIFYONLY aprovados. Fonte exclusiva de QA sem API ativa durante captura; restaurado sob um novo nome, com MOVE e sem sobrescrever banco.
- Contagens iguais antes/depois:29 migrations já existentes,47 tarefas,19 organizações,3 usuários. DBCC CHECKDB aprovado, sem erros informados. Nenhuma migration nova foi criada.
- Contraprova:uma cópia privada teve o cabeçalho corrompido; VERIFYONLY recusou com exit1. O original intacto foi novamente aprovado com exit0. A cópia inválida foi removida do host e do container de QA.
- Dois arquivos sintéticos representando anexo e chave foram copiados com Compose cp de containers parados e recuperados em volumes novos; SHA256 idêntico nos dois arquivos. Helpers próprios sem portas, com limite de64MB/0,1CPU, já removidos com os quatro volumes próprios. A primeira tentativa encontrou pools Docker esgotados; somente esses helpers passaram a network_mode:none. Nenhuma rede compartilhada foi alterada.

Scripts, logs e backup ficam apenas em .runlogs/oficina, ignorados pelo Git. A conferência SQL pontual de11MB não é suíte pesada; as suítes de produto continuaram serializadas. Os resultados não vêm de produção. A base restaurada e o backup temporário no SQL foram removidos nominalmente ao encerrar o ensaio; a fonte de QA permaneceu intacta.

## Autorrevisão e limites

Autorrevisão:sem achados altos/médios confirmados. Roteiro evita restauração destrutiva e informa preservação conjunta de arquivos/configuração. Revisão humana deve conferir caminhos customizados, arquivos SQL adicionais e credenciais da instalação de destino. Os exemplos usam o banco padrão; o operador substitui seu nome e os nomes lógicos reais.

Auto-QA:verificação técnica de SQL e arquivos acima. Não foi executado um quickstart completo novo com login/anexo real depois da recuperação, nem upgrade entre versões diferentes. O ensaio de arquivos usa conteúdo sintético, sem material criptográfico real. A homologação ampla da TASK-507 permanece pendente. E2E e builds de produto não repetidos:exceção formal de documentação, sem mudança funcional. Essa exceção não dispensa o gate clean-install específico da TASK-504; falta o ensaio integral em base vazia e a recuperação funcional da aplicação, que serão feitos só em recursos próprios de QA.

Nada em produção, main, merge de PR ou deploy. REVISAO e QA oficiais:pendentes.

## Gate clean-install concluído — 20/10/2026

Ensaio real com a imagem candidata própria do CI39402799260, mergec24d7042fa1374d9d417621cd64153e74c94222c, artefato12255596452. Checksum do arquivo inteiro e identidade OCI verificados: tag única, manifesto e config ligados pelos respectivos SHA256. A primeira tentativa parou antes de criar recursos porque o harness esperava digest de config como DockerId; este host retorna o digest do manifestoOCI. Não era falha do produto. Corrigida a checagem para aceitar somente as duas identidades comprovadamente vinculadas ao mesmo arquivo, sem dispensar hash. Primeiro cleanup confirmou ausência de recursos.

Segunda execução nativa:2PASS,1,1min, desktop1440x900 e mobile390x844. Instalação em banco vazio sem usuários/organizações; bootstrap201 e replay409. Criados usuário administrativo, projeto, quadro, tarefa, anexo real48bytes e keyring DataProtection. Após parar a fonte, backupCOPY_ONLY/CHECKSUM, VERIFYONLY, restauração em outro nome com MOVE e DBCC CHECKDB aprovados. API restaurada preservou a sessão JWT anterior, permitiu novo login, manteve campos/identificadores/responsável da tarefa e recuperou bytes do anexo. Hashes do arquivo de dados e da chave DataProtection idênticos;1arquivo de cada. Resultados públicos no JSON privado clean-recovery-result.json, sem senha.

Auto-QA: as duas capturas foram abertas e examinadas. Tarefa e anexo restore-proof.txt visíveis e utilizáveis nas duas larguras; painel de anexos cabe no viewport. A navegação horizontal das abas e a chave técnica visível são comportamentos da base desta imagem, tratados em PRs próprios, sem correção visual nesta documentação. Não constitui homologação integral de outras telas.

Limpeza nominal verificada após encerrar:2containers,4volumes,2bancos próprios removidos; tags/backup temporários próprios ausentes; cleanupVerified=true e errors=[]. Nenhum recurso externo ou produção tocado. A documentação fecha o gate específico de instalação/restauração da TASK504; upgrade entre versões diferentes e TASK507 continuam pendentes. Nenhuma migration nova. REVISAO e QA oficiais:pendentes. PR35 continua rascunho.
