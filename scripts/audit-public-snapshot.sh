#!/usr/bin/env bash
set -euo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repository_root"

if ! command -v gitleaks >/dev/null 2>&1; then
  echo "gitleaks não está instalado; a auditoria não pode ser considerada concluída." >&2
  exit 1
fi

mapfile -t files < <(git ls-files --cached --others --exclude-standard)
violations=()

for file in "${files[@]}"; do
  normalized="${file//\\//}"

  case "$normalized" in
    .agent-state/*|.artifacts/*|.runlogs/*|docs/entrada/*|migracao/*)
      violations+=("prefix:$normalized")
      ;;
    .github/workflows/deploy.yml|DEPLOY.md|HANDOFF.md|RODAR-LOCAL.md|docs/context-explorer.html|out1.txt|tools/context-explorer/model.json|tools/context-explorer/web/public/model.json)
      violations+=("path:$normalized")
      ;;
    */e2e/.auth/*)
      violations+=("browser-auth:$normalized")
      ;;
  esac

  name="${normalized##*/}"
  if [[ "$name" == ".env" || ( "$name" == .env.* && "$name" != ".env.example" ) ]]; then
    violations+=("environment:$normalized")
  fi

  case "${normalized,,}" in
    *.bak|*.docx|*.pptx|*.xlsx|*.7z)
      violations+=("extension:$normalized")
      ;;
  esac
done

if ((${#violations[@]} > 0)); then
  printf 'Snapshot público reprovado:\n' >&2
  printf -- '- %s\n' "${violations[@]}" | sort -u >&2
  exit 1
fi

gitleaks dir "$repository_root" --config "$repository_root/.gitleaks.toml" --no-banner --redact=100 --exit-code 1
printf 'Snapshot aprovado: %d arquivos candidatos e zero achados do gitleaks.\n' "${#files[@]}"
