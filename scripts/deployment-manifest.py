"""Emite hashes das migrations; diferenças exigem operação humana explícita."""
import hashlib
import json
from pathlib import Path

root = Path(__file__).resolve().parents[1]
directory = root / "src/Prisma.Workspace.Infrastructure/Persistence/Migrations"
manifest = {
    path.name: hashlib.sha256(path.read_bytes().replace(b"\r\n", b"\n")).hexdigest()
    for path in sorted(directory.glob("[0-9]*.cs"))
    if not path.name.endswith(".Designer.cs")
}
print(json.dumps(manifest, sort_keys=True, indent=2))
