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
