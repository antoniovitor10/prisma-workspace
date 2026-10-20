#!/usr/bin/env python3
"""Registra a origem declarada e hashes da candidata; não produz assinatura ou nível SLSA."""

import argparse
import hashlib
import json
import os
from pathlib import Path
import re
from urllib.parse import quote, urlsplit


INPUT_FILES = (
    "prisma-image.tar.gz",
    "migrations.json",
    "sbom-runtime.spdx.json",
    "sbom-frontend.spdx.json",
)


def file_digest(path: Path) -> str:
    if path.is_symlink() or not path.is_file():
        raise ValueError(f"Arquivo regular obrigatório: {path.name}")
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for chunk in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def create_statement(directory: Path, environment: dict[str, str]) -> dict:
    # Apenas contexto público de execução; nunca serializar o ambiente completo.
    repository = environment.get("GITHUB_REPOSITORY", "")
    commit = environment.get("GITHUB_SHA", "")
    workflow_commit = environment.get("GITHUB_WORKFLOW_SHA", "")
    ref = environment.get("GITHUB_REF", "")
    server = environment.get("GITHUB_SERVER_URL", "")
    run_id = environment.get("GITHUB_RUN_ID", "")
    attempt = environment.get("GITHUB_RUN_ATTEMPT", "")
    workflow_ref = environment.get("GITHUB_WORKFLOW_REF", "")
    if not re.fullmatch(r"[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+", repository):
        raise ValueError("Repositório do CI inválido.")
    if not all(re.fullmatch(r"[0-9a-f]{40}|[0-9a-f]{64}", value)
               for value in (commit, workflow_commit)):
        raise ValueError("Commit do código ou workflow inválido.")
    if not re.fullmatch(r"refs/(heads|tags|pull)/[^\x00-\x20]+", ref):
        raise ValueError("Referência do código inválida.")
    url = urlsplit(server)
    if (url.scheme != "https" or not url.hostname or url.username or url.password
            or url.path not in ("", "/") or url.query or url.fragment):
        raise ValueError("Origem do servidor de CI inválida.")
    if not all(re.fullmatch(r"[1-9][0-9]*", value) for value in (run_id, attempt)):
        raise ValueError("Execução ou tentativa do CI inválida.")
    expected_prefix = f"{repository}/.github/workflows/ci.yml@refs/"
    if not workflow_ref.startswith(expected_prefix) or any(ord(char) <= 32 for char in workflow_ref):
        raise ValueError("Workflow de origem inválido.")

    digests = {name: file_digest(directory / name) for name in INPUT_FILES}
    checksum = directory / "prisma-image.sha256"
    if checksum.is_symlink() or not checksum.is_file():
        raise ValueError("Checksum da imagem ausente ou inválido.")
    expected = f"{digests['prisma-image.tar.gz']}  prisma-image.tar.gz"
    if checksum.read_text(encoding="utf-8").rstrip("\n") != expected:
        raise ValueError("O checksum não corresponde à imagem candidata.")
    json.loads((directory / "migrations.json").read_text(encoding="utf-8"))
    for name in INPUT_FILES[2:]:
        document = json.loads((directory / name).read_text(encoding="utf-8"))
        if (document.get("spdxVersion") != "SPDX-2.3"
                or not isinstance(document.get("packages"), list)
                or not document["packages"]):
            raise ValueError(f"Inventário SPDX 2.3 vazio ou inválido: {name}")

    base = server.rstrip("/")
    workflow_uri = f"{base}/{repository}/blob/{workflow_commit}/.github/workflows/ci.yml"
    return {
        "_type": "https://in-toto.io/Statement/v1",
        "subject": [{"name": name, "digest": {"sha256": value}} for name, value in digests.items()],
        "predicateType": "https://slsa.dev/provenance/v1",
        "predicate": {
            "buildDefinition": {
                "buildType": f"{base}/{repository}/blob/{workflow_commit}/docs/maintenance/supply-chain.md#build-type",
                "externalParameters": {"repository": repository, "ref": ref, "workflowRef": workflow_ref},
                "resolvedDependencies": [{
                    "uri": f"git+{base}/{repository}.git@{quote(ref, safe='/')}",
                    "digest": {"gitCommit": commit},
                }],
            },
            "runDetails": {
                "builder": {"id": workflow_uri},
                "metadata": {"invocationId": f"{base}/{repository}/actions/runs/{run_id}/attempts/{attempt}"},
            },
        },
    }


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("directory", type=Path)
    args = parser.parse_args()
    statement = create_statement(args.directory, dict(os.environ))
    output = args.directory / "provenance.unsigned.intoto.json"
    if output.is_symlink():
        raise ValueError("Saída de proveniência não pode ser um link simbólico.")
    output.write_text(json.dumps(statement, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
