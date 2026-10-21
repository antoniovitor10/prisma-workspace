# SPEC-ATTACHMENTS: Anexos

**Status:** approved

**Revisão funcional:** aprovada por PO durante a revisão módulo a módulo em 2026-08-24

**Homologação manual:** pendente

**Natureza:** contrato funcional desejado comparado ao estado atual do produto

## Propósito

Definir o comportamento oficial do módulo de anexos de itens de trabalho e registrar, sem mascarar divergências, o que já está comprovado no código e o que ainda precisa ser implementado ou homologado.

## Contexto

O sistema já possui a entidade `Attachment`, endpoints e casos de uso para listar, enviar e baixar arquivos, armazenamento local por `LocalFileStorage`, metadados no SQL Server e interface na aba **Anexos** do detalhe da tarefa. PO definiu nesta revisão o contrato funcional definitivo do módulo. A aprovação desta spec confirma o requisito; não comprova que o produto atual já o atende integralmente.

## Escopo incluído

- Anexos vinculados a itens de trabalho.
- Envio, listagem, visualização e download.
- Exclusão com confirmação e retenção temporária em lixeira.
- Permissões por operação.
- Limites de tamanho e quantidade.
- Registro imutável de envio e exclusão.
- Armazenamento físico e metadados.
- Homologação manual dos fluxos funcionais.

## Escopo excluído

- Anexos da Wiki, que possuem fluxo próprio.
- Regras específicas dos anexos do Portal Externo, que continuam subordinadas às D27 e D28.
- Pré-visualização avançada, versionamento de arquivos e armazenamento em nuvem.
- Verificação antimalware na primeira versão deste contrato.

## Contrato funcional aprovado

### Tipos, tamanho e quantidade

- O módulo deve aceitar qualquer tipo de arquivo.
- Cada arquivo deve possuir no máximo **10 MB**.
- Não existe limite de quantidade de anexos por tarefa.
- Arquivos com o mesmo nome devem ser mantidos como registros separados; um upload não substitui o anterior.

### Permissões

- Podem enviar anexos:
  - participantes da tarefa;
  - administradores.
- Podem visualizar e baixar anexos:
  - qualquer usuário da organização à qual a tarefa pertence.
- Podem excluir anexos:
  - o autor do envio;
  - administradores.
- Usuários anônimos não podem acessar os anexos internos da tarefa.
- As regras públicas do Portal Externo não são ampliadas por esta spec: um anexo só atravessa essa fronteira quando `IsExternalVisible` estiver explicitamente marcado, conforme D28.

### Exclusão e retenção

- A interface deve sempre solicitar confirmação antes de excluir um anexo.
- A exclusão deve retirar o anexo do fluxo normal e enviá-lo para uma lixeira.
- O anexo deve permanecer recuperável na lixeira durante **7 dias**.
- Ao completar 7 dias na lixeira, arquivo e metadados devem ser apagados definitivamente por rotina automática.
- A exclusão definitiva não deve ocorrer antes do encerramento do prazo de retenção.

### Histórico imutável

- Todo envio deve gerar um evento no histórico imutável da tarefa contendo:
  - autor;
  - data e hora;
  - nome do arquivo.
- Toda exclusão deve gerar um evento no histórico imutável da tarefa contendo:
  - autor da exclusão;
  - data e hora;
  - nome do arquivo.
- O evento histórico deve sobreviver à exclusão definitiva do conteúdo e dos metadados operacionais do anexo.

### Segurança desta versão

- A primeira versão não exige verificação antimalware.
- A ausência de inspeção antimalware é um risco aceito temporariamente e uma pendência de evolução futura.
- Mesmo aceitando qualquer tipo, o sistema deve continuar impedindo path traversal, controle do caminho físico pelo nome fornecido pelo cliente e exposição do caminho interno.
- O download deve usar metadados seguros e não deve executar o conteúdo no servidor.

## Estados

- **Recebido:** arquivo em processamento, ainda sem garantia de persistência completa.
- **Disponível:** arquivo armazenado, metadados persistidos e acesso normal permitido.
- **Na lixeira:** arquivo indisponível no fluxo normal, recuperável até completar 7 dias.
- **Excluído definitivamente:** arquivo e metadados operacionais removidos após o prazo; eventos do histórico permanecem.
- **Falha:** envio, leitura, movimentação para a lixeira, restauração ou exclusão definitiva não concluída.

## Persistência

- `Attachment` representa atualmente os metadados e a associação com o `WorkItem`.
- O conteúdo físico permanece em disco local controlado pela API, em `App_Data/attachments`, conforme D8.
- Os nomes físicos devem ser independentes do nome original e evitar colisão; nomes originais iguais permanecem permitidos.
- A implementação da lixeira precisa persistir, no mínimo, o estado de exclusão e a data a partir da qual começa a retenção de 7 dias.
- A rotina de expurgo deve remover arquivo e metadados de modo consistente e deve permitir diagnóstico/reprocessamento em caso de falha parcial.
- Qualquer alteração de schema necessária para lixeira ou retenção exige `G-MIGRATION` antes da implementação.

## API esperada

- Listar anexos de uma tarefa conforme a permissão de visualização.
- Enviar anexo validando tamanho, autoria e permissão.
- Baixar anexo conforme a permissão de visualização.
- Solicitar exclusão, enviando o anexo à lixeira quando o ator for o autor do envio ou administrador.
- Restaurar um anexo enquanto o prazo de 7 dias não tiver terminado.
- Impedir acesso normal a anexos que estejam na lixeira.
- A confirmação obrigatória é responsabilidade da interface; a API continua responsável por autorização e consistência.

## Interface esperada

- A aba **Anexos** deve listar nome, tamanho e ações disponíveis.
- O controle de upload deve aceitar qualquer tipo de arquivo e comunicar claramente o limite de 10 MB.
- A interface deve permitir download aos usuários autorizados.
- A ação de exclusão só deve aparecer para o autor do envio e administradores.
- A exclusão deve abrir confirmação explícita antes de enviar a operação à API.
- Deve existir acesso à lixeira para visualizar e restaurar anexos dentro dos 7 dias, respeitando permissões administrativas definidas para essa superfície.
- Erros de tamanho, permissão, armazenamento e expiração da retenção devem ser apresentados em linguagem clara.

## Estado atual comprovado no código

- Existe a entidade `Attachment`, associada ao `WorkItem`.
- Existem operações internas para listar, enviar e baixar anexos em `WorkItemAttachmentsController` e `AttachmentsFeature`.
- A listagem e o download exigem hoje `PlatformPermission.View` e papel mínimo `ProjectRole.Viewer` no escopo da tarefa.
- O upload exige hoje `PlatformPermission.Edit` e papel mínimo `ProjectRole.Member`.
- O backend aceita hoje somente uma lista fechada de extensões.
- O backend aceita hoje arquivos de até **50 MB**.
- Não há limite de quantidade comprovado para anexos internos da tarefa.
- Cada arquivo recebe identificador e nome físico próprios; portanto, nomes originais iguais não sobrescrevem necessariamente o conteúdo anterior.
- `LocalFileStorage` mantém o conteúdo físico e o SQL Server mantém os metadados.
- A interface atual permite listar, enviar e baixar anexos na aba **Anexos**.
- Não foi encontrada operação interna de exclusão de anexo em `WorkItemAttachmentsController`.
- Não foi encontrada lixeira, retenção automática de 7 dias ou restauração para anexos de tarefa.
- Não foi encontrado registro específico de upload/exclusão de anexo no histórico imutável da tarefa.
- Não foi encontrada verificação antimalware, o que está de acordo com a decisão desta primeira versão.

## Gaps entre a spec e o produto atual

1. **Tipos aceitos:** o requisito aceita qualquer tipo; o código atual usa uma lista fechada de extensões.
2. **Tamanho máximo:** o requisito limita a 10 MB; o código atual permite até 50 MB.
3. **Permissão de upload:** o requisito restringe a participantes da tarefa e administradores; o código atual valida permissão de edição/papel `Member`, sem comprovação de equivalência exata.
4. **Permissão de leitura:** o requisito libera para qualquer usuário da organização; o código atual valida acesso no escopo da tarefa/projeto, sem comprovação de acesso para todo membro da organização.
5. **Exclusão:** não existe endpoint/fluxo interno comprovado para excluir anexos de tarefa.
6. **Confirmação:** não existe ação de exclusão na interface e, portanto, não existe confirmação obrigatória.
7. **Lixeira e retenção:** não existe estado de lixeira, restauração nem expurgo automático após 7 dias.
8. **Histórico:** upload e exclusão não geram eventos específicos comprovados com autor, data e nome do arquivo.
9. **Cobertura:** não há evidência consolidada de testes automatizados e E2E cobrindo o contrato funcional completo.
10. **Homologação:** os fluxos ainda não foram testados manualmente por PO.

## Validações e erros

- Rejeitar arquivo vazio.
- Rejeitar arquivo com mais de 10 MB.
- Rejeitar upload, listagem, download, exclusão ou restauração quando o ator não possuir a permissão definida.
- Rejeitar identificadores de tarefa ou anexo inexistentes sem expor detalhes internos.
- Preservar o formulário e apresentar erro claro quando o upload falhar.
- Tratar divergências entre metadados e arquivo físico sem afirmar sucesso parcial.
- Bloquear restauração após o prazo de 7 dias.

## Critérios de aceite

- **Dado** um participante da tarefa ou administrador, **quando** enviar um arquivo de até 10 MB, **então** o anexo deve ficar disponível e o histórico deve registrar autor, data e nome.
- **Dado** um arquivo de qualquer tipo com até 10 MB, **quando** o upload for autorizado, **então** a extensão não deve causar rejeição por lista fechada.
- **Dado** dois arquivos com o mesmo nome, **quando** ambos forem enviados, **então** os dois devem permanecer disponíveis como anexos distintos.
- **Dado** qualquer usuário da organização, **quando** listar ou baixar um anexo interno da organização, **então** o acesso deve ser permitido.
- **Dado** um usuário sem participação na tarefa e que não seja administrador, **quando** tentar enviar um anexo, **então** o acesso deve ser negado.
- **Dado** o autor do envio ou um administrador, **quando** confirmar a exclusão, **então** o anexo deve ir para a lixeira e o histórico deve registrar autor, data e nome.
- **Dado** um anexo na lixeira há menos de 7 dias, **quando** uma restauração autorizada for solicitada, **então** ele deve voltar a ficar disponível.
- **Dado** um anexo na lixeira há 7 dias, **quando** a rotina automática executar, **então** arquivo e metadados operacionais devem ser removidos definitivamente e o evento histórico deve permanecer.
- **Dado** qualquer tentativa de exclusão na interface, **quando** o usuário acionar a ação, **então** deve ser apresentada confirmação antes da chamada à API.

## Testes e evidências necessárias

- Testes de autorização para upload, listagem, download, exclusão e restauração.
- Testes do limite exato de 10 MB e da rejeição acima do limite.
- Testes com extensões e MIME types variados, incluindo arquivos executáveis, sem executar o conteúdo.
- Teste de múltiplos arquivos com o mesmo nome.
- Testes de lixeira antes e depois de 7 dias e da rotina automática de expurgo.
- Testes de preservação do histórico após exclusão definitiva.
- Testes de falha parcial entre SQL Server e filesystem.
- Testes de path traversal e nomes maliciosos.
- E2E da aba **Anexos** cobrindo upload, download, confirmação, exclusão, lixeira e restauração.
- Homologação manual por PO ainda pendente para envio, download e exclusão.

## Riscos e pendências futuras

- Arquivos maliciosos podem ser armazenados porque não haverá inspeção antimalware nesta primeira versão.
- Aceitar qualquer tipo aumenta a importância de cabeçalhos seguros no download e de impedir execução pelo servidor.
- A ausência de limite de quantidade pode gerar crescimento não controlado do armazenamento local; esse risco deve ser monitorado por capacidade operacional.
- Falhas parciais podem gerar arquivo ou metadado órfão.
- A expiração automática exige rotina confiável, observável e idempotente.
- Backup e restauração precisam manter consistência entre SQL Server e `App_Data/attachments`.
- A estratégia futura de antimalware continua pendente, mas não bloqueia esta versão.

## Decisões ainda não fechadas nesta rodada

- Quem pode acessar e administrar a superfície da lixeira, além das permissões de exclusão já definidas.
- Estratégia técnica de recuperação diante de divergência entre SQL Server e filesystem.
- Estratégia institucional de backup/restauração e eventual migração para blob storage.

Esses pontos técnicos não alteram as decisões funcionais já aprovadas e devem ser fechados antes da implementação correspondente quando afetarem arquitetura, schema ou operação.

## Rollback

Esta revisão altera somente documentação. Implementações futuras devem definir rollback próprio para schema, rotina de expurgo, arquivos e metadados, com os Human Gates aplicáveis.

## Referências

- `AGENTS.md`
- `DECISIONS.md`, especialmente D8, D17, D18, D28, D34 e D47.
- `ROADMAP.md`, Fase 3 e Fase 8.
- `src/Prisma.Workspace.Domain/Entities/Attachment.cs`
- `src/Prisma.Workspace.Api/Controllers/WorkItemAttachmentsController.cs`
- `src/Prisma.Workspace.Application/Features/Attachments/AttachmentsFeature.cs`
- `src/Prisma.Workspace.Infrastructure/Storage/LocalFileStorage.cs`
- `src/Prisma.Workspace.Web/src/components/TaskDetailDrawer.tsx`

## Rastreabilidade

Decisões humanas de PO → `SPEC-ATTACHMENTS` aprovada → gaps documentados → futura tarefa de implementação aprovada → testes automatizados → homologação manual → evidência de aderência.
