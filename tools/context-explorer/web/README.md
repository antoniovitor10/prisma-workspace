# Context Explorer Web

Interface React/Vite para revisar o estado do projeto e compreender sua arquitetura AI-Native a partir do `model.json` produzido pelo scanner.

## Executar

```bash
cd tools/context-explorer/web
npm install
npm run dev
```

Abra `http://localhost:5174`. Durante o desenvolvimento, a tela tenta regenerar e buscar o modelo pela API local (`/api/model`). Se a API estiver indisponível, usa o snapshot copiado de `../model.json` para `public/model.json`. O topo informa claramente qual fonte está ativa.

## Gerar o build estático

```bash
npm run build
```

O resultado fica em `dist/`. Para conferir o build localmente, use `npm run preview`.

## Organização

- **Visão geral:** métricas objetivas, fluxo de trabalho e próximas tarefas.
- **Histórias do produto:** fonte funcional escrita antes da solução técnica; o catálogo canônico vive em `stories/catalog.json`.
- **Especificações:** contratos gerados ou revisados pela IA a partir das histórias; somente a spec recebe aprovação G-SPEC.
- **Trabalho dos agentes:** tarefas técnicas por prioridade e estado; servem para acompanhamento e são executadas pelos agentes.
- **Homologação:** PO confere o produto pelas histórias sem aprová-las individualmente. Com a API local ativa, resultados e observações ficam em `.agent-state/manual-validation.json`; observações, ajustes e itens não implementados criam tarefas rastreáveis em `.agent-state/story-tasks.json`. Sem a API, permanecem como rascunho no navegador.
- **Lacunas e decisões:** separa lacunas funcionais declaradas nas specs de alertas técnicos produzidos pelo scanner.
- **Aprovações:** mostra primeiro apenas specs realmente pendentes; decisões especiais de banco, workflow, histórico, escopo ou deploy ficam recolhidas até um agente solicitar o gate.
- **Processos:** dois mapas em React Flow inspirados em BPMN/Bizagi — o desenvolvimento story-first com triagem/retorno e a operação do produto.
- **Arquitetura de IA:** visão integrada, contexto, motor, ciclo de correção, agentes e rastreabilidade em uma área técnica separada.

O Inspetor lateral abre detalhes ao clicar em specs, tarefas, lacunas, etapas dos processos ou entidades dos grafos.
