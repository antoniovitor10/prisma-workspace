import importlib.util
import pathlib
import unittest
import sys
import time
import tempfile
import json
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

    def test_gemini_login_accepts_only_google_host_and_expires_in_five_minutes(self):
        self.assertEqual(auth.instructions("gemini", "https://accounts.google.com/o/oauth2/v2/auth?state=temporary"), ("https://accounts.google.com/o/oauth2/v2/auth?state=temporary", None))
        self.assertEqual(auth.instructions("gemini", "https://accounts.google.com.attacker.invalid/auth"), (None, None))
        row = auth.Session("gemini", "owner")
        self.assertLessEqual(row.expires - time.time(), 300)
        row.state = "waiting"
        self.assertTrue(row.view()["requiresCode"])

    def test_gemini_status_uses_verified_module_result_not_just_a_cache_file(self):
        from subprocess import CompletedProcess
        with patch.object(auth.shutil, "which", return_value="/bin/gemini"), patch.object(auth.subprocess, "run", return_value=CompletedProcess([], 0, b'PRISMA_GEMINI_AUTH={"authenticated":false}\n', b'')):
            self.assertFalse(auth.status("gemini")["authenticated"])
        with patch.object(auth.shutil, "which", return_value="/bin/gemini"), patch.object(auth.subprocess, "run", return_value=CompletedProcess([], 0, b'PRISMA_GEMINI_AUTH={"authenticated":true}\n', b'')):
            self.assertTrue(auth.status("gemini")["authenticated"])

    def test_google_environment_drops_api_keys_adc_and_oauth_overrides(self):
        keys = ["GEMINI_API_KEY", "GOOGLE_API_KEY", "GOOGLE_APPLICATION_CREDENTIALS", "GOOGLE_CLOUD_ACCESS_TOKEN", "GEMINI_API_BASE_URL", "GEMINI_FORCE_AUTH_TYPE"]
        with patch.dict(auth.os.environ, dict.fromkeys(keys, "private")):
            env = auth.environment("/tmp/isolated")
        for key in keys:
            self.assertNotIn(key, env)
        self.assertEqual(env["NO_BROWSER"], "true")

    def test_gemini_chat_uses_temporary_config_with_zero_tools_and_only_oauth_cache(self):
        with tempfile.TemporaryDirectory() as private, tempfile.TemporaryDirectory() as directory:
            folder = pathlib.Path(private) / ".gemini"; folder.mkdir()
            (folder / "oauth_creds.json").write_text('{"refresh_token":"fake-private"}')
            (folder / "settings.json").write_text('{"mcpServers":{"unsafe":{}}}')
            with patch.dict(auth.os.environ, {"GEMINI_AUTH_HOME": private}):
                env = bridge.gemini_chat_environment(directory)
            config = json.loads((pathlib.Path(directory) / ".gemini" / "settings.json").read_text())
            self.assertEqual(env["GEMINI_CLI_HOME"], directory)
            self.assertEqual(config["tools"]["core"], [])
            self.assertEqual(config["mcpServers"], {})
            self.assertFalse(config["hooksConfig"]["enabled"])
            self.assertFalse(config["experimental"]["enableAgents"])
            args = bridge.command("gemini", "auto")
            self.assertEqual(args[args.index("--extensions") + 1], "none")
            self.assertIn("/bridge/gemini-deny-tools.toml", args)
            self.assertNotIn("--yolo", args)

    def test_gemini_json_preserves_response_and_validates_token_statistics(self):
        text, usage = bridge.gemini_result(json.dumps({"response":" Resposta ","stats":{"models":{"gemini":{"tokens":{"input":20,"candidates":8}}}}}))
        self.assertEqual(text, "Resposta")
        self.assertEqual(usage, {"prompt_tokens":20,"completion_tokens":8})
        with self.assertRaises(ValueError):
            bridge.gemini_result('{"response":"text","error":{"code":403}}')

    def test_google_code_is_sent_only_once_and_cancellation_keeps_credentials_private(self):
        from unittest.mock import Mock
        row = auth.Session("gemini", "owner"); row.state = "waiting"; row.process = Mock(); row.process.poll.return_value = None
        row.complete("temporary-code")
        row.process.stdin.write.assert_called_once_with(b"temporary-code\n")
        with self.assertRaises(ValueError):
            row.complete("second-code")

    def test_gemini_missing_statistics_estimates_usage_instead_of_bypassing_token_quota(self):
        text, usage = bridge.gemini_result('{"response":"Resposta"}', "Pergunta")
        self.assertEqual(text, "Resposta")
        self.assertEqual(usage, {"prompt_tokens": 3, "completion_tokens": 3})
        _, partial = bridge.gemini_result('{"response":"Resposta","stats":{"models":{"gemini":{"tokens":{"input":20,"candidates":-1}}}}}', "Pergunta")
        self.assertEqual(partial, {"prompt_tokens": 20, "completion_tokens": 3})

    def test_gemini_promotes_only_the_official_cache_into_its_private_volume(self):
        with tempfile.TemporaryDirectory() as private, tempfile.TemporaryDirectory() as directory:
            folder = pathlib.Path(directory) / ".gemini"; folder.mkdir()
            (folder / "oauth_creds.json").write_text('{"refresh_token":"fake-private"}')
            (folder / "settings.json").write_text('{"unsafe":"configuration"}')
            with patch.dict(auth.os.environ, {"GEMINI_AUTH_HOME": private}):
                auth.persist_gemini_login(directory)
            self.assertEqual([p.name for p in (pathlib.Path(private) / ".gemini").iterdir()], ["oauth_creds.json"])


if __name__ == "__main__":
    unittest.main()
