# Oficina qa-6 — C6, quadros arquivados e acesso por lote

Branch fix/qa-round-1, base a2797a7 com merges L06/L07/L08. PR27 em rascunho. Produto principal em3f973c1, acumulado com Brasília e testes visuais eme705aec; o commit desta entrega acrescenta relato, sem modificar a lógica C6.

BoardRepository.GetAllAsync exclui quadros cujo projeto está arquivado, preservando registros e IDs. ProjectAccessService e PermissionService consultam conjuntos de projetos/concessões, evitando consultas por projeto. Método aditivo da interface tem fallback para implementações alternativas; a implementação EF registrada usa lote. Negação explícita,membership/equipe ativas,papéis administrativos,proprietário,tenant e escopo do token do método preservados.

Backend compilado e17/17 testes afetados passaram, incluindo SQL real em3111 e matriz de perfis/permissões. SQL usa24 projetos e mede quantidade constante de comandos:até5 para acesso e1 para listar quadros. Contraprova com serviços anteriores produziu2 falhas:quadro arquivado continuava listado e consultas cresceram de6 para145. finally restaurou, recompilação e17/17 novamente verdes. Fixtures iniciais foram corrigidas com OwnerId e contexto próprio por tenant, sem alterar proteção.

Native inicial de API/reativação/IDs:3/3PASS. Roteiro reforçado do seletor real:3/3PASS, incluindo setup e um caso por largura1440x900/390x844. Cria dois projetos próprios, confirma opção antes do arquivamento, confirma ausência no seletor do projeto-âncora e presença após reativar, com a mesma tarefa/ID/número. Seis capturas ativo/arquivado/reativado foram abertas e inspecionadas. Nenhum dado do seed é usado como fixture de negócio.

Frontend de produto não mudou por C6; tipos/lint/build da branch e27 Vitest visuais passaram. Suite completa fica para o fim da branch. Autorrevisão:sem alto/médio confirmado; agrupamento mantém precedência de negação e filtros de tenant. Auto-QA:seletor responde e tarefa reaparece após reativação nas duas larguras.

Limites/riscos: número constante de consultas não promete custo ilimitado por organização; fallback de implementações não EF preserva compatibilidade, sem promessa de lote. Leitura direta e restauração mantêm os contratos existentes. Não houve índice,schema ou migration nova. Sem produção,main,merge dePR ou deploy.

REVISAO e QA oficiais:pendentes. Entrega C6 não equivale à conclusão da rodada inteira.
