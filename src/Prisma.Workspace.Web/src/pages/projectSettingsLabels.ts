export function translateProjectHistoryKind(kind: string): string {
  const labels: Record<string,string> = { created:'Projeto criado', updated:'Projeto atualizado', archived:'Projeto arquivado', reactivated:'Projeto reativado', member_added:'Membro adicionado', member_updated:'Membro atualizado', member_removed:'Membro removido', team_added:'Equipe adicionada', team_removed:'Equipe removida', custom_field_created:'Campo personalizado criado', custom_field_saved:'Campo personalizado salvo', custom_field_deleted:'Campo personalizado desativado', custom_field_disabled:'Campo personalizado desativado' };
  const normalized = kind.trim().toLowerCase();
  const fallback = normalized.replace(/[_-]+/g,' ').split(' ').filter(Boolean).map(word=>word.charAt(0).toUpperCase()+word.slice(1)).join(' ');
  return labels[normalized] ?? (fallback || 'Alteração administrativa');
}

export function projectMemberSuccessMessage(alreadyMember: boolean): string {
  return alreadyMember ? 'Papel do membro atualizado.' : 'Membro adicionado ao projeto.';
}
