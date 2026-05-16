import json
import sys
import unittest
import asyncio
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))

from setup_textual_app import (
    EXERCISM_TOKEN_ENV,
    EstudioSetupDesk,
    InstallerState,
    SetupCommand,
    build_core_command,
    build_core_environment,
    parse_progress_event,
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
                self.assertIn("[X]", ribbon.content)

        asyncio.run(run())

    def test_wrapper_prefers_packaged_textual_executable(self) -> None:
        wrapper = (ROOT.parent / "Estudio.Setup.cmd").read_text(encoding="utf-8")

        self.assertIn("Estudio.Setup.Textual.exe", wrapper)
        self.assertIn("ESTUDIO_SETUP_TEXTUAL_BYPASS", wrapper)
        self.assertIn("Estudio.Setup.exe", wrapper)


if __name__ == "__main__":
    unittest.main()
