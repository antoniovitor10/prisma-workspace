---
id: ADR-XXXX
titulo: "Direção visual: rota <nome>"
status: aceita
data: AAAA-MM-DD
origem: HU-XXXX
substitui: ""
---

# ADR-XXXX · Direção visual: rota <nome>

## Contexto

<Resumo do brief (docs/design/brief-visual.md): sujeito, público, tarefa única da página, atributos em tensão, restrições (marca, conselho, fotos disponíveis).>

## Decisão

Adotar a rota **<nome>**, registrada em `DESIGN.md` e em `<caminho>/tokens.css`, com a skill de projeto `design-system`.

## Alternativas consideradas

| Rota | Atributo que mais provava | Por que não foi escolhida | Capturas |
|---|---|---|---|
| A · <nome> | | | `docs/design/rotas/capturas/rota-a/` |
| B · <nome> | | | `docs/design/rotas/capturas/rota-b/` |
| C · <nome> | | | `docs/design/rotas/capturas/rota-c/` |

Mistura de eixos, se houve: <qual eixo de qual rota, e por quê não virou "Franken-comp">.

## Consequências

- Fica proibido: <defaults que esta rota exclui, cores e fontes fora dos tokens>.
- Toda página nova começa com uma seção de design que cita o `DESIGN.md`.
- Revisão: a direção só muda com um novo ADR que substitua este, feito com a skill `direcao-visual`.
- O que muda em `principios.md` ou nas skills do projeto: <ex.: "Nenhum hex fora de tokens.css" vira princípio>.
