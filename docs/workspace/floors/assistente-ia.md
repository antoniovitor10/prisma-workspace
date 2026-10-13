# Assistente IA — fundação

Andar Maestri: `018f5cbc-e9ac-4e3e-9d1b-96f8a7083938`. Atualizado em 2026-10-06.

## IA — objetivo e escopo

Objetivo: implementar e validar a primeira fatia do assistente de IA do Prisma. Componentes: API, Infrastructure, Application, Web e testes no mesmo monorepo. Fonte: pedido do PO registrado em 2026-09-24, stories/ai-assistant.md e memória. Decisão atual D91; alias histórico D90 de 24/09, distinto da D90 de acesso de 22/09. Contrato: specs/ai-assistant-foundation.md, SPEC-AI-001, status approved pelo PO em 2026-10-06. Fundação implementada na branch local `feat/ai-assistant-foundation`: conexões, ferramentas de leitura autorizadas, streaming, histórico privado, mascaramento, painéis e cotas.

## IA — próximo resultado e gates

Próximo resultado: teste humano em produção, `https://prisma.nordevs.com.br/settings`, configurando modelo e habilitando organização. G-SPEC, G-SCOPE, G-MIGRATION e G-DEPLOY aprovados pelo PO; main/produção `1f2fe20` saudável, 109 E2E da imagem aprovados sem retry e seis skips condicionais. Migration aplicada ao E2E e à produção com backup verificado; conta indicada recebeu flag Identity. Fatias futuras: ações aprovadas, escrita assistida, atas/áudios/anexos, consumo e exposição de MCP; não juntar implementação dessas fatias nesta fundação. Não registrar chaves em notas. Fonte documental preservada; históricos externos não lidos.

As notas estão no canvas deste andar. Nenhum terminal temporário ou agente de IA permanece aqui.
