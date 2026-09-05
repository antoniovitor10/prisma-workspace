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
