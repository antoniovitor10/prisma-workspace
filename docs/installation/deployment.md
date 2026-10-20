# Operação de uma instalação independente

Uma instalação da Community Edition usa configuração, banco e volumes próprios. Comece pelo
[guia Docker](docker.md), que descreve os pré-requisitos, o bootstrap do administrador e a persistência.
O responsável pela instalação define os endereços, a edição licenciada do banco e o destino dos backups.

## Preparar uma versão candidata

Registre o commit da versão, a imagem correspondente e seu checksum. Confira o resultado dos gates de
backend, frontend e E2E da candidata. Os artefatos do CI permitem conferir a imagem e o manifesto de migrations;
a disponibilidade de um artefato não autoriza sua promoção para uma instalação.

Compare o manifesto da candidata com o da versão instalada antes da troca. Uma alteração de schema precisa
de aprovação e ensaio específicos, incluindo compatibilidade dos dados e recuperação. Não edite migrations
já aplicadas nem use o início de uma aplicação como aprovação implícita para modificar o banco.

## Configuração e persistência

Mantenha senhas, chaves, tokens e endereços operacionais em configuração externa não versionada. A instalação
independente não precisa dos secrets, das redes ou dos caminhos usados pelos mantenedores em seus ambientes.
Não copie credenciais de uma instalação para exemplos, logs ou artefatos de distribuição.

Preserve conjuntamente:

- o banco SQL Server;
- os anexos em `App_Data`;
- as chaves de Data Protection;
- a configuração externa e a identidade da imagem utilizada.

Os volumes do Compose mantêm esses dados entre recriações de containers. Confira os volumes e os caminhos
reais da instalação antes de qualquer substituição; não remova volumes para atualizar a aplicação.

## Backup e restauração

Faça o backup antes de trocar a versão. Use um backup SQL consistente, com checksum e verificação de leitura,
e uma cópia coerente dos anexos, das chaves e da configuração externa. Restrinja o acesso a esse conjunto,
pois ele pode conter credenciais e dados pessoais.

Ensaie a recuperação em outro banco e em volumes novos, sem sobrescrever a origem. Verifique o banco,
o login, os anexos e os campos importantes de uma tarefa. A presença de um arquivo de backup não comprova
que a aplicação pode ser recuperada. Consulte também a seção de operação do [guia Docker](docker.md).

## Troca e retorno à versão anterior

Prepare uma janela de manutenção e um critério de aceitação. Depois da troca, confira a saúde da aplicação,
o acesso do administrador e os fluxos essenciais. Registre o commit e o resultado da verificação.

Mantenha a imagem anterior e o conjunto de recuperação até validar a candidata. Voltar à imagem anterior exige
compatibilidade com o schema atual; restaurar dados exige um procedimento separado, ensaiado e autorizado.
Evite operações de limpeza enquanto esses recursos sustentarem a recuperação.

A promoção e a publicação de uma versão permanecem decisões do operador e dos gates humanos aplicáveis.
Este guia não executa atualizações nem configura um serviço de publicação automática.
