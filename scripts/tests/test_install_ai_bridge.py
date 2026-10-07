import copy
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

spec = importlib.util.spec_from_file_location("bridge_install", Path(__file__).parents[1] / "install-ai-bridge.py")
installer = importlib.util.module_from_spec(spec)
spec.loader.exec_module(installer)


def existing():
    return {"Config": {"Env": ["AI_BRIDGE_TOKEN=fake-private-token"], "User": "node", "Labels": {"org.opencontainers.image.revision": "a" * 40}},
            "HostConfig": {"ReadonlyRootfs": True, "Privileged": False, "PortBindings": {}},
            "NetworkSettings": {"Networks": {installer.NETWORK: {}}},
            "Mounts": [{"Type": "volume", "RW": True, "Name": name, "Destination": destination} for name, destination in [("prisma_ai_cli_auth", "/home/node/.claude"), ("prisma_ai_codex_auth", "/home/node/.codex")]]}


class BridgeUpgradeTests(unittest.TestCase):
    def test_accepts_only_preserved_private_auth_volumes_and_optional_gemini(self):
        current = existing(); installer.validate_existing(current, "fake-private-token")
        current["Mounts"].append({"Type": "volume", "RW": True, "Name": "prisma_ai_gemini_auth", "Destination": "/home/node/.gemini"})
        installer.validate_existing(current, "fake-private-token")
        current["Mounts"][0]["Name"] = "unexpected"
        with self.assertRaises(ValueError): installer.validate_existing(current, "fake-private-token")

    def test_refuses_bind_mount_database_network_public_ports_or_changed_token(self):
        changes = [lambda x: x["Mounts"][0].update(Type="bind"), lambda x: x["NetworkSettings"]["Networks"].update(sql={}),
                   lambda x: x["HostConfig"].update(PortBindings={"8080/tcp": [{"HostPort":"8080"}]}), lambda x: x["Config"].update(User="root")]
        for change in changes:
            current = existing(); change(current)
            with self.assertRaises(ValueError): installer.validate_existing(current, "fake-private-token")
        with self.assertRaises(ValueError): installer.validate_existing(existing(), "wrong-token")

    def test_failed_upgrade_restores_previous_container_and_never_removes_auth_volumes(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory); source = root / "incoming" / "b" / "bridge"; source.mkdir(parents=True)
            (source / "Dockerfile").write_text("FROM scratch")
            (root / "ai-bridge.env").write_text("Ai__BridgeToken=fake-private-token\nAi__BridgeUrl=http://prisma-ai-bridge:8080/v1\n")
            commands = []; sha = "b" * 40
            def run(*args, **_kwargs):
                commands.append(args)
                if args[:3] == ("docker", "network", "inspect"):
                    return json.dumps([{ "Labels": {"prisma.purpose":"ai-bridge"}, "Containers": {} }])
                if args[:2] == ("docker", "inspect"):
                    value = existing()
                    if any(command[:3] == ("docker", "run", "-d") for command in commands):
                        value["Config"]["Labels"]["org.opencontainers.image.revision"] = sha
                    return json.dumps([value])
                return ""
            with patch.object(installer, "ROOT", root), patch.object(installer, "run", side_effect=run), patch.object(installer, "exists", return_value=True), patch.object(installer, "wait_healthy", side_effect=[RuntimeError("unhealthy"), None]):
                with self.assertRaises(RuntimeError): installer.install(source, sha, upgrade=True)
            self.assertTrue(any(command[:3] == ("docker", "rename", installer.CONTAINER) for command in commands))
            self.assertIn(("docker", "start", installer.CONTAINER), commands)
            self.assertFalse(any(command[:3] == ("docker", "volume", "rm") for command in commands))
