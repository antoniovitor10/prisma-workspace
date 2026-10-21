---
name: entrevistar
description: Fecha o escopo de qualquer pedido novo (funcionalidade, ajuste, bug) com o máximo de perguntas úteis antes de escrever a HU. Use sempre que o desenvolvedor pedir algo para ser feito, mesmo que pareça pequeno. Nunca implemente durante a entrevista.
metadata:
  origem: metodo
  versao: "0.4.1"
---

# Entrevistar

Objetivo: sair da conversa com **zero dúvidas que mudariam a implementação**. Na dúvida entre perguntar ou assumir, pergunte.

**Perguntar é o trabalho desta etapa, não um atraso.** Cada pergunta que você deixa de fazer vira uma suposição escondida no código, e o desenvolvedor só descobre no PR. O erro comum de agente é perguntar pouco e cedo demais dizer "fecha?". Se você está prestes a escrever "vou assumir que…", isso é uma pergunta.

## Passo 0: Classificar a trilha

Na primeira resposta, diga qual trilha o pedido parece ser (`fluxo.md`, "Trilhas por tamanho") e por quê:

- **micro** → 1 rodada de perguntas, 1 task avulsa, sem HU
- **bug** → pare aqui e siga `depurar-bug`
- **incidente** (produção quebrada agora) → pare aqui e siga `hotfix-producao`
- **funcionalidade** ou **épico** → continue nesta skill

O desenvolvedor pode mudar a trilha. Na dúvida, a menor.

## Passo 1: Ler antes de perguntar

Antes da primeira pergunta:

1. Leia `metodo/projeto/contexto.md` e as skills do projeto que tocam no assunto.
2. Procure no código o que o pedido afeta. Exemplo: para "botão no cabeçalho", ache o componente/template do cabeçalho, os botões existentes, as cores do tema e o comportamento no celular.
3. Procure HUs anteriores em `docs/trabalho/` sobre o mesmo assunto.

Separe o que encontrou em três listas (para você, não para o desenvolvedor):

- **Descoberto:** o código ou as skills respondem → vira pergunta de **confirmação**
- **Decisão:** só o desenvolvedor ou o cliente sabe → vira pergunta **aberta ou com opções**
- **Premissa:** detalhe pequeno com um padrão óbvio → listado no resumo final para o desenvolvedor aprovar de uma vez

## Passo 2: Perguntar em rodadas

### Mínimos (não negocie consigo mesmo)

| Trilha | Mínimo antes de propor o "fecha?" |
|---|---|
| micro | 1 rodada |
| funcionalidade | **3 rodadas**: desenho → consequências → riscos e aceite |
| épico | **4 rodadas** ou mais, e o PRD ainda terá as suas |

O mínimo é piso, não meta: continue enquanto houver dúvida que mude a implementação. Só feche antes do mínimo se o desenvolvedor pedir ("já tá bom, escreve") ou se todas as dimensões relevantes já estiverem respondidas; nesse caso, diga no resumo quais dimensões cobriu e por que não precisou de mais.

### Perguntas dinâmicas: cada rodada nasce da anterior

Não despeje um questionário fixo. Depois de cada resposta, pergunte-se: **"o que essa resposta abriu?"** e monte a próxima rodada com isso:

- **Consequência:** "Você escolheu abrir no app do WhatsApp. No desktop, onde não há app, abre o WhatsApp Web ou mostra o número?"
- **Caso de borda da escolha:** "Se o horário de atendimento acabou, o botão some, muda o texto ou continua igual?"
- **Conflito com o que existe:** "Isso cobre o banner do topo no celular. Empurra o banner ou fica por cima?"
- **Quem mais é afetado:** "Isso muda o que o atendente recebe? Ele precisa saber de qual página veio?"
- **Como vamos saber que funciona:** "Como o teste de interação confere isso? Precisa de usuário de teste, dado de exemplo, horário simulado?"

Em toda rodada a partir da segunda, **pelo menos metade das perguntas vem das respostas anteriores**, não do checklist.

### Rodada de riscos (obrigatória em funcionalidade e épico)

Antes do "fecha?", uma rodada só sobre o que pode dar errado: erro e falha de rede, dado ausente ou inválido, permissão (quem não pode), duplicidade, volume, dado pessoal e LGPD, o que acontece com o que já existe (migração, compatibilidade), e como cada critério será testado.

### Formato

- Rodadas de **até 4 perguntas**, das mais importantes (que mudam o desenho) para as de detalhe. A resposta de uma rodada molda a próxima.
- O agente **sempre propõe**: toda pergunta vem com opções e uma recomendada, e o desenvolvedor escolhe. Exemplo: "Cor: (a) azul primário `#1A4D8F`, como os outros botões, recomendado; (b) verde WhatsApp `#25D366`; (c) outra".
- Se a ferramenta tiver perguntas interativas de múltipla escolha, use-as (uma rodada = uma chamada). Se não, numere as perguntas para o desenvolvedor responder por número.
- **Opções específicas deste projeto**, tiradas do código e das skills, nunca genéricas. Ruim: "(a) azul (b) verde". Bom: "(a) `--cor-primaria` `#1A4D8F`, como os botões do formulário de contato (recomendado: mantém o padrão) (b) verde WhatsApp `#25D366` (c) outra".
- **Toda recomendada diz o porquê** em meia linha.
- Sempre há uma saída "outra / explico" para o desenvolvedor não ficar preso às suas opções.
- Confirmações podem ir agrupadas: "Achei no código: X, Y, Z. Confirma?" Elas **não contam** para o mínimo de rodadas.
- Não pergunte o que já foi respondido nem o que o código responde com certeza absoluta.

### Antes de propor o "fecha?"

Passe pelo checklist abaixo e, para cada dimensão que o pedido toca, verifique: **respondida** ou **premissa listada para aprovação**. Se alguma está nos dois "não", faça outra rodada. Mostre essa cobertura no resumo (uma linha: "Cobri: objetivo, conteúdo, comportamento, estados, responsivo, dados, LGPD, aceite. Não se aplica: integrações, SEO.").

### O que não fazer

- Fechar depois de uma rodada numa funcionalidade.
- Perguntas que só confirmam o que você já decidiu ("pode ser azul, ok?").
- Opções genéricas que servem para qualquer projeto.
- Esconder decisão no meio de "premissas" para não perguntar. Premissa é só detalhe com padrão óbvio.
- Perguntar tudo de uma vez num bloco de 15 itens: a resposta de cada rodada muda a seguinte.

### Checklist de dimensões

**Núcleo (sempre):** objetivo, conteúdo, comportamento, responsivo, fora do escopo e aceite.
**O resto:** só quando o pedido toca no assunto. Um pedido pequeno costuma fechar em 1 ou 2 rodadas.

| Dimensão | Exemplos de pergunta |
|---|---|
| **Objetivo** | Por que isso? Que problema resolve? Como saber que deu certo? |
| **Quem usa** | Que tipo de usuário? Logado? Em que situação? |
| **Conteúdo** | Textos exatos, rótulos, imagens, ícones |
| **Visual** | Cor, tamanho, posição, estilo; segue o design system? |
| **Comportamento** | O que acontece ao clicar? Abre onde (mesma aba, nova aba, app)? |
| **Estados** | normal, hover, foco, carregando, erro, vazio, desabilitado |
| **Condições** | Aparece sempre? Em que páginas, horários, perfis? |
| **Responsivo** | Celular, tablet, desktop: muda algo? |
| **Dados** | De onde vem? Para onde vai? Precisa salvar? |
| **Integrações** | API externa, webhook, e-mail, WhatsApp, pagamento |
| **Regras de negócio** | Limites, validações, cálculos, exceções |
| **Casos de borda** | Texto muito longo, sem internet, duplo clique, dados ausentes |
| **Acessibilidade** | Rótulo para leitor de tela, contraste, teclado |
| **SEO / analytics** | Rastrear clique? Evento no GA/Pixel? (sites) |
| **Segurança / LGPD** | Coleta dado pessoal? Quem pode ver? Precisa de consentimento? |
| **Desempenho** | Volume esperado, tempo de resposta |
| **Fora do escopo** | O que, explicitamente, **não** faz parte? |
| **Aceite** | Quem aprova? Precisa de validação do cliente? |

### Exemplo: "Preciso colocar um botão no cabeçalho"

> Li o `header.php` e o `style.css`. Rodada 1:
>
> 1. **Objetivo:** é para contato (WhatsApp), agendamento ou outra coisa?
> 2. **Texto:** qual o texto exato? Sugestão: "Agendar consulta".
> 3. **Destino:** link para onde? Abre em nova aba?
> 4. **Cor:** os botões do site usam o azul `#1A4D8F`. Segue esse padrão (recomendado) ou outra cor?
> 5. **Celular:** hoje o cabeçalho vira menu hambúrguer abaixo de 768px. O botão fica visível fora do menu (recomendado para conversão), dentro do menu ou some?
>
> (Rodada 2, conforme as respostas: aparece em todas as páginas? ícone? hover? rastrear clique no GA?)

## Passo 3: Fechar

Quando não restar dúvida que mude a implementação, mostre o **resumo do escopo**:

```
## Escopo: <título>
Objetivo: …
Inclui: …
Não inclui: …
Critérios de aceite (rascunho):
  - Dado … quando … então …
Como vamos testar: <o teste de interação de cada CA com interface; dados de teste>
Cobertura: cobri … · não se aplica …
Premissas (assumi; corrija se errado):
  - …
Tamanho estimado: pequeno (1 task) | médio (2–4 tasks) | grande (PRD + várias tasks)
```

Pergunte: **"Fecha? Posso escrever a HU?"** (trilha micro: **"Fecha? Crio a task?"**)

- "Sim" → siga a skill `escrever-hu` (trilha micro: crie a task avulsa como diz `fluxo.md`, "Como criar uma task avulsa", com `trilha: micro`, e peça autorização).
- Correções → ajuste e mostre o resumo de novo.

## Regras

- **Nunca** implemente, crie branch ou edite código durante a entrevista.
- Se o desenvolvedor disser "não sei" ou "decide você", registre como premissa com a sua recomendação e destaque no resumo.
- Se uma resposta depende do cliente, registre como **dúvida do cliente** e pergunte se a HU espera ou segue com uma premissa provisória.
