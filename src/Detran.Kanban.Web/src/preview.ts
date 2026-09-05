import type { BacklogItem, Sprint, Team } from './types/scrum';

export const previewMode = import.meta.env.DEV && import.meta.env.VITE_PREVIEW_MODE === 'true';

export const previewProject = {
  id: '11111111-1111-1111-1111-111111111111',
  key: 'PRISMA',
  name: 'Plataforma de Serviços Digitais',
  description: 'Modernização dos serviços internos e digitais.',
  boards: [
    { id: '22222222-2222-2222-2222-222222222222', name: 'Desenvolvimento', teamId: '33333333-3333-3333-3333-333333333333' },
    { id: '22222222-2222-2222-2222-222222222223', name: 'Sustentação', teamId: '33333333-3333-3333-3333-333333333333' },
  ],
  teams: [{ id: '33333333-3333-3333-3333-333333333333', name: 'Novas Tecnologias' }],
};

export const previewTeams: Team[] = [{
  id: '33333333-3333-3333-3333-333333333333',
  name: 'Novas Tecnologias',
  members: [
    { userId: 'user-ana', name: 'Ana Costa', weeklyCapacityHours: 40, weekHours: 14.5 },
    { userId: 'user-bruno', name: 'Bruno Lima', weeklyCapacityHours: 40, weekHours: 11 },
    { userId: 'user-carla', name: 'Carla Souza', weeklyCapacityHours: 30, weekHours: 8.5 },
  ],
}];

export const previewSprints: Sprint[] = [
  {
    id: '44444444-4444-4444-4444-444444444444', projectId: previewProject.id,
    teamId: previewTeams[0].id, name: 'Sprint 24', goal: 'Entregar a nova jornada de serviços digitais',
    teamName: 'Novas Tecnologias', startDate: '2026-07-13', endDate: '2026-07-24', status: 2,
    itemCount: 7, completedItemCount: 2, storyPoints: 34, completedStoryPoints: 8, remainingHours: 31,
    capacities: [
      { userId: 'user-ana', availableHours: 64, daysOffHours: 0 },
      { userId: 'user-bruno', availableHours: 64, daysOffHours: 8 },
      { userId: 'user-carla', availableHours: 48, daysOffHours: 0 },
    ],
  },
  {
    id: '44444444-4444-4444-4444-444444444445', projectId: previewProject.id,
    teamId: previewTeams[0].id, name: 'Sprint 25', goal: 'Melhorar observabilidade e atendimento',
    teamName: 'Novas Tecnologias', startDate: '2026-07-27', endDate: '2026-08-07', status: 1,
    itemCount: 3, completedItemCount: 0, storyPoints: 21, completedStoryPoints: 0, remainingHours: 24,
    capacities: [],
  },
  {
    id: '44444444-4444-4444-4444-444444444443', projectId: previewProject.id,
    teamId: previewTeams[0].id, name: 'Sprint 23', goal: 'Estabilizar a consulta de documentos',
    teamName: 'Novas Tecnologias', startDate: '2026-06-29', endDate: '2026-07-10', status: 3,
    itemCount: 9, completedItemCount: 8, storyPoints: 38, completedStoryPoints: 34, remainingHours: 2,
    capacities: [],
  },
  {
    id: '44444444-4444-4444-4444-444444444442', projectId: previewProject.id,
    teamId: previewTeams[0].id, name: 'Sprint 22', goal: 'Consolidar o fluxo de triagem',
    teamName: 'Novas Tecnologias', startDate: '2026-06-15', endDate: '2026-06-26', status: 3,
    itemCount: 8, completedItemCount: 8, storyPoints: 31, completedStoryPoints: 31, remainingHours: 0,
    capacities: [],
  },
];

export const previewBacklog: BacklogItem[] = [
  {
    id: 'a0000000-0000-0000-0000-000000000001', boardId: previewProject.boards[0].id,
    boardName: 'Desenvolvimento', kind: 1, title: 'Portal unificado de serviços digitais',
    description: 'Centralizar os serviços digitais e reduzir o tempo de atendimento ao cidadão.',
    priority: 2, points: 55, rank: 1000, createdAt: '2026-06-20T12:00:00Z',
  },
  {
    id: 'a0000000-0000-0000-0000-000000000002', boardId: previewProject.boards[0].id,
    boardName: 'Desenvolvimento', parentId: 'a0000000-0000-0000-0000-000000000001', kind: 2,
    title: 'Autenticação e perfil do cidadão', priority: 2, points: 21, rank: 2000,
    createdAt: '2026-06-24T12:00:00Z',
  },
  {
    id: 'a0000000-0000-0000-0000-000000000003', boardId: previewProject.boards[0].id,
    boardName: 'Desenvolvimento', parentId: 'a0000000-0000-0000-0000-000000000002',
    sprintId: previewSprints[0].id, stageName: 'Em desenvolvimento', kind: 3,
    title: 'Como cidadão, quero consultar a situação da CNH',
    description: 'Exibir situação, validade e eventuais impedimentos de forma clara e responsiva.',
    priority: 2, points: 8, estimatedHours: 18, remainingHours: 10, rank: 3000,
    dueDate: '2026-07-18', createdAt: '2026-07-01T12:00:00Z', assigneeIds: ['user-ana'],
  },
  {
    id: 'a0000000-0000-0000-0000-000000000004', boardId: previewProject.boards[0].id,
    boardName: 'Desenvolvimento', parentId: 'a0000000-0000-0000-0000-000000000003',
    sprintId: previewSprints[0].id, stageName: 'Em desenvolvimento', kind: 5,
    title: 'Implementar endpoint de consulta da CNH', priority: 1, estimatedHours: 10,
    remainingHours: 6, rank: 4000, createdAt: '2026-07-02T12:00:00Z', assigneeIds: ['user-bruno'],
  },
  {
    id: 'a0000000-0000-0000-0000-000000000005', boardId: previewProject.boards[1].id,
    boardName: 'Sustentação', sprintId: previewSprints[0].id, stageName: 'Concluído', kind: 4,
    title: 'Corrigir lentidão na emissão de documentos', priority: 2, points: 5,
    estimatedHours: 12, remainingHours: 0, rank: 5000, createdAt: '2026-07-08T12:00:00Z',
    completedAt: '2026-07-14T17:20:00Z', assigneeIds: ['user-carla'],
  },
  {
    id: 'a0000000-0000-0000-0000-000000000006', boardId: previewProject.boards[1].id,
    boardName: 'Sustentação', kind: 3, title: 'Adicionar alertas operacionais no painel',
    priority: 1, points: 8, rank: 6000, createdAt: '2026-07-10T12:00:00Z',
  },
  {
    id: 'a0000000-0000-0000-0000-000000000007', boardId: previewProject.boards[0].id,
    boardName: 'Desenvolvimento', sprintId: previewSprints[0].id, stageName: 'Validação', kind: 3,
    title: 'Acompanhar solicitação por protocolo', priority: 2, points: 3, estimatedHours: 8,
    remainingHours: 4, rank: 7000, createdAt: '2026-07-03T12:00:00Z', assigneeIds: ['user-ana'],
  },
  {
    id: 'a0000000-0000-0000-0000-000000000008', boardId: previewProject.boards[0].id,
    boardName: 'Desenvolvimento', sprintId: previewSprints[0].id, stageName: 'Concluído', kind: 3,
    title: 'Confirmar solicitação por e-mail', priority: 1, points: 3, estimatedHours: 6,
    remainingHours: 0, rank: 8000, createdAt: '2026-07-02T12:00:00Z',
    completedAt: '2026-07-13T19:00:00Z', assigneeIds: ['user-bruno'],
  },
  {
    id: 'a0000000-0000-0000-0000-000000000009', boardId: previewProject.boards[1].id,
    boardName: 'Sustentação', sprintId: previewSprints[0].id, stageName: 'A fazer', kind: 4,
    title: 'Tratar falha intermitente na geração do protocolo', priority: 2, points: 8,
    estimatedHours: 15, remainingHours: 11, rank: 9000, createdAt: '2026-07-12T12:00:00Z', assigneeIds: ['user-carla'],
  },
  {
    id: 'a0000000-0000-0000-0000-000000000010', boardId: previewProject.boards[0].id,
    boardName: 'Desenvolvimento', sprintId: previewSprints[0].id, stageName: 'A fazer', kind: 5,
    title: 'Criar telemetria do fluxo externo', priority: 1, estimatedHours: 8,
    remainingHours: 8, rank: 10000, createdAt: '2026-07-12T12:00:00Z', assigneeIds: ['user-bruno'],
  },
];
