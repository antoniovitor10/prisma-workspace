# Inventários e origem declarada da candidata

O job `container` guarda os inventários e a proveniência no mesmo artefato que contém o export da imagem,
seu checksum e o manifesto de migrations. Não publica imagem, release ou snapshot de dependências.
As permissões continuam `contents: read`; o job de implantação não foi alterado.

## Arquivos

- `sbom-runtime.spdx.json`: Syft examina a imagem candidata local, incluindo pacotes do sistema e dependências
  reconhecidas nos arquivos publicados da aplicação.
- `sbom-frontend.spdx.json`: inventário das entradas do build frontend a partir de seu diretório/lock.
  Inclui ferramentas de desenvolvimento; não afirma que cada pacote identificado foi incorporado ao JavaScript final.
- `provenance.unsigned.intoto.json`: declaração in-toto com predicate de proveniência SLSA v1,
  vinculando hashes do export, manifesto e dois inventários ao contexto do CI.

Anchore SBOM Action está fixada no commit da versão0.24.3; Syft em1.55.0. Atualizações dessas versões precisam
de revisão. O formato SPDX é produzido pelo scanner; seus campos de licença não escolhem a licença do Prisma.
A decisão de licença e os gates de publicação continuam humanos.

## Build type

O identificador desse build type aponta para esta documentação no commit do workflow executado.
O procedimento é o job `container` de `.github/workflows/ci.yml`: construir com o Dockerfile do código selecionado,
exportar a imagem, gerar checksum e manifesto, inventariar runtime/frontend e executar
`python3 scripts/candidate-provenance.py image`. Depois, o upload existente armazena o diretório `image/`.

`externalParameters` contém `repository` (owner/repository), `ref` (referência Git) e `workflowRef`
(arquivo do workflow e sua referência), fornecidos pelo ambiente GitHub Actions. O commit efetivo do código
fica em `resolvedDependencies`, com URI Git e digest `gitCommit`. O commit do workflow é registrado separadamente
na URI de `builder.id`, pois pode diferir do código construído. `invocationId` aponta para a execução e tentativa.
Não há dump de variáveis, argumentos arbitrários, tokens ou cabeçalhos.

O script exige os quatro arquivos regulares, JSONs legíveis, inventários SPDX2.3 não vazios e o checksum correto
da imagem. Recusa links simbólicos nos arquivos e contexto de CI ausente/inválido. Não substitui validadores completos
dos esquemas SPDX/SLSA ou o gate existente do manifesto de migrations.

O workflow é iniciado pelos eventos já existentes de PR/push ou, por um operador autorizado, pela interface do CI.
Para reprodução, use o código e o workflow indicados pela declaração e confira as versões das ferramentas.
As dependências resolvidas listadas na proveniência são uma coleta parcial; bases Docker e pacotes remotos podem
variar. Não se afirma build reproduzível bit a bit nem fechamento completo dos materiais.

## Limite de confiança

A declaração é **unsigned**, gerada pelo processo do próprio repositório. Não é uma atestação assinada da plataforma,
não estabelece identidade criptográfica do produtor e não atribui nível SLSA. Quem puder substituir todo o artefato
também poderá recalcular seus hashes. Para avaliar a candidata, obtenha o artefato da execução confiável,
confira a origem do workflow e compare os hashes de cada `subject` com os arquivos recebidos.

SBOM é inventário, não parecer de ausência de vulnerabilidades. A integração deste lote depende dos ajustes npm
e NuGet testados das TASK-502/503. Assinatura, registro público e política de promoção permanecem decisões separadas.

Referências: [Anchore SBOM Action](https://github.com/anchore/sbom-action),
[proveniência SLSA v1](https://slsa.dev/spec/v1.1/provenance)
e [Statement in-toto](https://github.com/in-toto/attestation/blob/main/spec/v1/statement.md).
