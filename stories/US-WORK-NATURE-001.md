# US-WORK-NATURE-001: Classificar o trabalho por natureza

**Status:** active
**Módulo:** projetos
**Spec vinculada:** `SPEC-WORK-NATURE`

## História

Como pessoa responsável por organizar o trabalho, quero informar a natureza e o tipo de trabalho ao criar ou editar
um projeto para separar iniciativas temporárias, melhorias contínuas e sustentação operacional sem depender de IA.

## Cenário principal

- **Dado** que uma pessoa autorizada inicia a criação de uma estrutura de trabalho;
- **Quando** informa os dados necessários;
- **Então** escolhe exatamente uma natureza entre `Projeto`, `Melhoria` e `Sustentação`;
- **E** escolhe um tipo de trabalho predefinido;
- **E** pode configurar as subdivisões ou etapas internas adequadas àquela estrutura.

## Tipos de trabalho predefinidos

`Desenvolvimento`, `Infraestrutura`, `Banco de Dados`, `Suporte`, `Segurança`, `Dados/BI`, `Integração`,
`Documentação` e `Gestão`.

## Exemplos fornecidos pelo PO

- `Projeto`: Implantação do Sistema X → Levantamento, Desenvolvimento, Testes e Implantação.
- `Melhoria`: Criar novo relatório → Análise e Desenvolvimento.
- `Sustentação`: Erro no relatório → Análise, Correção e Homologação.

## Limites explícitos

- A classificação é escolhida pela pessoa usuária; não existe inferência, sugestão ou geração por IA.
- As etapas acima são exemplos, não modelos obrigatórios nem listas fechadas.

## Origem

Necessidade e exemplos fornecidos diretamente pelo PO em 2026-09-03.
Em 2026-09-04, após comparar o modelo central de Demandas com a classificação por projeto, o PO escolheu
explicitamente manter Natureza e Tipo de Trabalho no `Project` e determinou sua implementação até produção.
