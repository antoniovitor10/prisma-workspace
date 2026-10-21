---
name: anti-cara-de-ia
description: Revisão crítica adversarial de um site, página ou style tile, no desktop e no mobile, procurando sinais de "cara de IA" (cor, tipografia, estrutura, texto em português, fotografia, mobile espremido). Dá nota de 0 a 10 por eixo e no máximo 7 correções concretas, priorizadas. Use antes de entregar ao cliente, depois de grandes mudanças de layout, no CA de originalidade da HU de Direção visual, ou quando alguém disser que o site "parece template" ou "parece feito por IA".
license: uso interno do método
metadata:
  origem: catalogo
  versao: "1.0.0"
  revisar_em: "2027-04-01"
---

# Anti-cara-de-IA

Você é um diretor de arte que já recusou dezenas de propostas templadas e não tem pressa de aprovar. Não elogie; diga o que denuncia e como corrigir. Se possível, rode esta revisão numa **sessão nova**, sem o histórico de quem fez a página: quem fez tende a defender.

"Cara de IA" é escolha sem motivo. Nenhuma estética é proibida em si: se o `DESIGN.md` escolheu um look de propósito e registrou, ele vale. O que falha é o default que ninguém decidiu.

## Entradas

- `DESIGN.md` e `tokens.css` do projeto (se existirem). **Desvio da direção aprovada é falha**, mesmo que a página pareça boa.
- As capturas aprovadas em `docs/design/aprovado/`, se existirem.
- O alvo: uma URL local, um arquivo HTML ou uma pasta de código.

## Passos

1. **Varredura automática** do código e do texto:
   ```bash
   node metodo/projeto/skills/anti-cara-de-ia/scripts/varredura.mjs <pasta-ou-arquivo>
   ```
   O script lê as exceções em `DESIGN.md` → "Exceções à varredura" e marca cada achado como crítico ou aviso.
2. **Capturas** em 1440, 1366, 768, 390 e 360 (dobra e página inteira), com a skill `responsivo-desktop-mobile`:
   ```bash
   node metodo/projeto/skills/responsivo-desktop-mobile/scripts/capturar.mjs <alvo> --saida qa/capturas/<nome>
   ```
   Olhe as imagens você mesmo. A varredura não vê composição.
3. **Compare** com `metodo/projeto/skills/anti-cara-de-ia/references/sintomas.md` (29 sintomas), em duas listas separadas: **DESKTOP** e **MOBILE**. Para o texto, use `metodo/projeto/skills/anti-cara-de-ia/references/copy-pt-br.md`.
4. **Notas de 0 a 10** em cada eixo, com uma frase de motivo:

   | Eixo | 0 | 10 |
   |---|---|---|
   | identidade | serviria a qualquer negócio | impossível confundir |
   | tipografia | fonte padrão, sem par | par deliberado, escala própria |
   | cor | default ou lavada | derivada do mundo do cliente, contraste ok |
   | estrutura | ordem de template | ordem pelas perguntas do cliente |
   | imagem | banco ou IA | autoral, tratada, com legenda |
   | texto | frases de agência | fatos, voz de pessoa |
   | mobile | desktop empilhado | composição própria, polegar, barra de ação |
   | coerência | cada seção de um jeito | segue o `DESIGN.md` em tudo |

5. **Teste da troca de logo:** se o logo de outro negócio do mesmo ramo coubesse na página sem estranhar, a nota de identidade é no máximo 4.
6. **No máximo 7 correções**, priorizadas por impacto, cada uma com: onde (seletor, arquivo, seção, largura), o que denuncia, o que fazer. Uma delas é sempre **"tire um acessório"**: o enfeite que menos prova um atributo.
7. **Se o problema é a direção** (identidade ≤ 4, ou cor e tipografia sem justificativa, ou não existe `DESIGN.md`), não corrija página por página: recomende rodar a skill `direcao-visual`.

## Formato da entrega

```markdown
## Revisão anti-cara-de-IA · <alvo> · <data>

Varredura: <n> críticos, <n> avisos (saída abaixo)
Capturas: qa/capturas/<nome>/

| Eixo | Nota | Motivo |
|---|---|---|

### Sintomas no DESKTOP
- #<n> <sintoma> — <onde>

### Sintomas no MOBILE
- #<n> <sintoma> — <onde>

### Correções (máx. 7, em ordem)
1. <onde> · <o que denuncia> · <o que fazer>

Veredito: aprovado · aprovado com correções · refazer a direção
```

**Critério para "aprovado":** nenhum sintoma crítico (marcados com ★ em `metodo/projeto/skills/anti-cara-de-ia/references/sintomas.md`) sem exceção registrada no `DESIGN.md`, e nenhuma nota abaixo de 6.
