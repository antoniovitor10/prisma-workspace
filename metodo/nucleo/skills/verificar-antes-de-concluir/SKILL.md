---
name: verificar-antes-de-concluir
description: Impede que uma task seja dada como pronta sem evidência. Use antes de declarar qualquer task concluída, antes de commitar como final, antes de abrir PR/MR e antes de mover o status para em-revisao.
metadata:
  origem: metodo
  versao: "0.5.1"
---

# Verificar antes de concluir

Regra: **evidência antes de afirmação**. "Deve funcionar", "parece certo" e "não tem como quebrar" não são evidência. A saída real de um comando é.

## Passos

1. **Rode os comandos** de `config.yaml` que existirem: `lint`, `testar`, `e2e`, `build`. Cole a saída real (as últimas linhas relevantes) no relatório da task. Se um comando não existe, escreva "não existe neste projeto".
   - **Compare com a `linha_de_base`** do `config.yaml`. Falha que já estava lá não bloqueia a task. Falha **nova**, ou falha antiga que piorou (mais testes quebrados no mesmo arquivo, mais erros de lint), bloqueia. Se a task fez uma falha da linha de base sumir, diga isso e proponha tirá-la de lá.
   - Se `lint_escopo` diz "só arquivos alterados", rode o lint só nos arquivos do diff.
2. **Confira cada CA** da seção "Critérios de aceite" da task (em task com HU, são os de `cobre:`), um por um, com o tipo de teste da tabela "Testes" e **como** conferiu:

   | CA | Tipo | Teste / como verifiquei | Pega? | Resultado |
   |---|---|---|---|---|
   | CA-1 | interação | `e2e/contato.spec.ts` · "CA-1: envia o formulário" → passed | sim | ✅ |
   | CA-2 | unit | `src/utils/telefone.test.ts` → 4 passed | sim | ✅ |
   | CA-3 | manual | não consegui verificar: precisa da chave do GA | — | ⚠️ não verificado |

   - Diga **onde** cada teste rodou (local com dublê, local com serviço real, homologação). Assert em dublê não prova integração externa; teste que existe e não rodou nesta task é "não verificado" (`metodo/nucleo/referencias/testes.md`, "Local, simulado ou real").
   - **CA com interface sem teste de interação = task não concluída.** A exceção é o desenvolvedor aceitar, com o motivo escrito no relatório. Sem ferramenta instalada (`testes.interacao.ferramenta: agente`), vale a sessão dirigida com evidência, e a automação fica como pendência.
   - **Prova de que o teste pega:** para cada teste automatizado de CA, quebre o comportamento de propósito, confirme que o teste falha e desfaça (`metodo/nucleo/referencias/testes.md`). Teste que continua verde com o código quebrado não vale.

3. **Confira o escopo:** o diff toca só no que a task pede? Liste qualquer arquivo fora do esperado e justifique.
4. **Confira as regras escritas** (só o que o diff encosta, para não gastar contexto à toa): `principios.md` inteiro (é curto) e as skills do projeto cujo assunto ou caminhos aparecem nos arquivos alterados. Se o que foi feito contradiz uma regra escrita, **não conclua**: siga "Contradição com regra escrita" em `executar-task`.
5. **Confira o que não pode estar lá:**
   - **Segredos:** rode `node metodo/nucleo/scripts/segredos.mjs` e cole a saída. Achado bloqueia até sair do código ou ser explicado como falso positivo.
   - **Dado pessoal real** em teste, fixture, log ou exemplo (a massa de teste é sempre sintética).
   - `console.log` esquecido, dependência nova sem ADR, TODO sem task.
6. **Se algo falhou ou não foi verificado:** não conclua. Volte para o implementador (se é ajuste) ou registre como bloqueio (se precisa do desenvolvedor). Um ⚠️ só passa se o desenvolvedor aceitar explicitamente.
7. Só então diga "pronta para revisão" e siga a entrega conforme a autonomia.

## Regras

- Nunca edite um CA para fazê-lo passar. Se o CA está errado, é bloqueio.
- Nunca pule esta skill porque "a mudança é pequena". Mudança pequena verifica rápido.
- Nunca apague nem enfraqueça um teste existente para a task passar. Se o teste antigo está errado, é bloqueio.
- Se os testes já estavam quebrados antes da task e isso não está na `linha_de_base`, diga, mostre a saída de antes e de depois e proponha registrar na linha de base.
