# Oficina — TASK-502, manutenção de segurança npm

Base exata: a2797a7. Branch fix/npm-security-20261020, worktree própria oficina-npm-security. US-OPEN-SOURCE-001, SPEC-OPEN-SOURCE-DISTRIBUTION, D38 e D78 aprovadas. Checkpoint local; backend e E2E completos ainda pendentes, sem ENTREGA final.

O lock anterior apresentou8 alertas altos e34 moderados. Pacotes Tiptap foram alinhados em3.32.0, React Router em7.18.4 e Vitest em4.1.11. Core foi declarado como dependência de desenvolvimento explícita da regressão, já existente transitivamente. Demais dependências diretas mantiveram suas versões resolvidas anteriores; nenhuma major direta mudou. Transitividades vulneráveis foram resolvidas dentro dos contratos compatíveis. Não se usou force,legacy-peer-deps ou audit fix major.

Duas causas de resolução foram diagnosticadas: o lock antigo preservava peers exatos Tiptap3.28; uma resolução sem lock no npm10.9.9 também reproduziu a exceção edgesOut do Arborist. npm11.19.1 foi usado de forma isolada via npx para gerar o conjunto coerente, sem atualizar a instalação global10.9.9. A primeira resolução ampla atualizou pacotes sem relação com os alertas; foi substituída por uma resolução restrita às famílias necessárias. Baseline e saídas intermediárias ficaram na própria.runlogs, sem instalações compartilhadas.

Verificação real até aqui:

- Regressão dos atributos do editor:2FAIL com bibliotecas anteriores,2PASS após instalação física própria. O teste confirma que um __proto__ vindo de JSON não torna atributos herdados executáveis na serialização DOM. Fragmento fica destacado, sem executar handler ou chamar rede.
- Instalação npm ci própria, tsc-b, lint e build passaram.
- Suíte completa Vitest:164PASS em42 arquivos,168s, zero falhas.
- Auditoria npm final:0 críticas,0 altas,0 moderadas,0 baixas. Restaurar manifest/lock anteriores reproduziu8 altas/34 moderadas e exit1; finally restaurou a proposta e a auditoria voltou a0/exit0. Nenhuma instalação alheia foi modificada.

A regressão acompanha o comportamento publicado no [advisory Tiptap](https://github.com/advisories/GHSA-cp6q-959q-f8rh). A avaliação do fornecedor observa que schemas fixos podem descartar atributos desconhecidos; não se afirma que todo alerta era diretamente explorável no Prisma. Também foram consultados os advisories de [parsing Markdown](https://github.com/advisories/GHSA-j95f-988m-3j2f), [paste do ProseMirror](https://github.com/advisories/GHSA-c8x8-7fp4-3x9w) e [mocker do Vitest](https://github.com/advisories/GHSA-82fw-gwwq-j7x9). Contexto do erro do resolvedor: [issue oficial npm/cli](https://github.com/npm/cli/issues/9787).

Autorrevisão parcial:diff limitado a manifest,lock,testes e documentação; majors e versões diretas fora do lote conferidas. Não houve mudança de API funcional,schema ou migration nova. Riscos: minor do editor e patches de navegação exigem o E2E real; auditoria zero é o resultado do catálogo npm consultado nessa data e não substitui a revisão. O alerta NuGet/AngleSharp é daTASK-503 e não é tratado neste lote.

Auto-QA visual e E2E ainda pendentes. Backend não foi alterado, mas os gates da branch ainda serão registrados. Nada foi publicado ou enviado à produção. REVISAO e QA oficiais:pendentes.
