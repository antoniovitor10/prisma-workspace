# Inventário e proveniência dos artefatos de CI

Como pessoa que revisa uma candidata Community, quero receber inventários de dependências e uma declaração de origem vinculada aos hashes dos arquivos, para conferir qual código e execução produziram a candidata antes de qualquer publicação.

Origem:TASK-505, US-OPEN-SOURCE-001, SPEC-OPEN-SOURCE-DISTRIBUTION aprovada e D78. SBOMs SPDX para runtime e entradas frontend; registro in-toto/SLSA v1 unsigned com hashes e contextoCI. Não equivale a assinatura, nívelSLSA ou aprovação. Preservar permissões mínimas, checksums e pipeline de testes; não alterar o job de promoção, licença ou gates humanos. Dependências de integração:manutenção npm/NuGet em branches próprias.
