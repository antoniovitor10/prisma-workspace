---
name: criar-skill-projeto
description: Cria ou atualiza uma skill específica deste projeto (stack, padrões, design system, deploy, regras do cliente, domínio) em metodo/projeto/skills, a partir do código e de perguntas ao desenvolvedor. Use no onboarding, quando o desenvolvedor pedir ou quando uma task propuser uma regra nova aprovada.
metadata:
  origem: metodo
  versao: "0.5.1"
---

# Criar uma skill do projeto

Skills do projeto guardam o que **só vale aqui**: o que um dev novo precisaria saber para não errar neste projeto. O método (como trabalhar) fica em `metodo/nucleo/`. Aqui fica o projeto (o que é verdade neste código e neste cliente).

## Conjunto inicial recomendado

Crie as que fazem sentido para o projeto. Nem todo projeto precisa de todas.

| Skill | Quando existe | Conteúdo |
|---|---|---|
| `stack` | sempre | linguagens, frameworks, versões, estrutura de pastas, como rodar, testar e fazer build |
| `testes` | sempre que há código testável | como os testes funcionam **neste** repositório: ferramentas e versões, onde ficam, como rodar tudo e um arquivo só, a ferramenta de interação (Playwright, Maestro, Detox ou sessão dirigida) e como subir o que ela precisa (servidor, emulador, app instalado), a convenção de identificadores (`testID`, `data-testid`), massa de dados e usuários de teste, como fazer dublê de API, testes lentos ou instáveis conhecidos; **com um exemplo real de cada tipo**. O "o que testar" é do método (`metodo/nucleo/referencias/testes.md`) |
| `padroes-codigo` | sempre | nomes, organização, estilo, padrões de componentes/serviços, o que evitar; **com exemplos reais do código** |
| `design-system` | interface com identidade já no código (projeto existente) | cores, tipografia, espaçamentos, componentes existentes, breakpoints, tom de voz, extraídos do código. **No projeto novo não é criada no onboarding:** nasce na HU-0001 pela skill `direcao-visual` (modelo em `metodo/projeto/skills/direcao-visual/assets/design-system.template.md`) |
| `dominio` | tem regras de negócio | entidades, regras, glossário estendido, fluxos do negócio |
| `dados` | tem banco | modelo de dados, migrações, convenções, dados sensíveis |
| `integracoes` | usa APIs externas | cada integração: para que serve, auth, limites, onde está no código, armadilhas |
| `deploy` | sempre que publica | ambientes, como publicar, rollback, o que verificar depois |
| `cliente` | projeto de cliente | preferências e restrições do cliente, quem aprova o quê, o que **nunca** fazer (se for curto, cabe em `contexto.md`) |
| `areas-sensiveis` | quando existe | arquivos ou sistemas que não se tocam sem autorização explícita |

## Passos

1. **Ler:** examine o código para extrair o que já é verdade (versões no `package.json`/`composer.json`, cores no CSS/tema, padrões em arquivos representativos). **Cite caminhos reais.**
2. **Perguntar:** o que o código não diz (preferências do cliente, regras de negócio, deploy), em rodadas, como na skill `entrevistar`.
3. **Escrever:** copie `metodo/nucleo/modelos/skill-projeto.md` para `metodo/projeto/skills/<nome>/SKILL.md` e preencha.
   - Frontmatter com `name`, `description` e `metadata` e, se precisar, os opcionais do padrão (`license`, `compatibility`, `allowed-tools`). Campos próprios (origem, versão, data de revisão) vão dentro de `metadata`.
   - `name`: igual ao nome da pasta, minúsculo, com hífens, sem acento, até 64 caracteres
   - `description`: **quando usar** a skill, numa frase específica. É o que faz o agente carregá-la na hora certa. Ex.: "Cores, tipografia e componentes visuais do site da Cooperar. Use em qualquer task que mexa em interface."
   - Corpo: regras curtas e verificáveis, com exemplos do próprio código. Nada de teoria genérica que qualquer modelo já sabe.
3b. **Skill de fora (GitHub, pacote de terceiros, outro projeto):** copie com a licença (`LICENSE`, se houver) e crie `SOURCE.md` na pasta da skill com: de onde veio (repositório e caminho), a revisão fixada (commit ou tag), a data, a licença e o que foi adaptado para este projeto. No frontmatter, `metadata.origem: externa`. Só traga o que o projeto usa: skill de frontend em projeto sem frontend, ou catálogo com megabytes de dados, gasta contexto em toda sessão e dispara fora de hora.
4. **Adaptadores e registro:** rode `node metodo/nucleo/scripts/gerar-adaptadores.mjs`. Ele gera os ponteiros e regenera a lista "Skills deste projeto" do `AGENTS.md` a partir da `description` (não edite a lista à mão).
5. **Mostrar ao desenvolvedor** para aprovação.

## Conformidade com o padrão Agent Skills

Toda skill segue https://agentskills.io (resumo em `metodo/nucleo/referencias/agent-skills.md`). Checklist antes de mostrar ao desenvolvedor:

- [ ] `name` só com `a-z`, `0-9` e `-`, até 64 caracteres, **igual ao nome da pasta**
- [ ] `description` com o que faz **e** quando usar, até 1024 caracteres, com as palavras que o pedido costuma ter
- [ ] frontmatter só com campos do padrão (`name`, `description`, `metadata`; opcionais `license`, `compatibility`, `allowed-tools`); campos próprios dentro de `metadata`
- [ ] `SKILL.md` com menos de 500 linhas; material longo em `references/<arquivo>.md`, linkado com caminho relativo, um nível só
- [ ] código auxiliar em `scripts/`, modelos e dados em `assets/`; nunca segredo em lugar nenhum
- [ ] `node metodo/nucleo/scripts/gerar-adaptadores.mjs` roda sem avisos para esta skill

## Atualizar a partir de uma task

Quando o relatório de uma task traz uma "Proposta para skills do projeto" e o desenvolvedor aprova: acrescente a regra na skill certa (ou crie uma), com o motivo e o ID da task de origem: `- Nunca usar vermelho em botões (cliente pediu; T-0012).`

## Regras

- Uma skill = um assunto. Até ~300 linhas; se passar, mova detalhe para `references/` dentro da pasta da skill e aponte.
- Conhecimento que envelhece (preço de API, versão de SDK) leva `metadata.revisar_em: AAAA-MM-DD`.
- Nenhum segredo (senha, token, chave) nas skills. Diga **onde** o segredo fica, nunca o valor.
- Regra sem fonte não entra: ou veio do código (cite o arquivo) ou do desenvolvedor.
