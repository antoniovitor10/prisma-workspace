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
