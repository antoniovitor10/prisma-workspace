# Oficina — TASK-505: inventários e proveniência de CI

Base original:a2797a7. Branch ci/supply-chain-artifacts-20261020. Dependências próprias incorporadas por merge de branch:a9d2c8e(PR32/npm) e9677abd(PR34/NuGet), sem merge de PR ou alteração de main. Os conflitos de NOTAS/PROGRESS preservaram as duas contribuições. Spec de distribuição aprovada, D78 e história específica. Proposta técnica reversível registrada em DECISIONS antes da implementação; não aprova human gates.

## Alteração

O job container gera dois SPDX2.3:runtime da imagem local e inventário das entradas frontend. Anchore SBOM Action0.24.3 fixada no commit66cbf4bc1f1c0d2edc94016e65bc221b6bb0ad6c, Syft1.55.0 explícito. Upload de releases, artefatos paralelos e Dependency Snapshot desabilitados na action; o upload existente guarda os arquivos junto à candidata.

candidate-provenance.py registra quatro hashes(export,migrations,SBOMruntime,SBOMfrontend) e contexto público do CI, mantendo commits de código/workflow distintos. Recusa imagem cujo checksum não corresponde, arquivos ausentes/symlinks, inventários vazios e contexto inválido. O documento in-toto/SLSA v1 é explicitamente unsigned:sem assinatura, atestação da plataforma ou nívelSLSA. Permissões continuam contents:read; demais jobs, inclusive deploy, comparados e inalterados. Não há envio de imagem a registro nem publicação de release.

## Testes reais e pendências

- Dez testes Python aprovados. Contraprova desabilitou integridade do checksum e adicionou ambiente completo ao documento:2FAIL/8PASS; finally restaurou;10PASS no fechamento. Os testes cobrem artefato adulterado, identidade/contexto, vínculos por digest, JSON inválido, symlink e privacidade.
- YAML parseado; ordem dos passos, entradas da action fixada, flags de publicação, permissões e outros jobs comparados com o baseline.
- Syft oficial baixado somente na worktree, checksum conferido antes de extrair o binário. Smoke com fixture pequena identificou a dependência e produziu SPDX2.3. Primeiro seletor usava nome onde a CLI exige tag; diagnóstico explícito corrigiu apenas o harness para tag javascript. Não houve varredura de produção.
- Dependências npm/NuGet têm builds e testes próprios nos PR32/34; nenhum pacote extra de produto foi incluído para SBOM. CI integrado e geração sobre imagem real ainda pendentes no momento deste checkpoint. Nenhum workflow foi disparado manualmente; a criação do PR seguirá os eventos automáticos existentes.

## Autorrevisão e auto-QA

Autorrevisão parcial sem alto/médio confirmado. Limites:inventário frontend inclui dependências de desenvolvimento, materiais resolvidos são parciais e uma declaração unsigned não protege contra substituição de todo o artefato. Não se afirma reprodutibilidade bit a bit, ausência de vulnerabilidades, validação completa de schemas ou licença do Prisma. docs/maintenance/supply-chain.md descreve esses limites e o build type.

Auto-QA técnico pelos testes de integridade e privacidade acima; não há UI alterada. Backend/frontend/E2E não repetidos localmente para o recorte de CI sem impacto de comportamento; a integração completa será verificada no CI automático. Sem nova migration, produção, main, merge de PR, release ou deploy. REVISAO e QA oficiais:pendentes. Não é ENTREGA final antes do CI integrado.

## Fechamento do CI automático — 20/10/2026 11:14 UTC

O run 39409267216, referente ao head 1ec277a e ao merge sintético 6a761ba1890dbe3e3acea99277e9d0b39554b318, terminou com sucesso em Backend, Frontend, Context governance, Container build e E2E (SQL Server, desktop e mobile). Deploy produção: skipped. Nenhum workflow foi disparado manualmente.

O artefato próprio 12259116077 foi baixado e inspecionado: os quatro SHA-256 da declaração correspondem aos arquivos reais. SPDX runtime: 189 pacotes, incluindo HtmlSanitizer 9.1.982 e AngleSharp 1.7.0. SPDX frontend: 203 pacotes, incluindo @tiptap/core 3.32.0 e react-router 7.18.4. A declaração aponta para refs/pull/36/merge, distingue a execução e o código e permanece explicitamente unsigned. Isso confirma a produção dos inventários e vínculos de integridade; não equivale a assinatura, nível SLSA ou inventário completo de todas as entradas de build.

Este fechamento substitui a pendência de CI do checkpoint acima. Autorrevisão concluída para o recorte, sem achado alto ou médio confirmado. Auto-QA técnico concluído por contraprova local, inspeção do artefato real e CI automático. Não há UI própria neste item. Migration: não. REVISAO e QA oficiais: pendentes. PR continua em rascunho.

## Retificação de cobertura e complemento — 20/10/2026

A inspeção adicional dos203 pacotes frontend mostrou ausência de Vite/Vitest. A afirmação anterior de incluir ferramentas de desenvolvimento estava incorreta: Syft as exclui por padrão, conforme sua documentação oficial. O CI passou a definir explicitamente SYFT_JAVASCRIPT_INCLUDE_DEV_DEPENDENCIES=true somente na etapa frontend. O inventário de runtime não muda.

Contraprova local com o Syft1.55.0 oficial e uma fixture de lock: com flag false a ferramenta de desenvolvimento não aparece; com true aparece na versão esperada. Ambas as execuções terminaram0 e os oráculos de presença/ausência passaram. Não foram instalados pacotes nem consultado projeto de terceiros. YAML parseado e comparado com o head anterior: única diferença operacional é a variável da etapa frontend; demais jobs/permissões/deploy inalterados. Documentação primária: https://oss.anchore.com/docs/capabilities/javascript/ . O primeiro artefato continua válido para runtime e hashes, mas sua cobertura frontend de desenvolvimento era incompleta.

Este complemento reabre a confirmação de inventário frontend real: aguardar CI automático e conferir Vite/Vitest no novo artefato antes de considerar a cobertura final concluída. Não há novo gate aprovado, migration ou deploy. REVISAO e QA oficiais: pendentes.
