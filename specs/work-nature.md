# SPEC-WORK-NATURE: Natureza da estrutura de trabalho

**Status:** approved

**Origem:** `US-WORK-NATURE-001`, registrada por instrução direta do PO em 2026-09-03.

## Objetivo

Permitir que todo projeto seja classificado explicitamente por Natureza e Tipo de Trabalho, tornando a organização
operacional compreensível sem criar uma entidade central de Demanda e sem usar IA.

## Escopo proposto

- Exibir as escolhas de Natureza e Tipo de Trabalho na criação e edição do projeto.
- Persistir exatamente uma natureza e um tipo por projeto novo.
- Apresentar os rótulos funcionais `Projeto`, `Melhoria` e `Sustentação`.
- Apresentar os tipos `Desenvolvimento`, `Infraestrutura`, `Banco de Dados`, `Suporte`, `Segurança`, `Dados/BI`,
  `Integração`, `Documentação` e `Gestão`.
- Exibir ambas as classificações nos cards, busca/filtros e configurações do projeto.
- Preservar tenant, autorizações, histórico e estruturas existentes.

## Semântica das naturezas

- **Projeto:** iniciativa temporária, com início, fim e entrega definida, como a implantação de um sistema.
- **Melhoria:** evolução incremental ou melhoria contínua de algo existente, como um novo relatório ou ajuste.
- **Sustentação:** trabalho operacional de suporte, manutenção, correção ou atendimento, como um erro em produção.

## Fora do escopo

- Inferência, recomendação, geração de etapas ou qualquer outra função de IA.
- Etapas fixas ou automaticamente criadas de acordo com a natureza.
- Alterar a natureza dos tipos de item Épico, Bug, Feature, Product Backlog Item e Tarefa.
- Renomear `Solicitações` para `Demandas` ou criar uma entidade/tabela `Demand`.
- Automatizar etapas, quadros, workflow ou SLA com base na classificação.

## Requisitos funcionais propostos

1. A criação não pode ser concluída sem selecionar uma das três naturezas e um dos nove tipos.
2. A API e o domínio validam os dois catálogos fechados e rejeitam valores desconhecidos ou não classificados.
3. A classificação pertence ao `Project` e respeita o tenant ativo.
4. Administrador ou Gestor autorizado pode alterar Natureza e Tipo nas configurações já protegidas do projeto.
5. Alterações posteriores são registradas no `ProjectEvent` administrativo com valores anterior e novo.
6. Registros existentes permanecem como `Não classificado` até edição explícita, evitando backfill semanticamente falso.
7. Novos projetos nunca podem ser persistidos como `Não classificado`.
8. Busca e filtros da listagem funcionam pelos dois campos, e os cards apresentam seus rótulos.

## Impacto técnico esperado

- Domínio e DTOs: enums `WorkNature` e `WorkType` no `Project`.
- SQL Server/EF Core: duas colunas inteiras não nulas; valor técnico `0` representa somente legado não classificado.
- Frontend: seleção obrigatória na criação, edição em configurações e apresentação/filtro na listagem.
- Testes: domínio, API, persistência, autorização e E2E de criação para as três naturezas.

## Human Gates

- `G-SCOPE`: atendido pela inclusão explícita no escopo pelo PO em 2026-09-03.
- `G-SPEC`: aprovado pelo PO em 2026-09-04 ao escolher explicitamente este modelo e determinar a implementação.
- `G-MIGRATION`: aprovado pelo PO em 2026-09-04 na determinação de implementar, publicar e validar até concluir.
- `G-WORKFLOW`: não aplicável; não há automação ou mudança de transição.
- `G-DEPLOY`: aprovado explicitamente pelo PO em 2026-09-04.

## Rastreabilidade

US-WORK-NATURE-001 → SPEC-WORK-NATURE → TASK-040 → G-SPEC/G-MIGRATION → implementação → testes → homologação

Implementação publicada em produção em 2026-09-04, com migration aplicada e validação Chrome desktop/mobile concluída.
