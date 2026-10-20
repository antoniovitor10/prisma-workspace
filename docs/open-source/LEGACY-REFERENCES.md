# Baseline de referências legadas

**Data:** 2026-09-05
**Escopo:** snapshot privado, excluindo dependências/builds e o catálogo JSON gerado de histórias.

| Referência | Arquivos |
|---|---:|
| `Prisma.Workspace` | 448 |
| `Detran` em qualquer capitalização | 460 |
| `Runrun` em qualquer capitalização | 28 |
| `nordevs.com.br` | 2 |
| IP da VPS atual | 1 |
| e-mail `@detran.se.gov.br` | 5 |

## Distribuição das referências Detran

- `src/`: 400 arquivos, predominantemente namespaces, projetos e imports técnicos.
- `tests/`: 20 arquivos.
- `specs/`: 20 arquivos, incluindo contexto e rastreabilidade histórica.
- `scripts/`: 6 arquivos.
- Demais ocorrências: documentação, solution, Dockerfile e governança.

## Regra de tratamento

1. Domínios, IPs, e-mails, credenciais demonstrativas e nomes institucionais na superfície/configuração devem chegar a zero.
2. `Prisma.Workspace` será migrado mecanicamente para `Prisma.Workspace` em lote dedicado, incluindo solution, pastas,
   projetos, namespaces, referências e Dockerfile.
3. Referências históricas em ADRs/specs não serão apagadas cegamente; devem ser anonimizadas ou mantidas somente
   quando necessárias para explicar uma decisão técnica, sem identificar pessoas ou dados operacionais.
4. O scanner será repetido depois de cada lote e antes da promoção pública.

## Gate

A existência dessas referências é tolerada apenas no repositório privado de preparação. O repositório não pode ser
tornado público enquanto domínios, IPs, e-mails e nomes institucionais de superfície não forem eliminados ou
formalmente justificados no inventário final.

## Conferência delimitada — 20/10/2026

Base a2797a7, revisão de preparação privada da Community Edition. A tabela de setembro acima é histórica,
não uma contagem atual. README e DbInitializer já usam identidade Prisma e dados de demonstração; não foi
feita substituição cega de dados ou migrations. O guia docs/installation/deployment.md passou a descrever
operação independente, sem caminhos, rede, host, container ou banco de uma instalação institucional.

As chaves de storage legadas em api.ts e PublicPortal.tsx são identificadores de compatibilidade, não nomes
visíveis da interface. Renomeá-las sem transporte de dados pode encerrar sessões ou perder acesso a protocolos;
foram preservadas nesta revisão. IDs/classes de migrations e decisões históricas também permanecem intactos,
conforme D76.

Os documentos server-dev.md, server-dev-validation.md, workspace/README.md, workspace/validation.md e os
inventários históricos ainda precisam de seleção/anonimização antes de qualquer snapshot público. Esse recorte
não certifica o histórico Git, a árvore inteira, ausência global de segredos ou liberação pública. Nenhum arquivo
.env, runtime, histórico de conversa ou ambiente institucional foi lido. A licença e os gates de publicação
continuam humanos e pendentes.
