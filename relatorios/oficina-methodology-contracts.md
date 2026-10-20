# Oficina — TASK-123, consumidores operacionais sem metodologia

Base origin/main a2797a71b79543cf71930d04547021564a7e4e88; branch fix/methodology-consumers-hidden. Spec project-methodology-hidden aprovada, histórias canônicas US-PROJECT-METHODOLOGY-HIDDEN-004/005 no catálogo. O cherry-pick38156e8 traz somente a correção de espera terminal no teste do editor, comprovada no PR30.

## Alteração

Kanban, tarefa, backlog, sprint e relatórios deixam de condicionar pontos à metodologia interna. Os contratos das props operacionais de backlog/sprint excluem esse campo; relatório lê somente id/key/name. O tipo local da tarefa e a leitura do Kanban também deixam de oferecê-lo. A consulta adicional do relatório, usada apenas para essa condição, foi retirada. Dados, enum, coluna, contratos HTTP e leitura de compatibilidade nas configurações permanecem. Não se afirma resolver a mutabilidade histórica da API fora deste recorte.

Escolha reversível registrada em PERGUNTAS: manter estimativas disponíveis para todos, em vez de retirar a capacidade dos projetos legados. A spec exige experiência igual entre valores internos. Preferências de mostrar pontos nos cartões continuam funcionando; não se normalizam valores persistidos. Duas asserções antigas que exigiam ocultação foram substituídas por exigência positiva do campo, preservando os demais controles.

## Evidência até o checkpoint

Afetados52PASS em cinco arquivos. Vinte casos com os quatro valores internos nas cinco superfícies. Contraprova temporária das cinco implementações anteriores:5FAIL/15PASS,32 casos não selecionados pelo filtro; finally restaurou as fontes. Restauração20PASS/32 não selecionados. Nenhum skip novo foi escrito. Logs locais próprios preservados.

O primeiro tsc-b --force identificou duas opções exact inválidas de Testing Library nos testes novos. Corrigidas; o gate final será repetido após congelar as fontes. Não se relata tipo limpo com base em npx tsc --noEmit. Nova tipagem das props também será checada no fechamento. Backend build/xUnit completos, lint/build/fullVitest, tipos explícitos E2E e nativo ainda em andamento ou enfileiramento. O E2E novo compara quatro valores persistidos em projetos exclusivos, pontos reais e preservação do valor, nas larguras1440x900/390x844. Arquiva as fixtures no finally. Ainda não executou.

## Autorrevisão e auto-QA

Autorrevisão inicial sem alto/médio confirmado: mudança de disponibilidade, não de valor de estimativa; compatibilidade de edição e dados preservada. Risco: campos adicionais aumentam a altura da tarefa no celular. Auto-QA visual ainda pendente; não afirmar aprovação. Não há framework novo ou mudança de arquitetura.

Migration: não. REVISAO e QA oficiais: pendentes. Ainda não é ENTREGA. Somente branch própria e futuro PR em rascunho; sem produção, main, merge ou deploy.

Atualização intermediária: build backend aprovado com três avisos anteriores; fullxUnit690PASS/33skips existentes/0FAIL em46s. Bootstrap de banco próprioPrisma_BoardColumns_Oficina_Methodology_Seeded concluído e API temporária encerrada. Tipagem explícita dos dois E2E aprovou. Gates frontend finais aguardam CPU enquanto roda a suíte combinada; o primeiro tipo com duas opções RTL inválidas permanece registrado, sem declarar aprovação antecipada. E2E focal e completo desta branch serão executados somente após frontend final aprovado.
