import hashlib
import importlib.util
import json
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch

spec = importlib.util.spec_from_file_location("deployment", Path(__file__).parents[1] / "deploy-production.py")
deployment = importlib.util.module_from_spec(spec)
spec.loader.exec_module(deployment)
SHA = "a" * 40


class DeploymentSafetyTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.incoming = self.root / "incoming" / SHA
        self.incoming.mkdir(parents=True)
        payload = b"candidate archive"
        (self.incoming / "prisma-image.tar.gz").write_bytes(payload)
        (self.incoming / "prisma-image.sha256").write_text(hashlib.sha256(payload).hexdigest())
        for path in [self.root / "migrations.json", self.incoming / "migrations.json"]:
            path.write_text(json.dumps({"20260101000000_Initial.cs": "hash"}))
        self.patch_root = patch.object(deployment, "ROOT", self.root)
        self.patch_root.start()
        self.addCleanup(self.patch_root.stop)

    def test_changed_migration_cannot_touch_docker(self):
        (self.incoming / "migrations.json").write_text("{}")
        with patch.object(deployment, "run") as run:
            with self.assertRaisesRegex(ValueError, "G-MIGRATION"):
                deployment.deploy(SHA)
            run.assert_not_called()

    def test_corrupted_artifact_cannot_touch_docker(self):
        (self.incoming / "prisma-image.tar.gz").write_bytes(b"changed")
        with patch.object(deployment, "run") as run:
            with self.assertRaisesRegex(ValueError, "Checksum"):
                deployment.deploy(SHA)
            run.assert_not_called()

    def test_failed_health_restores_previous_container(self):
        previous = {"Id": "previous", "Config": {"Env": [], "Labels": {}},
                    "HostConfig": {"PortBindings": {}}, "Mounts": [
                        {"Type": "bind", "Source": str(self.root), "Destination": "/app/App_Data", "RW": True}],
                    "NetworkSettings": {"Networks": {"slc_default": {"Aliases": ["prisma-api"]}}}}
        def inspect(name):
            if name.startswith("prisma-workspace:"):
                return {"Config": {"Labels": {"org.opencontainers.image.revision": SHA}}}
            if name == deployment.SQL_CONTAINER:
                return {"Config": {"Env": ["MSSQL_SA_PASSWORD=placeholder"]}}
            return previous
        results = [subprocess.CompletedProcess([], 0, json.dumps([{"Id": "candidate"}]).encode()),
                   subprocess.CompletedProcess([], 0, b""),
                   subprocess.CompletedProcess([], 0, b"[]")]
        with patch.object(deployment, "inspect", side_effect=inspect), \
             patch.object(deployment, "run") as run, \
             patch.object(deployment, "healthy", side_effect=[False, True]), \
             patch.object(deployment.subprocess, "run", side_effect=results) as remove, \
             patch.object(deployment.Path, "mkdir"):
            with self.assertRaisesRegex(RuntimeError, "candidata"):
                deployment.deploy(SHA)
            commands = [call.args for call in run.call_args_list]
            self.assertIn(("docker", "start", deployment.CONTAINER), commands)
            self.assertTrue(any(command[:2] == ("docker", "rename") and command[-1] == deployment.CONTAINER for command in commands))
            remove.assert_any_call(["docker", "rm", "--force", deployment.CONTAINER], check=True)
            self.assertFalse((self.root / "current-commit").exists())


if __name__ == "__main__":
    unittest.main()
