from __future__ import annotations

import argparse
import json
import os
import subprocess
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Iterable

from textual import work
from textual.app import App, ComposeResult
from textual.containers import Horizontal, Vertical
from textual.widgets import Button, Footer, Header, Input, Label, ListItem, ListView, ProgressBar, RichLog, Static


DEFAULT_STEP_IDS = (
    "git",
    "github-cli",
    "github-auth",
    "git-identity",
    "git-safety-backup",
    "github-alias-rename",
    "local-alias",
    "github-fork",
    "git-remotes",
    "git-project-update",
    "node",
    "vscode",
    "vscode-settings",
    "vscode-extension-package",
    "vscode-extension",
    "powershell7",
    "msys2-toolchain",
    "user-path",
    "gemini-runtime-config",
    "exercise-catalog",
)

STATUS_LABELS = {
    "pending": "PENDIENTE",
    "running": "EN CURSO",
    "ok": "OK",
    "warning": "ADVERTENCIA",
    "missing": "FALTA",
    "error": "ERROR",
}

STATUS_CLASSES = {
    "pending": "pending",
    "running": "running",
    "ok": "ok",
    "warning": "warning",
    "missing": "bad",
    "error": "bad",
}

MODE_TOKENS = {"install", "update", "reinstall", "repair", "uninstall", "verify", "package"}


@dataclass(frozen=True)
class SetupCommand:
    mode: str
    alias: str = ""
    change_github: bool = False
    only_step_ids: tuple[str, ...] = ()
    passthrough_args: tuple[str, ...] = ()


@dataclass
class StepState:
    step_id: str
    status: str = "pending"
    phase: str = ""
    message: str = ""


class InstallerState:
    def __init__(self, step_ids: Iterable[str] = DEFAULT_STEP_IDS) -> None:
        self.steps: dict[str, StepState] = {step_id: StepState(step_id) for step_id in step_ids}
        self.log_lines: list[str] = []
        self.mode = "install"
        self.running = False
        self.success: bool | None = None
        self.state_path = ""
        self.log_path = ""
        self.report_path = ""

    @property
    def completed_count(self) -> int:
        return sum(1 for step in self.steps.values() if step.status in {"ok", "warning"})

    @property
    def total_count(self) -> int:
        return len(self.steps)

    def failed_step_ids(self) -> tuple[str, ...]:
        return tuple(step.step_id for step in self.steps.values() if step.status in {"missing", "error"})

    def reset_for_run(self, mode: str) -> None:
        self.mode = mode
        self.running = True
        self.success = None
        self.state_path = ""
        self.log_path = ""
        self.report_path = ""
        for step in self.steps.values():
            step.status = "pending"
            step.phase = ""
            step.message = ""

    def apply(self, event: dict[str, Any]) -> None:
        event_type = event.get("type")
        if event_type == "run-started":
            self.reset_for_run(str(event.get("mode", self.mode)).lower())
            self.log_lines.append(f"Modo: {event.get('mode', self.mode)}")
            return

        if event_type == "phase-started":
            step = self._step(str(event.get("stepId", "")))
            step.status = "running"
            step.phase = str(event.get("phase", ""))
            step.message = f"{step.phase}..."
            self.log_lines.append(f"{step.step_id}.{step.phase}: iniciando")
            return

        if event_type == "phase-finished":
            step = self._step(str(event.get("stepId", "")))
            step.status = str(event.get("status", "error"))
            step.phase = str(event.get("phase", ""))
            step.message = str(event.get("message", ""))
            self.log_lines.append(f"{step.step_id}.{step.phase}: {step.message}")
            return

        if event_type == "run-finished":
            self.running = False
            self.success = bool(event.get("success", False))
            self.log_lines.append("Resultado: OK" if self.success else "Resultado: ERROR")
            return

        if event_type == "artifacts":
            self.state_path = str(event.get("statePath", ""))
            self.log_path = str(event.get("logPath", ""))
            self.report_path = str(event.get("reportPath", ""))

    def _step(self, step_id: str) -> StepState:
        if step_id not in self.steps:
            self.steps[step_id] = StepState(step_id)
        return self.steps[step_id]


def parse_progress_event(line: str) -> dict[str, Any] | None:
    try:
        value = json.loads(line)
    except json.JSONDecodeError:
        return None
    if not isinstance(value, dict) or "type" not in value:
        return None
    return value


def build_core_command(core_path: Path, command: SetupCommand) -> list[str]:
    args = [str(core_path), command.mode, "--events-json"]
    for arg in command.passthrough_args:
        normalized = arg.strip().lower()
        if normalized in {"--tui", "-tui", "/tui", "--visual", "--events-json", "--json-progress"}:
            continue
        if normalized in MODE_TOKENS:
            continue
        args.append(arg)
    if command.alias.strip():
        args.extend(["--alias", command.alias.strip()])
    if command.change_github:
        args.append("--change-github")
    for step_id in command.only_step_ids:
        args.extend(["--only", step_id])
    return args


def resolve_core_path(explicit: str | None) -> Path:
    if explicit:
        return Path(explicit).expanduser().resolve()
    base = Path(sys.executable if getattr(sys, "frozen", False) else __file__).resolve().parent
    release_core = base / "Estudio.Setup.exe"
    if release_core.exists():
        return release_core
    repo_core = base.parent / "Estudio.Setup.cmd"
    if repo_core.exists():
        return repo_core
    return release_core


class EstudioSetupDesk(App):
    CSS = """
    Screen {
        background: #0d1113;
        color: #e9eef0;
    }

    Header {
        background: #16615f;
        color: white;
    }

    #layout {
        height: 1fr;
    }

    .panel {
        border: tall #2d4a50;
        padding: 1;
        background: #12181b;
        height: 100%;
    }

    #steps-panel {
        width: 38%;
    }

    #work-panel {
        width: 62%;
    }

    .panel-title {
        color: #8cebd8;
        text-style: bold;
        margin-bottom: 1;
    }

    ListView {
        height: 1fr;
        scrollbar-color: #8cebd8 #1d272b;
    }

    ListItem {
        height: 2;
        padding: 0 1;
    }

    ListItem.--highlight {
        background: #244a50;
        color: white;
    }

    Input {
        margin-bottom: 1;
    }

    Button {
        margin-right: 1;
        margin-bottom: 1;
        min-width: 14;
    }

    #status {
        height: 7;
        border: tall #2d4a50;
        padding: 1 2;
        background: #0d1215;
    }

    #progress {
        height: 3;
        padding: 0 2;
        background: #0d1215;
    }

    #log {
        height: 1fr;
        border: tall #2d4a50;
        padding: 1;
        background: #0d1215;
    }

    .pending {
        color: #89969c;
    }

    .running {
        color: #ffd166;
        text-style: bold;
    }

    .ok {
        color: #8cebd8;
    }

    .warning {
        color: #f7c56b;
    }

    .bad {
        color: #ff7c7c;
        text-style: bold;
    }
    """

    BINDINGS = [
        ("i", "run_install", "Instalar"),
        ("u", "run_update", "Actualizar"),
        ("r", "run_reinstall", "Reinstalar"),
        ("v", "run_verify", "Verificar"),
        ("x", "run_uninstall", "Desinstalar"),
        ("g", "change_github", "GitHub"),
        ("f", "retry_failed", "Fallidos"),
        ("q", "quit", "Salir"),
    ]

    def __init__(self, core_path: Path, initial_command: SetupCommand) -> None:
        super().__init__()
        self.core_path = core_path
        self.initial_command = initial_command
        self.state = InstallerState()
        self._started_initial = False

    def compose(self) -> ComposeResult:
        yield Header(show_clock=True)
        with Horizontal(id="layout"):
            with Vertical(classes="panel", id="steps-panel"):
                yield Static("Componentes", classes="panel-title")
                yield ListView(id="steps")
            with Vertical(classes="panel", id="work-panel"):
                yield Static("Instalador 2.0", classes="panel-title")
                yield Input(placeholder="Alias local, por ejemplo axel", id="alias")
                with Horizontal():
                    yield Button("Instalar", id="install", variant="primary")
                    yield Button("Actualizar", id="update")
                    yield Button("Reinstalar", id="reinstall")
                    yield Button("Verificar", id="verify")
                with Horizontal():
                    yield Button("Desinstalar", id="uninstall", variant="error")
                    yield Button("Cambiar GitHub", id="github")
                    yield Button("Reintentar fallidos", id="retry")
                    yield Button("Salir", id="quit")
                yield Static(id="status")
                yield ProgressBar(total=100, show_eta=False, id="progress")
                yield RichLog(id="log", wrap=True, highlight=True)
        yield Footer()

    def on_mount(self) -> None:
        self.title = "Estudio Socratico Setup 2.0"
        self.sub_title = str(self.core_path)
        self.query_one("#alias", Input).value = self.initial_command.alias
        self.refresh_steps()
        self.update_status("Listo", "Elige una accion o usa las teclas. El modo inicial corre automaticamente.")
        self.query_one("#alias", Input).focus()
        self.call_after_refresh(self.start_initial_run)

    def start_initial_run(self) -> None:
        if self._started_initial:
            return
        self._started_initial = True
        if self.initial_command.mode != "package":
            self.run_setup(self.initial_command)

    def on_button_pressed(self, event: Button.Pressed) -> None:
        button_id = event.button.id
        if button_id == "install":
            self.action_run_install()
        elif button_id == "update":
            self.action_run_update()
        elif button_id == "reinstall":
            self.action_run_reinstall()
        elif button_id == "verify":
            self.action_run_verify()
        elif button_id == "uninstall":
            self.action_run_uninstall()
        elif button_id == "github":
            self.action_change_github()
        elif button_id == "retry":
            self.action_retry_failed()
        elif button_id == "quit":
            self.exit()

    def action_run_install(self) -> None:
        self.run_setup(self.command("install"))

    def action_run_update(self) -> None:
        self.run_setup(self.command("update"))

    def action_run_reinstall(self) -> None:
        self.run_setup(self.command("reinstall"))

    def action_run_verify(self) -> None:
        self.run_setup(self.command("verify"))

    def action_run_uninstall(self) -> None:
        self.run_setup(self.command("uninstall"))

    def action_change_github(self) -> None:
        self.run_setup(self.command("update", change_github=True))

    def action_retry_failed(self) -> None:
        failed = self.state.failed_step_ids()
        if not failed:
            self.write_log("No hay pasos fallidos para reintentar.")
            return
        self.run_setup(self.command("repair", only_step_ids=failed))

    def command(
        self,
        mode: str,
        *,
        change_github: bool = False,
        only_step_ids: tuple[str, ...] = (),
    ) -> SetupCommand:
        return SetupCommand(
            mode=mode,
            alias=self.query_one("#alias", Input).value,
            change_github=change_github,
            only_step_ids=only_step_ids,
            passthrough_args=self.initial_command.passthrough_args,
        )

    def run_setup(self, command: SetupCommand) -> None:
        if self.state.running:
            self.write_log("Ya hay una ejecucion en curso.")
            return
        self.query_one("#log", RichLog).clear()
        self.run_setup_worker(command)

    @work(thread=True, exclusive=True)
    def run_setup_worker(self, command: SetupCommand) -> None:
        args = build_core_command(self.core_path, command)
        self.call_from_thread(self.write_log, "> " + " ".join(args))
        try:
            env = os.environ.copy()
            env["ESTUDIO_SETUP_TEXTUAL_BYPASS"] = "1"
            process = subprocess.Popen(
                args,
                stdout=subprocess.PIPE,
                stderr=subprocess.STDOUT,
                env=env,
                text=True,
                encoding="utf-8",
                errors="replace",
                bufsize=1,
            )
        except OSError as exc:
            self.call_from_thread(self.finish_with_launch_error, exc)
            return

        assert process.stdout is not None
        for raw_line in process.stdout:
            line = raw_line.strip()
            if not line:
                continue
            event = parse_progress_event(line)
            if event is None:
                self.call_from_thread(self.write_log, line)
            else:
                self.call_from_thread(self.apply_event, event)

        exit_code = process.wait()
        self.call_from_thread(self.finish_process, exit_code)

    def finish_with_launch_error(self, exc: OSError) -> None:
        self.state.running = False
        self.update_status("No pude abrir el backend", str(exc))
        self.write_log(f"ERROR: {exc}")

    def finish_process(self, exit_code: int) -> None:
        self.state.running = False
        if exit_code == 0:
            self.write_log("Proceso finalizo con codigo 0.")
        else:
            self.write_log(f"Proceso finalizo con codigo {exit_code}.")
        self.refresh_steps()
        self.refresh_progress()

    def apply_event(self, event: dict[str, Any]) -> None:
        self.state.apply(event)
        event_type = event.get("type")
        if event_type == "phase-started":
            self.update_status(
                f"{event.get('stepName', event.get('stepId'))}",
                f"{event.get('phase')} en curso...",
            )
        elif event_type == "phase-finished":
            self.update_status(
                f"{event.get('stepId')} · {STATUS_LABELS.get(str(event.get('status')), event.get('status'))}",
                str(event.get("message", "")),
            )
        elif event_type == "run-finished":
            self.update_status(
                "Resultado: OK" if event.get("success") else "Resultado: ERROR",
                f"Ultimo paso correcto: {event.get('lastSuccessfulStep', 'start')}",
            )
        elif event_type == "artifacts":
            self.update_status(
                "Artefactos de setup",
                f"Estado: {self.state.state_path}\nReporte: {self.state.report_path}",
            )
        for line in self.state.log_lines[-2:]:
            if line:
                self.write_log(line)
        self.refresh_steps()
        self.refresh_progress()

    def refresh_steps(self) -> None:
        view = self.query_one("#steps", ListView)
        view.clear()
        for step in self.state.steps.values():
            label = STATUS_LABELS.get(step.status, step.status)
            css = STATUS_CLASSES.get(step.status, "pending")
            text = f"[{css}]{label:>11}[/]  {step.step_id}"
            if step.phase:
                text += f"  [{step.phase}]"
            view.append(ListItem(Label(text)))

    def refresh_progress(self) -> None:
        bar = self.query_one("#progress", ProgressBar)
        total = max(1, self.state.total_count)
        progress = int((self.state.completed_count / total) * 100)
        bar.update(total=100, progress=progress)

    def update_status(self, title: str, detail: str) -> None:
        self.query_one("#status", Static).update(f"[b]{title}[/b]\n{detail}")

    def write_log(self, message: str) -> None:
        self.query_one("#log", RichLog).write(message)


def parse_args(argv: list[str]) -> tuple[Path, SetupCommand]:
    parser = argparse.ArgumentParser()
    parser.add_argument("mode", nargs="?", default="install")
    parser.add_argument("--core")
    parser.add_argument("--alias", default="")
    parser.add_argument("--change-github", action="store_true")
    known, passthrough = parser.parse_known_args(argv)
    mode = known.mode.lower()
    if mode not in MODE_TOKENS:
        passthrough = [known.mode, *passthrough]
        mode = "install"
    return resolve_core_path(known.core), SetupCommand(
        mode=mode,
        alias=known.alias,
        change_github=known.change_github,
        passthrough_args=tuple(passthrough),
    )


def main(argv: list[str] | None = None) -> int:
    core_path, command = parse_args(sys.argv[1:] if argv is None else argv)
    EstudioSetupDesk(core_path, command).run()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
