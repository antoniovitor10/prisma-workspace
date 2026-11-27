-- T-0019 · Itens que a edição pela gaveta transformou de Tarefa (5) em Subtarefa (6).
--
-- SOMENTE LEITURA. Nada aqui grava. Não rodar em produção sem o ok do desenvolvedor; de preferência num backup restaurado.
-- A correção NÃO é feita por SQL (PRD da HU-0008, D12): é a rotina de código
-- `WorkItemKindRepairRoutine` (src/Prisma.Workspace.Infrastructure/Services/WorkItems/), que roda uma vez na
-- inicialização da API conforme a configuração `Maintenance__WorkItemKindRepair`:
--   (ausente) ou off  -> não roda (padrão; é assim que a imagem sai)
--   dry-run           -> só lista no log, com identificadores, o que corrigiria
--   apply             -> corrige: Kind 6 -> 5, um TaskEvent `work_item_kind_repaired` por item, autor `system`
-- A rotina é idempotente (o evento marca o item corrigido) e vale para todas as organizações (D-2 revisada).
-- Esta consulta reproduz o filtro da rotina para conferência antes e depois.

-- 1) Candidatos: evento `updated` com Kind 5 -> 6, item ainda Subtarefa, com o tipo do pai atual e se já foi corrigido.
SELECT e.Id            AS SourceEventId,
       w.Id            AS WorkItemId,
       w.Number,
       b.OrganizationId,
       b.ProjectId,
       p.Kind          AS ParentKind,
       w.Kind          AS KindAtual,
       w.IsArchived,
       e.CreatedAt     AS EventoEm,
       CASE
         WHEN EXISTS (SELECT 1 FROM TaskEvents r WHERE r.WorkItemId = w.Id AND r.Kind = 'work_item_kind_repaired') THEN 'ja corrigido'
         WHEN w.Kind <> 6 THEN 'mudou de tipo depois do evento'
         WHEN p.Id IS NULL THEN 'sem pai'
         WHEN p.Kind = 5 THEN 'subtarefa valida sob tarefa (nao tocar)'
         WHEN p.Kind <> 3 THEN 'pai nao aceita Tarefa (nao corrigido)'
         WHEN EXISTS (SELECT 1 FROM TaskEvents later
                      WHERE later.WorkItemId = w.Id AND later.Kind = 'updated' AND later.CreatedAt > e.CreatedAt
                        AND ISJSON(later.Payload) = 1
                        AND JSON_VALUE(later.Payload, '$.previous.Kind') <> JSON_VALUE(later.Payload, '$.current.Kind'))
              THEN 'mudou de tipo depois do evento'
         ELSE 'corrigir'
       END             AS Situacao
FROM TaskEvents e
JOIN WorkItems w ON w.Id = e.WorkItemId
JOIN Boards b    ON b.Id = w.BoardId
LEFT JOIN WorkItems p ON p.Id = w.ParentId
WHERE e.Kind = 'updated'
  AND ISJSON(e.Payload) = 1
  AND JSON_VALUE(e.Payload, '$.previous.Kind') = '5'
  AND JSON_VALUE(e.Payload, '$.current.Kind')  = '6'
ORDER BY b.OrganizationId, w.Number, e.CreatedAt;

-- 2) Depois do `apply`: correções gravadas pela rotina.
SELECT r.WorkItemId, w.Number, b.OrganizationId, b.ProjectId, w.Kind AS KindAtual, r.CreatedAt, r.Payload
FROM TaskEvents r
JOIN WorkItems w ON w.Id = r.WorkItemId
JOIN Boards b    ON b.Id = w.BoardId
WHERE r.Kind = 'work_item_kind_repaired'
ORDER BY r.CreatedAt;
