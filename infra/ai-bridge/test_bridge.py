import importlib.util
import pathlib
import unittest

spec = importlib.util.spec_from_file_location("bridge", pathlib.Path(__file__).with_name("server.py"))
bridge = importlib.util.module_from_spec(spec)
spec.loader.exec_module(bridge)


class BridgeTests(unittest.TestCase):
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
