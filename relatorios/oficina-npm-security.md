# Oficina — TASK-502, manutenção de segurança npm

Base exata: a2797a7. Branch fix/npm-security-20261020, worktree própria oficina-npm-security. US-OPEN-SOURCE-001, SPEC-OPEN-SOURCE-DISTRIBUTION, D38 e D78 aprovadas. Entrega local com os resultados e a limitação histórica do harness descritos abaixo.

O lock anterior apresentou8 alertas altos e34 moderados. Pacotes Tiptap foram alinhados em3.32.0, React Router em7.18.4 e Vitest em4.1.11. Core foi declarado como dependência de desenvolvimento explícita da regressão, já existente transitivamente. Demais dependências diretas mantiveram suas versões resolvidas anteriores; nenhuma major direta mudou. Transitividades vulneráveis foram resolvidas dentro dos contratos compatíveis. Não se usou force,legacy-peer-deps ou audit fix major.

Duas causas de resolução foram diagnosticadas: o lock antigo preservava peers exatos Tiptap3.28; uma resolução sem lock no npm10.9.9 também reproduziu a exceção edgesOut do Arborist. npm11.19.1 foi usado de forma isolada via npx para gerar o conjunto coerente, sem atualizar a instalação global10.9.9. A primeira resolução ampla atualizou pacotes sem relação com os alertas; foi substituída por uma resolução restrita às famílias necessárias. Baseline e saídas intermediárias ficaram na própria.runlogs, sem instalações compartilhadas.

Verificação real até aqui:

- Regressão dos atributos do editor:2FAIL com bibliotecas anteriores,2PASS após instalação física própria. O teste confirma que um __proto__ vindo de JSON não torna atributos herdados executáveis na serialização DOM. Fragmento fica destacado, sem executar handler ou chamar rede.
- Instalação npm ci própria, tsc-b, lint e build passaram.
- Build da solução backend aprovado; xUnit completo:690PASS,33 skips preexistentes,0 falhas,56s. Bootstrap e seed executados só em banco próprio de QA; API temporária foi encerrada pelo próprio processo antes de liberar a CPU.
- Suíte completa Vitest:164PASS em42 arquivos,168s, zero falhas.
- Auditoria npm final:0 críticas,0 altas,0 moderadas,0 baixas. Restaurar manifest/lock anteriores reproduziu8 altas/34 moderadas e exit1; finally restaurou a proposta e a auditoria voltou a0/exit0. Nenhuma instalação alheia foi modificada.

A regressão acompanha o comportamento publicado no [advisory Tiptap](https://github.com/advisories/GHSA-cp6q-959q-f8rh). A avaliação do fornecedor observa que schemas fixos podem descartar atributos desconhecidos; não se afirma que todo alerta era diretamente explorável no Prisma. Também foram consultados os advisories de [parsing Markdown](https://github.com/advisories/GHSA-j95f-988m-3j2f), [paste do ProseMirror](https://github.com/advisories/GHSA-c8x8-7fp4-3x9w) e [mocker do Vitest](https://github.com/advisories/GHSA-82fw-gwwq-j7x9). Contexto do erro do resolvedor: [issue oficial npm/cli](https://github.com/npm/cli/issues/9787).

Autorrevisão parcial:diff limitado a manifest,lock,testes e documentação; majors e versões diretas fora do lote conferidas. Não houve mudança de API funcional,schema ou migration nova. Riscos: minor do editor e patches de navegação exigem o E2E real; auditoria zero é o resultado do catálogo npm consultado nessa data e não substitui a revisão. O alerta NuGet/AngleSharp é daTASK-503 e não é tratado neste lote.

Auto-QA do editor realizado nas duas larguras; resultados nativos finais abaixo. Backend não foi alterado. REVISAO e QA oficiais: pendentes.


## Fechamento — 20/10/2026

A suíte E2E completa anterior encerrou com 117 aprovados, duas falhas e 40 skips existentes. Ambas as falhas eram do caso CLI340, cujo harness não fornecia Ai:BridgeToken ao fake local. Após configurar o token privado e o provedor próprio em18747, o focal npm-cli-auth-fixed aprovou os três casos, incluindo setup, em44,4 segundos. Não se reescreve o resultado completo como se tivesse sido uma nova execução integral verde.

O auto-QA específico do editor aprovou três casos em52,5 segundos: edição, salvamento, tela cheia/Escape/foco, recusa de javascript:, imagem colada e persistência após reabrir. A primeira captura mobile ainda mostrava o placeholder durante hidratação assíncrona. O roteiro privado foi fortalecido para aguardar src blob, carregamento completo e naturalWidth positivo; nova execução npm-security-editor-hydrated: três aprovados em54,7 segundos. Capturas desktop1440x900 e mobile390x844 abertas, com texto e imagem real persistidos. A imagem anterior ficou preservada separadamente.

Tentativas anteriores do editor falharam no startup por porta3128 ocupada por processo não identificado como nosso. Wrappers próprios passaram a usar3122/3124 e foram encerrados ao terminar. A normalização dos escapes dos scripts privados não tem diff anterior preservado e não é apresentada como causa comprovada. Tipagem explícita dos scripts privados aprovada. Nenhum prazo, retry, cota ou asserção foi reduzido.

Autorrevisão final: sem achado alto/médio confirmado neste recorte; compatibilidade de editor/navegação continua sujeita à revisão oficial. Auditoria0 é o resultado datado do catálogo consultado, sem certificação geral. Auto-QA específico concluído. Migration: não. REVISAO e QA oficiais: pendentes. PR32 permanece em rascunho.
