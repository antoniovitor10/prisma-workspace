import importlib.util
import pathlib
import unittest
import sys
import time
from unittest.mock import patch
sys.path.insert(0, str(pathlib.Path(__file__).parent))
import auth

spec = importlib.util.spec_from_file_location("bridge", pathlib.Path(__file__).with_name("server.py"))
bridge = importlib.util.module_from_spec(spec)
spec.loader.exec_module(bridge)


class BridgeTests(unittest.TestCase):
    def test_login_instructions_only_accept_official_https_hosts(self):
        url, code = auth.instructions("codex", "https://attacker.invalid/login\n\x1b[94mhttps://auth.openai.com/codex/device\x1b[0m\n\x1b[94mTEST-ABCDE\x1b[0m")
        self.assertEqual(url, "https://auth.openai.com/codex/device")
        self.assertEqual(code, "TEST-ABCDE")
        self.assertEqual(auth.instructions("claude", "https://claude.com.attacker.invalid/?secret=credential"), (None, None))
        self.assertEqual(auth.instructions("claude", "https://user:password@claude.com/login"), (None, None))

    def test_sessions_are_owned_and_adapter_bound(self):
        manager = auth.Sessions()
        with patch.object(auth.Session, "run"):
            started = manager.start("codex", "first")
        session_id = started["sessionId"]
        with self.assertRaises(FileNotFoundError):
            manager.get("codex", "second", session_id)
        with self.assertRaises(FileNotFoundError):
            manager.get("claude", "first", session_id)
        with self.assertRaises(FileExistsError):
            manager.start("codex", "second")
        row = manager.get("codex", "first", session_id)
        row.url, row.device_code = "https://auth.openai.com/codex/device", "TEST-CODE"
        row.terminate()
        self.assertEqual(row.view()["state"], "cancelled")
        self.assertIsNone(row.view()["url"])
        self.assertIsNone(row.view()["deviceCode"])

    def test_expired_sessions_discard_instructions_and_are_removed(self):
        manager = auth.Sessions()
        row = auth.Session("codex", "owner")
        row.expires = time.time() - 1
        row.url, row.device_code = "https://auth.openai.com/codex/device", "TEST-CODE"
        manager.rows[row.id] = row
        self.assertEqual(manager.get("codex", "owner", row.id).state, "expired")
        self.assertIsNone(row.url)
        row.expires -= 61
        manager.cleanup()
        self.assertFalse(manager.rows)

    def test_claude_completion_rejects_wrong_state_or_control_characters(self):
        row = auth.Session("claude", "owner")
        with self.assertRaises(ValueError):
            row.complete("code")
        row.state = "waiting"
        from unittest.mock import Mock
        row.process = Mock()
        row.process.poll.return_value = None
        with self.assertRaises(ValueError):
            row.complete("code\nsecond-command")
        row.process.stdin.write.assert_not_called()

    def test_environment_never_forwards_bridge_database_or_api_credentials(self):
        with patch.dict(auth.os.environ, {"AI_BRIDGE_TOKEN": "private", "DATABASE_URL": "private", "ANTHROPIC_API_KEY": "private", "OPENAI_API_KEY": "private", "CLAUDE_CODE_OAUTH_TOKEN": "private"}):
            env = auth.environment("/tmp/isolated")
        for key in ["AI_BRIDGE_TOKEN", "DATABASE_URL", "ANTHROPIC_API_KEY", "OPENAI_API_KEY", "CLAUDE_CODE_OAUTH_TOKEN"]:
            self.assertNotIn(key, env)

    def test_cancel_during_status_confirmation_cannot_restore_completed_state(self):
        row = auth.Session("claude", "owner")
        popen = auth.subprocess.Popen
        def fake_process(_argv, **kwargs):
            return popen([sys.executable, "-c", "pass"], **kwargs)
        def confirmation(_adapter):
            row.terminate()
            return {"authenticated": True}
        with patch.object(auth.subprocess, "Popen", side_effect=fake_process), patch.object(auth, "status", side_effect=confirmation):
            row.run()
        self.assertEqual(row.state, "cancelled")
        self.assertIsNone(row.view()["url"])

    def test_claude_disables_tools_mcp_and_sessions(self):
        args = bridge.command("claude", "model-test")
        self.assertEqual(args[args.index("--tools") + 1], "")
        self.assertIn("--no-session-persistence", args)
        self.assertIn("mcp__*", args)

    def test_codex_is_ephemeral_readonly_and_disables_execution(self):
        args = bridge.command("codex", "model-test")
        self.assertIn("--ephemeral", args)
        self.assertIn("--ignore-user-config", args)
        self.assertIn("read-only", args)
        self.assertIn("features.shell_tool=false", args)
        self.assertIn("features.unified_exec=false", args)

    def test_arguments_are_not_shell_commands_and_invalid_adapters_fail(self):
        args = bridge.command("claude", "model; echo forbidden")
        self.assertEqual(args[-1], "model; echo forbidden")
        with self.assertRaises(ValueError):
            bridge.command("unknown", "model")
        with self.assertRaises(ValueError):
            bridge.command("claude", "model\nargument")


if __name__ == "__main__":
    unittest.main()
