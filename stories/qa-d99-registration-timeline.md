# Cadastro identificado e movimentações legíveis

Como pessoa que usa o Prisma, quero cadastrar nome e sobrenome e consultar as movimentações em uma lista cronológica, para reconhecer os responsáveis e acompanhar o caminho da tarefa.

Origem: D99, SPEC-AUTH-001 e SPEC-TASK-HISTORY aprovadas, grupo C5 do goal Oficina. A linha do tempo é a visão principal de movimentações; o grafo continua como complemento explícito. Automações e lead time ficam ocultos na interface conforme as specs aprovadas, preservando execução e coleta internas.

Aceite: cadastro público exige duas partes no nome e preserva a identificação até a associação; a linha do tempo usa registros persistidos, criação e movimentos em ordem decrescente, com autoria preservada e fallback neutro quando ausente. Não cria schema, snapshots novos ou reescrita de histórico; nomes de colunas atuais devem ser identificados como fallback existente.
