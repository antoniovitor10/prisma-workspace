# Oficina — TASK-503: manutenção NuGet do parser HTML

Base exata: a2797a7. Branch fix/nuget-security-20261020. Referências: D38/D78, US-OPEN-SOURCE-001 e SPEC-OPEN-SOURCE-DISTRIBUTION aprovada. Checkpoint; E2E e frontend completos ainda pendentes.

## Alteração e compatibilidade

HtmlSanitizer foi atualizado de9.0.967 para9.1.982, patch final da menor família minor estável que já declara suporte ao parser corrigido. O pacote anterior exigia exatamente AngleSharp0.17.1; a proposta resolve AngleSharp1.7.0 e AngleSharp.Css1.0.1 dentro dos contratos do fornecedor. A dependência direta continua na família9, mas os transitivos mudam de major. Essa escolha reversível está registrada em PERGUNTAS-oficina.md; não houve override incompatível ou atualização automática de major.

O [advisory do parser](https://github.com/advisories/GHSA-pgww-w46g-26qg) relaciona o defeito ao ponto de integração HTML de annotation-xml; a [release1.5.0](https://github.com/AngleSharp/AngleSharp/releases/tag/1.5.0) registra a correção. Metadados consultados nos pacotes [9.0.967](https://www.nuget.org/packages/HtmlSanitizer/9.0.967) e [9.1.982](https://www.nuget.org/packages/HtmlSanitizer/9.1.982). A allowlist do Prisma, interfaces, DTOs, permissões e schema não mudaram. Migration nova:não.

## Verificação real

- Regressão do ponto de integração:duas codificações exigem namespace XHTML, como no navegador. Pacote antigo:2FAIL/2PASS; restaurado o pacote proposto:4PASS. Os controles preservam imagens, referências e conteúdo rico e removem atributos executáveis/URLs javascript. Nenhum HTML é executado pelo teste.
- Build da solução aprovado. xUnit completo:693PASS,33 skips preexistentes,0 falhas,55s, SQL de QA próprio.
- Auditoria NuGet final:cinco projetos sem pacotes vulneráveis nos feeds consultados, incluindo transitivos. O conteúdo foi conferido, além do exit0. A chamada inicial com solução relativa encontrou erro interno do SDK; caminho absoluto resolveu a invocação.
- Frontend completo:162 aprovados em41 arquivos, com tipos forçados, lint e build aprovados. O wrapper final usa somenteAPI3122/Vite3124 e banco próprio; tentativa anterior de startup em3128 foi abortada por porta ocupada, sem tocar no titular. Não houve redução de prazo,retry,cota ou asserção.

A hipótese inicial de teste era incorreta:esperava escape genérico de < e > em atributos OuterHtml, comportamento que falhou tanto no parser antigo quanto no novo. O [formatador oficial](https://github.com/AngleSharp/AngleSharp/blob/main/src/AngleSharp/Html/HtmlMarkupFormatter.cs) confirmou que esse não é o contrato. A investigação foi registrada e substituída pela condição concreta do advisory, sem mudar o produto para satisfazer uma hipótese inválida. Não se afirma corrigir o escape genérico.

## Autorrevisão e auto-QA

Autorrevisão parcial sem achados altos/médios confirmados; mudança de major transitiva é o principal risco de compatibilidade. O teste não demonstra exploração do Prisma anterior:os filtros já descartam vários elementos, e o resultado mede a correção do parser e a preservação do contrato de sanitização. Auditoria sem alertas é um resultado datado, não homologação de segurança geral.

Auto-QA do editor realizado nas duas larguras; resultados e limitações no fechamento abaixo. Nada em produção, main, merge de PR ou deploy. REVISAO e QA oficiais: pendentes.

## Atualização dos gates — 20/10/2026

A suíte frontend inicialmente teve161PASS/1FAIL: RichTaskDescriptionEditor já exibia a imagem enquanto a mensagem ainda era Loading. A corrida foi diagnosticada e corrigida somente no teste, no PR30, que passou a aguardar o mesmo feedback terminal. Contraprova específica retirou o feedback:1FAIL/6PASS; restauração7PASS. Cherry-pick da correção de teste nesta branch:8e04254. Novo frontend completo162PASS/41arquivos/0FAIL,192,13s. Componente de produto não foi alterado por essa correção.

E2E completo nesta branch117PASS/2FAIL/40skips existentes,11,2min. As duas falhas CLI têm causa no harness: Ai:BridgeToken ausente impede BridgeAsync antes da chamada ao provedor. O token privado foi configurado para o fake18747; repetição focal concluída com três aprovados em38,7 segundos, incluindo setup. O wrapper encerrou seus serviços próprios ao final do completo. Inspeção visual do editor nas duas larguras concluída, conforme fechamento. Backend693PASS/33skips e auditoria absoluta de cinco projetos sem pacotes vulneráveis permanecem válidos. O resultado completo anterior permanece preservado; fechamento local abaixo.


## Fechamento — 20/10/2026

O focal nuget-cli-auth-fixed aprovou três casos em38,7 segundos após corrigir exclusivamente a configuração privada de Ai:BridgeToken para o fake18747. O completo anterior permanece117 aprovados, duas falhas e40 skips existentes; não se apresenta a soma com o focal como nova execução integral verde.

Auto-QA específico nuget-security-editor-freeports: três aprovados em50,2 segundos. Exercitou edição/salvamento, tela cheia, Escape/foco, recusa de javascript:, imagem colada e persistência ao reabrir. A captura espera src blob, imagem completa e naturalWidth positivo. As imagens1440x900/390x844 foram abertas: texto e imagem real preservados, barra de ferramentas ajustada no mobile. Tipagem explícita dos scripts privados aprovada antes do reforço visual. A normalização de escapes sem diff anterior preservado não é apresentada como causa comprovada.

O log registra TaskCanceledException e HTTP500 em um PUT de tarefa após o caso mobile, durante encerramento do roteiro. O fato foi preservado, sem suprimir logs nem atribuir uma correção de cancelamento a este lote. A prova cobre persistência reaberta antes do encerramento; não certifica ausência de erros de servidor. Serviços temporários próprios encerrados pelo wrapper.

Autorrevisão final sem achado alto/médio confirmado no recorte. A mudança de major transitiva do parser continua risco de compatibilidade para revisão; allowlist, contratos e permissões não mudaram. Auditoria datada sem vulnerabilidades não equivale a homologação geral. Auto-QA específico concluído. Migration: não. REVISAO e QA oficiais: pendentes. PR34 permanece em rascunho.
