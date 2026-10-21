# Testes no método

Referência para o planejador, o testador e o revisor de código. A skill de projeto `testes` diz **como** isso funciona neste repositório (ferramentas, pastas, comandos); este arquivo diz **o que** precisa acontecer em qualquer projeto.

## Regra principal

**Todo critério de aceite com interface tem um teste de interação**: um teste que simula o usuário (abre a tela, clica ou toca, digita, rola) e confere o que ele vê. Os outros tipos (unit, integração) são bem-vindos, mas não substituem este.

Projeto sem interface (API, biblioteca, rotina): o equivalente é o teste de **integração** que chama a rota ou a função pública de verdade, com a entrada e a saída que o cliente da API veria.

## Quando os testes são escritos

**Depois da implementação**, pelo papel testador, a partir da tabela "Testes" da task. O planejador decide **o que** testar (na task); o testador escreve, roda e prova.

## Tipos

| Tipo | O que é | Exemplo |
|---|---|---|
| `interacao` | simula o usuário na interface | abre "Minha CNH", toca em "Renovar", vê o aviso de prazo |
| `integracao` | chama a rota, o banco ou o serviço de verdade (ou um dublê fiel do contrato) | `GET /veiculos/{placa}` devolve 404 com placa inexistente |
| `unit` | uma função ou componente isolado | `formatarPlaca("ABC1D23")` devolve `"ABC-1D23"` |
| `manual` | ninguém consegue automatizar agora; **sempre com motivo** | iOS sem Mac disponível |

Um CA pode ter mais de um tipo. `manual` nunca é o único tipo de um CA com interface, a não ser que o desenvolvedor aceite e o motivo fique escrito.

## Local, simulado ou real

Diga sempre **onde** o teste rodou. São coisas diferentes:

| Ambiente | Prova | Não prova |
|---|---|---|
| local com dublê (mock, stub, servidor falso) | a lógica do nosso código | que a integração externa funciona |
| local com o serviço de verdade (banco local, API de homologação) | a integração naquele ambiente | produção |
| homologação ou produção | o comportamento real | — |

- **Assert em dublê não prova integração.** Se o CA fala de um serviço externo (API do parceiro, gateway, banco), um teste com dublê vale como `unit` ou `integracao` local, e o relatório diz "integração real não verificada".
- **Teste que existe não é teste que rodou.** Só conta o que tem a saída do comando no relatório. "Já existe teste para isso" sem a saída é "não verificado".
- Resultados possíveis por CA: ✅ passou · ❌ falhou · ⚠️ não verificado (com o motivo: sem ambiente, sem acesso, sem aparelho) · — não se aplica (com o porquê). "Não verificado" nunca vira aprovado sem o "sim" do desenvolvedor.

## Ferramenta de interação

Fica em `config.yaml` → `testes.interacao`. O onboarding descobre o que o projeto já usa; se não houver nada, propõe e **pergunta antes de instalar**.

| Projeto | Preferida | Alternativas |
|---|---|---|
| web (site, sistema no navegador) | Playwright | Cypress, se já estiver no projeto |
| app React Native, Flutter, nativo | Maestro (fluxos em YAML, Android e iOS) | Detox, Appium |
| desktop | a do framework (ex.: Playwright para Electron) | automação do sistema operacional |
| sem ferramenta instalada ainda | `agente`: sessão dirigida pelo agente | ver abaixo |

**Teste versionado vence sessão dirigida.** O teste em arquivo (Playwright, Maestro) fica no repositório e roda de novo em toda task: vira regressão. A sessão dirigida (o agente operando a interface por um orquestrador como Batuta ou Maestri, pelo navegador do agente, por automação do computador ou por `adb`) verifica só aquela vez.

Com `ferramenta: agente`:
- o testador opera a interface, passo a passo, como o teste faria, e guarda a evidência (capturas numeradas ou gravação, com o passo de cada uma);
- o relatório marca o CA como "verificado por sessão dirigida";
- a seção "Pendências" da task registra "automatizar o teste de interação de CA-n" até o projeto ter a ferramenta.

## Seletores estáveis

Teste de interação que procura elemento por texto ou posição quebra a cada ajuste de layout. Use o identificador do projeto (`testID` no React Native, `data-testid` na web, ou o nome acessível do elemento). Se o elemento não tem identificador, o implementador acrescenta; a convenção fica na skill `testes`.

## Prova de que o teste pega

Teste que passa com o código certo pode passar também com o código errado. Com tudo verde, para cada CA com teste automatizado:

1. quebre o comportamento de propósito (inverta a condição, troque o valor, comente a chamada);
2. rode só aquele teste e confirme que **falha** pelo motivo certo;
3. desfaça a quebra e rode de novo (verde).

Registre na tabela do relatório ("pega: sim"). Se o teste continuou verde com o código quebrado, o teste não vale: reescreva antes de concluir.

## Qualidade do teste (checklist do revisor)

- Testa **comportamento** (o que o usuário ou o cliente da API vê), não detalhe interno.
- Tem asserção real. Teste sem `expect`, ou que só confere que "renderizou", não conta.
- Não faz dublê do próprio alvo. Dublê só nas bordas (API externa, relógio, aleatório).
- O nome do teste diz o CA: `CA-2: mostra aviso quando a CNH vence em menos de 30 dias`.
- Não depende de ordem nem de dado deixado por outro teste.
- Não usa espera fixa (`sleep`); espera pelo elemento ou pelo estado.
- Snapshot só onde a forma importa, e nunca como única asserção de um CA.

## Teste instável (flaky)

Se um teste passa e falha sem mudança no código: rode 3 vezes. Instável e **antigo** vai para `linha_de_base` com a nota "instável"; instável e **novo** (criado nesta task) é falha da task.

## Roteiro de homologação

Quando a task vai para `em-revisao`, o testador escreve na task o roteiro para quem homologa (status `em-teste`), a partir dos CA:

```
### Roteiro de homologação · T-XXXX
Ambiente: <homologação / build de teste, versão>
Aparelhos ou telas: <Android, iOS, claro e escuro, 390 px…>
Dados: <usuário de teste, placa, CNH de exemplo; nunca dado real de cidadão>

| # | Passos | Esperado | CA | Resultado |
|---|---|---|---|---|
| 1 | abrir "Minha CNH" e tocar em "Renovar" | aviso de prazo aparece | CA-2 | ☐ ok ☐ falhou |

Quem testou: ____ · Data: ____
```

Quem homologa marca o resultado na própria task. Falha na homologação volta como ajuste da mesma task (ou bug novo, se for fora do escopo).
