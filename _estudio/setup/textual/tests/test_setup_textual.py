import json
import sys
import unittest
import asyncio
import tempfile
from pathlib import Path
from unittest import mock

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))

from setup_textual_app import (
    EXERCISM_TOKEN_ENV,
    EstudioSetupDesk,
    InstallerState,
    SetupCommand,
    build_core_command,
    build_core_environment,
    load_saved_exercism_token,
    parse_progress_event,
    parse_args,
    resolve_initial_alias,
)


class SetupTextualTests(unittest.TestCase):
    def test_build_core_command_uses_json_progress_and_strips_tui(self) -> None:
        command = SetupCommand(
            mode="reinstall",
            alias="axel",
            exercism_token="secret-token",
            change_github=True,
            only_step_ids=("git", "vscode-settings"),
            passthrough_args=("--tui", "--state-root", r"C:\Temp\Estudio"),
        )

        result = build_core_command(Path(r"C:\Setup\Estudio.Setup.exe"), command)

        self.assertEqual(str(result[0]), r"C:\Setup\Estudio.Setup.exe")
        self.assertIn("reinstall", result)
        self.assertIn("--events-json", result)
        self.assertIn("--alias", result)
        self.assertIn("axel", result)
        self.assertIn("--change-github", result)
        self.assertEqual(result.count("--only"), 2)
        self.assertNotIn("--tui", result)
        self.assertNotIn("secret-token", result)
        self.assertIn(r"C:\Temp\Estudio", result)

    def test_build_core_environment_passes_exercism_token_outside_command_line(self) -> None:
        command = SetupCommand(mode="repair", exercism_token="token-from-ui")

        env = build_core_environment(command, {"PATH": r"C:\Windows"})

        self.assertEqual(env[EXERCISM_TOKEN_ENV], "token-from-ui")
        self.assertEqual(env["ESTUDIO_SETUP_TEXTUAL_BYPASS"], "1")

    def test_build_core_environment_clears_stale_exercism_token_when_input_is_empty(self) -> None:
        command = SetupCommand(mode="verify", exercism_token="")

        env = build_core_environment(command, {EXERCISM_TOKEN_ENV: "old-token"})

        self.assertNotIn(EXERCISM_TOKEN_ENV, env)

    def test_parse_progress_event_accepts_json_lines(self) -> None:
        event = parse_progress_event(
            json.dumps(
                {
                    "type": "phase-finished",
                    "stepId": "git",
                    "phase": "verify",
                    "status": "ok",
                    "message": "Git listo.",
                    "success": True,
                }
            )
        )

        self.assertIsNotNone(event)
        self.assertEqual(event["type"], "phase-finished")
        self.assertEqual(event["stepId"], "git")

    def test_parse_args_defaults_to_verify_when_no_mode_is_provided(self) -> None:
        _, command = parse_args([])

        self.assertEqual(command.mode, "verify")

    def test_resolve_initial_alias_reads_identity_file_then_git_config(self) -> None:
        with tempfile.TemporaryDirectory() as temp_root:
            root = Path(temp_root)
            (root / ".git").mkdir()
            (root / ".git" / "config").write_text("[user]\n\tname = axel_git\n", encoding="utf-8")
            self.assertEqual(resolve_initial_alias(root), "axel_git")
            (root / ".estudio_usuario").write_text("axel_local\n", encoding="utf-8")
            self.assertEqual(resolve_initial_alias(root), "axel_local")

    def test_load_saved_exercism_token_reads_appdata_user_json(self) -> None:
        with tempfile.TemporaryDirectory() as temp_root:
            appdata = Path(temp_root) / "AppData"
            (appdata / "exercism").mkdir(parents=True)
            (appdata / "exercism" / "user.json").write_text('{"token": "secret-token"}', encoding="utf-8")

            with mock.patch.dict("os.environ", {}, clear=False):
                token = load_saved_exercism_token(str(appdata))

            self.assertEqual(token, "secret-token")

    def test_installer_state_tracks_progress_and_failed_steps(self) -> None:
        state = InstallerState(("git", "vscode-settings"))
        state.apply({"type": "phase-started", "stepId": "git", "phase": "detect"})
        state.apply(
            {
                "type": "phase-finished",
                "stepId": "git",
                "phase": "detect",
                "status": "ok",
                "message": "Git listo.",
                "success": True,
            }
        )
        state.apply(
            {
                "type": "phase-finished",
                "stepId": "vscode-settings",
                "phase": "verify",
                "status": "error",
                "message": "Settings rotos.",
                "success": False,
            }
        )

        self.assertEqual(state.completed_count, 1)
        self.assertEqual(state.failed_step_ids(), ("vscode-settings",))

    def test_installer_state_tracks_active_pipeline_phases(self) -> None:
        state = InstallerState(("git",))
        state.apply({"type": "phase-started", "stepId": "git", "phase": "detect"})
        state.apply(
            {
                "type": "phase-finished",
                "stepId": "git",
                "phase": "detect",
                "status": "ok",
                "message": "Git listo.",
                "success": True,
            }
        )
        state.apply({"type": "phase-started", "stepId": "git", "phase": "verify"})

        self.assertEqual(state.active_step_id, "git")
        self.assertEqual(
            state.phase_statuses_for("git"),
            {"detect": "ok", "action": "pending", "verify": "running"},
        )

    def test_setup_console_layout_uses_terminal_gui_inspired_sections(self) -> None:
        async def run() -> None:
            app = EstudioSetupDesk(Path("missing.exe"), SetupCommand(mode="package"))
            async with app.run_test() as pilot:
                await pilot.pause()
                for selector in (
                    "#brand-strip",
                    "#command-strip",
                    "#exercism-token-url",
                    "#exercism-token",
                    "#component-matrix",
                    "#pipeline",
                    "#step-inspector",
                    "#log-panel",
                    "#phase-detect",
                    "#phase-action",
                    "#phase-verify",
                ):
                    self.assertIsNotNone(app.query_one(selector))

        asyncio.run(run())

    def test_command_strip_uses_compact_ribbon_instead_of_terminal_buttons(self) -> None:
        async def run() -> None:
            app = EstudioSetupDesk(Path("missing.exe"), SetupCommand(mode="package"))
            async with app.run_test() as pilot:
                await pilot.pause()
                self.assertEqual(len(list(app.query("#command-strip Button"))), 0)
                ribbon = app.query_one("#command-ribbon")
                self.assertIn("[I]", ribbon.content)
                self.assertIn("Actualizar", ribbon.content)
                self.assertIn("[X]", ribbon.content)

        asyncio.run(run())

    def test_layout_keeps_pipeline_visible_and_status_non_empty(self) -> None:
        async def run() -> None:
            app = EstudioSetupDesk(Path("missing.exe"), SetupCommand(mode="package"))
            async with app.run_test() as pilot:
                await pilot.pause()
                self.assertGreaterEqual(app.query_one("#phase-row").size.height, 5)
                self.assertIn("Listo", app.query_one("#status").content)

        asyncio.run(run())

    def test_top_bar_uses_localized_mode_and_compact_token_label(self) -> None:
        async def run() -> None:
            app = EstudioSetupDesk(Path("missing.exe"), SetupCommand(mode="update", alias="axel"))
            async with app.run_test() as pilot:
                await pilot.pause()
                context = app.query_one("#run-context")
                token_label = app.query_one("#exercism-token-url")
                self.assertIn("modo actualizar", context.content)
                self.assertEqual(token_label.content, "EXERCISM TOKEN")

        asyncio.run(run())

    def test_wrapper_prefers_packaged_textual_executable(self) -> None:
        wrapper = (ROOT.parent / "Estudio.Setup.cmd").read_text(encoding="utf-8")

        self.assertIn("Estudio.Setup.Textual.exe", wrapper)
        self.assertIn("ESTUDIO_SETUP_TEXTUAL_BYPASS", wrapper)
        self.assertIn("Estudio.Setup.exe", wrapper)
        self.assertIn("import textual", wrapper)
        self.assertIn("py -3", wrapper)


if __name__ == "__main__":
    unittest.main()
