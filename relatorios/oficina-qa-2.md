# Oficina qa-2 — C2, controles visuais prioritários

Branch fix/qa-round-1, base a2797a7 com merges L06/L07/L08. PR27 permanece em rascunho. Produto e testes deste grupo foram verificados em e705aec; o commit desta entrega acrescenta documentação e preparação de enquadramento/diagnóstico dos outros grupos, sem mudar o produto C2.

D04: Topbar reserva a largura do seletor de organização e permite que a busca encolha antes dele. D15: OrganizationSettings usa trilhas mínimas zero e uma coluna no mobile, inclusive na grade ancestral. D18: ProjectWiki permite empilhar título, estado e ações no celular, preservando Histórico alcançável.

Verificação local: frontend 27 Vitest em6 arquivos, tsc-b, lint e build passaram. Backend de produto não mudou neste grupo; os gates afetados de C1/C6/Brasília permaneceram verdes. E2E final qa-round-responsive:7/7PASS, incluindo setup e3 casos em1440x900/390x844. Contraprova com os3 componentes anteriores:3FAIL esperadas (D04desktop, D15mobile, D18mobile),4PASS; finally restaurou e o lote completo voltou a7/7PASS. Sem alteração de prazo,retry,cota ou tolerância geométrica.

Asserções conferem limites da viewport e hit test na esquerda,centro e direita do controle. O roteiro também abre o Histórico de versões. Seis capturas after finais abertas e inspecionadas; capturas before e counterproof-before preservadas em.runlogs/oficina/screenshots. Cada defeito possui imagens nas duas larguras.

Falhas intermediárias: a primeira fixture longa gerou Slug acima da coluna existente e retornou500 antes do teste visual; passou a informar slug curto explícito e ler id do DTO. Esse defeito de geração de slug está registrado em PERGUNTAS, sem correção incidental de schema. Aplicar ainda ultrapassava3px após a primeira mudança: captura apontou o mínimo da grade ancestral, corrigido sem aumentar tolerância.

Autorrevisão: nenhum achado alto/médio confirmado no recorte. A largura reservada não depende do tamanho do nome e os controles preservam handlers e contratos. Auto-QA: os3 controles-alvo estão alcançáveis nas duas larguras e a navegação para versões funciona. Não se afirma que toda a página ficou sem defeitos: o status “Falha ao salvar” da Wiki já aparece nas capturas anteriores, e o botão de criar template de workflow continua cortado no mobile. Esses achados adjacentes foram registrados, sem declarar sua correção.

Não verificado: suíte completa da branch, prevista ao terminar C1–C6; correção do salvamento da Wiki, geração de slug longo ou layout do template. Risco: possíveis interações com telas adjacentes serão revistas no full e na varredura final. Migration nova:não. Nenhuma produção,main,merge dePR ou deploy.

REVISAO e QA oficiais:pendentes. A entrega é do grupo C2, não liberação da branch completa.
