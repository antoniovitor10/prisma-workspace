# O padrão Agent Skills

Fonte: https://agentskills.io (especificação aberta, criada pela Anthropic e mantida em https://github.com/agentskills/agentskills). Toda skill do método, do núcleo ou do projeto, segue este formato. Revisar esta página a cada versão do método: a lista de ferramentas muda todo mês.

Última revisão: 2026-10-14.

## O formato

Uma skill é uma pasta com um `SKILL.md`. Tudo o mais é opcional.

```
nome-da-skill/
├── SKILL.md          obrigatório: frontmatter + instruções
├── scripts/          opcional: código executável (Node, Python, Bash…)
├── references/       opcional: documentação longa, lida sob demanda
└── assets/           opcional: modelos, imagens, dados
```

### Frontmatter

| Campo | Obrigatório | Regra |
|---|---|---|
| `name` | sim | 1 a 64 caracteres; só `a-z`, `0-9` e `-`; não começa nem termina com `-`; sem `--`; **igual ao nome da pasta** |
| `description` | sim | 1 a 1024 caracteres; diz **o que faz** e **quando usar**, com as palavras que o pedido do desenvolvedor costuma ter |
| `license` | não | nome da licença ou do arquivo de licença |
| `compatibility` | não | até 500 caracteres; só se a skill exige ambiente específico (pacote, rede, produto) |
| `metadata` | não | mapa de texto → texto; é onde vão os campos próprios do método (`origem`, `versao`, `revisar_em`) |
| `allowed-tools` | não | experimental; suporte varia por ferramenta. O método não depende dele |

Qualquer outro campo no frontmatter é inválido no padrão. O que o método precisa guardar fica dentro de `metadata`.

### Corpo

Markdown livre. O que funciona: passos numerados, exemplos de entrada e saída, casos de borda, condição de parada explícita ("se X não existe, pare e pergunte").

Limites recomendados: **menos de 500 linhas** no `SKILL.md` (menos de 5.000 tokens). O que passar disso vai para `references/`, com link relativo a partir da raiz da skill (`references/REFERENCE.md`), **um nível de profundidade** só.

### Divulgação progressiva

1. **Descoberta:** ao iniciar, o agente carrega só `name` e `description` de todas as skills (~100 tokens cada).
2. **Ativação:** quando o pedido bate com a descrição, carrega o `SKILL.md` inteiro.
3. **Execução:** lê `references/`, roda `scripts/` ou usa `assets/` só quando a instrução manda.

Consequência prática: a `description` é o que decide se a skill é aberta. Modelos mais fracos subdisparam; por isso o `AGENTS.md` do método **roteia explicitamente** ("pediu bug → leia `depurar-bug`") em vez de confiar só na descoberta.

### Validação

`skills-ref validate ./minha-skill` (biblioteca de referência do padrão) confere frontmatter e nomes. O `gerar-adaptadores.mjs` do método faz a mesma conferência básica (nome, tamanho da descrição, pasta, 500 linhas) e avisa.

## Onde cada ferramenta procura skills no projeto

O padrão define o formato, não a pasta. Cada ferramenta tem a sua; o `gerar-adaptadores.mjs` gera ponteiros para as pastas listadas em `adaptadores:` do `config.yaml`. O conteúdo real fica sempre em `metodo/`.

| Ferramenta | Pasta no projeto | Chave em `adaptadores:` |
|---|---|---|
| Codex | `.agents/skills/` | `agents` |
| Claude Code | `.claude/skills/` (e subagentes em `.claude/agents/`) | `claude` |
| GitHub Copilot / VS Code | `.github/skills/`, `.claude/skills/`, `.agents/skills/` | `agents` basta |
| Cursor | `.agents/skills/`, `.cursor/skills/` | `agents` basta |
| Gemini CLI | `.gemini/skills/` ou `.agents/skills/` | `agents` basta |
| OpenCode | `.opencode/skills/`, `.claude/skills/`, `.agents/skills/` | `agents` basta |
| Kiro | `.kiro/skills/` | `kiro` |
| Outras | leem `AGENTS.md`, que aponta para `metodo/` | — |

Padrão do método: `adaptadores: [agents, claude]` cobre Codex, Claude Code, Copilot, Cursor, Gemini CLI e OpenCode. Acrescente `kiro`, `github`, `cursor`, `gemini` ou `opencode` só se a ferramenta não ler `.agents/`. Ferramentas que leem mais de uma pasta podem listar a mesma skill duas vezes; se incomodar, gere só `agents` e deixe `.claude/` fora do git.

## O que o padrão não cobre (e o método cobre)

- **Ordem, portões e autorização.** Skill diz como fazer uma coisa; não diz em que ordem, com que portão nem com autorização de quem. Isso está em `fluxo.md`, nos papéis e nos scripts.
- **Versão e dependência entre skills.** O conjunto é versionado por `metodo/nucleo/VERSAO` e pelo CHANGELOG; cada skill leva `metadata.versao`.
- **Segurança.** Skill é instrução executável. Skill de terceiro só entra depois de lida inteira. As suas vivem em `skills-reutilizaveis/`.
