from __future__ import annotations

import argparse
import configparser
import json
import os
import re
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
    "exercism-cli",
    "msys2-toolchain",
    "user-path",
    "exercism-c-track",
    "gemini-runtime-config",
    "exercise-catalog",
)

EXERCISM_TOKEN_ENV = "ESTUDIO_EXERCISM_TOKEN"
EXERCISM_TOKEN_URL = "https://exercism.org/settings/api_cli"
LOCAL_ALIAS_ENV = "ESTUDIO_USUARIO"
ALIAS_PATTERN = re.compile(r"^[A-Za-z0-9_](?:[A-Za-z0-9_-]*[A-Za-z0-9_])?$")

STATUS_LABELS = {
    "pending": "PEND",
    "running": "RUN",
    "ok": "OK",
    "warning": "WARN",
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

MODE_LABELS = {
    "install": "instalar",
    "update": "actualizar",
    "reinstall": "reinstalar",
    "repair": "reparar",
    "uninstall": "desinstalar",
    "verify": "verificar",
    "package": "package",
}

MODE_TOKENS = {"install", "update", "reinstall", "repair", "uninstall", "verify", "package"}


@dataclass(frozen=True)
class SetupCommand:
    mode: str
    alias: str = ""
    exercism_token: str = ""
    change_github: bool = False
    only_step_ids: tuple[str, ...] = ()
    passthrough_args: tuple[str, ...] = ()


@dataclass
class StepState:
    step_id: str
    status: str = "pending"
    phase: str = ""
    message: str = ""
    phases: dict[str, str] | None = None


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
        self.active_step_id = ""

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
        self.active_step_id = ""
        for step in self.steps.values():
            step.status = "pending"
            step.phase = ""
            step.message = ""
            step.phases = None

    def phase_statuses_for(self, step_id: str) -> dict[str, str]:
        step = self._step(step_id)
        phases = step.phases or {}
        return {
            "detect": phases.get("detect", "pending"),
            "action": phases.get("action", "pending"),
            "verify": phases.get("verify", "pending"),
        }

    def apply(self, event: dict[str, Any]) -> None:
        event_type = event.get("type")
        if event_type == "run-started":
            self.reset_for_run(str(event.get("mode", self.mode)).lower())
            self.log_lines.append(f"Modo: {event.get('mode', self.mode)}")
            return

        if event_type == "phase-started":
            step = self._step(str(event.get("stepId", "")))
            self.active_step_id = step.step_id
            step.status = "running"
            step.phase = str(event.get("phase", ""))
            step.message = f"{step.phase}..."
            self._set_phase_status(step, step.phase, "running")
            self.log_lines.append(f"{step.step_id}.{step.phase}: iniciando")
            return

        if event_type == "phase-finished":
            step = self._step(str(event.get("stepId", "")))
            self.active_step_id = step.step_id
            step.status = str(event.get("status", "error"))
            step.phase = str(event.get("phase", ""))
            step.message = str(event.get("message", ""))
            self._set_phase_status(step, step.phase, step.status)
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

    @staticmethod
    def _set_phase_status(step: StepState, phase: str, status: str) -> None:
        if step.phases is None:
            step.phases = {}
        step.phases[phase_bucket(phase)] = status


def phase_bucket(phase: str) -> str:
    if phase == "detect":
        return "detect"
    if phase == "verify":
        return "verify"
    return "action"


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


def build_core_environment(command: SetupCommand, base_env: dict[str, str] | None = None) -> dict[str, str]:
    env = dict(os.environ if base_env is None else base_env)
    env["ESTUDIO_SETUP_TEXTUAL_BYPASS"] = "1"
    token = command.exercism_token.strip()
    if token:
        env[EXERCISM_TOKEN_ENV] = token
    else:
        env.pop(EXERCISM_TOKEN_ENV, None)
    return env


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


def looks_like_workspace_root(path: Path) -> bool:
    return (
        (path / ".git").exists()
        or (path / "_estudio" / "setup" / "Estudio.Setup.cmd").exists()
        or ((path / "Estudio.Setup.cmd").exists() and (path / "_estudio" / "setup").exists())
    )


def resolve_workspace_root(start_path: Path | None = None) -> Path:
    current = (start_path or Path.cwd()).resolve()
    fallback: Path | None = None
    while True:
        if (current / ".estudio_usuario").exists():
            return current
        if fallback is None and looks_like_workspace_root(current):
            fallback = current
        if current.parent == current:
            break
        current = current.parent
    return fallback or (start_path or Path.cwd()).resolve()


def is_valid_alias(value: str) -> bool:
    return bool(ALIAS_PATTERN.fullmatch(value.strip()))


def read_saved_alias(workspace_root: Path) -> str:
    identity_path = workspace_root / ".estudio_usuario"
    if not identity_path.exists():
        return ""
    try:
        return identity_path.read_text(encoding="utf-8").strip()
    except OSError:
        return ""


def read_git_config_alias(workspace_root: Path) -> str:
    config_path = workspace_root / ".git" / "config"
    if not config_path.exists():
        return ""
    parser = configparser.ConfigParser(interpolation=None)
    try:
        parser.read(config_path, encoding="utf-8")
    except (configparser.Error, OSError):
        return ""
    for section_name, key_name in (("github", "user"), ("user", "name")):
        value = parser.get(section_name, key_name, fallback="").strip()
        if is_valid_alias(value):
            return value
    return ""


def resolve_initial_alias(start_path: Path | None = None) -> str:
    workspace_root = resolve_workspace_root(start_path)
    for candidate in (
        read_saved_alias(workspace_root),
        os.environ.get(LOCAL_ALIAS_ENV, ""),
        read_git_config_alias(workspace_root),
        os.environ.get("USERNAME", ""),
        os.environ.get("USER", ""),
    ):
        if is_valid_alias(candidate):
            return candidate.strip()
    return "estudiante"


def load_saved_exercism_token(appdata_path: str | None = None) -> str:
    token = os.environ.get(EXERCISM_TOKEN_ENV, "").strip()
    if token:
        return token
    appdata_root = appdata_path or os.environ.get("APPDATA", "")
    if not appdata_root:
        return ""
    config_path = Path(appdata_root) / "exercism" / "user.json"
    if not config_path.exists():
        return ""
    try:
        value = json.loads(config_path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        return ""
    if not isinstance(value, dict):
        return ""
    token_value = str(value.get("token", "")).strip()
    return token_value


class EstudioSetupDesk(App):
    CSS = """
    Screen {
        background: #0b1016;
        color: #e8edf2;
    }

    Header {
        background: #18212b;
        color: #e8edf2;
    }

    #app-shell {
        height: 1fr;
        padding: 1 2;
    }

    #brand-strip {
        height: 4;
        border: hkey #35475c;
        background: #101923;
        padding: 0 2;
        margin-bottom: 1;
    }

    #brand-title {
        width: 34;
        content-align: left middle;
        color: #8bd8ff;
        text-style: bold;
    }

    #brand-subtitle {
        width: 32;
        content-align: left middle;
        color: #d9b76e;
    }

    #run-context {
        width: 1fr;
        content-align: right middle;
        color: #9ba8b5;
    }

    #command-strip {
        height: 4;
        border: tall #35475c;
        background: #0f171f;
        padding: 0 2;
        margin-bottom: 1;
    }

    #alias-box {
        width: 28;
        margin-right: 2;
    }

    #token-box {
        width: 48;
        margin-right: 2;
    }

    .field-label {
        height: 1;
        color: #8bd8ff;
        text-style: bold;
    }

    #alias {
        height: 1;
        width: 100%;
        border: none;
        background: #18212b;
        color: #e8edf2;
    }

    #exercism-token {
        height: 1;
        width: 100%;
        border: none;
        background: #18212b;
        color: #e8edf2;
    }

    #command-ribbon {
        width: 1fr;
        content-align: center middle;
        color: #cbd5df;
    }

    #command-ribbon .key {
        color: #8bd8ff;
        text-style: bold;
    }

    #command-ribbon .danger {
        color: #ff7a7a;
        text-style: bold;
    }

    #console-grid {
        height: 1fr;
    }

    .panel {
        border: tall #35475c;
        padding: 1;
        background: #101820;
        height: 100%;
    }

    .panel-title {
        color: #8bd8ff;
        text-style: bold;
        height: 1;
        margin-bottom: 1;
    }

    #component-matrix {
        width: 32%;
        margin-right: 1;
    }

    #operation-deck {
        width: 1fr;
        margin-right: 1;
    }

    #step-inspector {
        width: 27%;
    }

    ListView {
        height: 1fr;
        scrollbar-color: #8bd8ff #18212b;
    }

    ListItem {
        height: 1;
        padding: 0 1;
    }

    ListItem.--highlight {
        background: #26394c;
        color: #ffffff;
    }

    #pipeline {
        height: 10;
        border: hkey #35475c;
        padding: 0 2;
        background: #0d141c;
        margin-bottom: 1;
    }

    #phase-row {
        height: 5;
    }

    .phase-card {
        width: 1fr;
        border: round #35475c;
        padding: 0 1;
        margin-right: 1;
        content-align: center middle;
        background: #101923;
    }

    #phase-verify {
        margin-right: 0;
    }

    #progress {
        height: 2;
        margin-top: 1;
    }

    #status {
        height: 3;
        border: hkey #35475c;
        padding: 0 2;
        background: #0d141c;
        margin-bottom: 1;
        content-align: left middle;
    }

    #log-panel {
        height: 1fr;
    }

    #log {
        height: 1fr;
        background: #090f15;
    }

    #selected-step {
        height: 2;
        color: #8bd8ff;
        text-style: bold;
    }

    #selected-message {
        height: 1fr;
        color: #cbd5df;
    }

    #artifact-paths {
        height: 7;
        color: #9ba8b5;
    }

    .pending {
        color: #667483;
    }

    .running {
        color: #d9b76e;
        text-style: bold;
    }

    .ok {
        color: #77dfb2;
    }

    .warning {
        color: #e6b45c;
    }

    .bad {
        color: #ff7a7a;
        text-style: bold;
    }

    .cyan {
        color: #8bd8ff;
    }

    .muted {
        color: #9ba8b5;
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
        self.state.mode = initial_command.mode
        self._started_initial = False

    def compose(self) -> ComposeResult:
        yield Header(show_clock=True)
        with Vertical(id="app-shell"):
            with Horizontal(id="brand-strip"):
                yield Static("ESTUDIO SOCRATICO", id="brand-title")
                yield Static("Setup Console 2.0", id="brand-subtitle")
                yield Static(id="run-context")
            with Horizontal(id="command-strip"):
                with Vertical(id="alias-box"):
                    yield Static("ALIAS", classes="field-label")
                    yield Input(placeholder="alias local", id="alias")
                with Vertical(id="token-box"):
                    yield Static("EXERCISM TOKEN", classes="field-label", id="exercism-token-url")
                    yield Input(placeholder="pega token y reintenta fallidos", id="exercism-token", password=True)
                yield Static(
                    "[I] Instalar   [U] Actualizar   [R] Reinstalar   [V] Verificar   "
                    "[X] Desinstalar   [G] GitHub   [F] Fallidos   [Q] Salir",
                    id="command-ribbon",
                    markup=False,
                )
            with Horizontal(id="console-grid"):
                with Vertical(classes="panel", id="component-matrix"):
                    yield Static("Matriz de componentes", classes="panel-title")
                    yield ListView(id="steps")
                with Vertical(id="operation-deck"):
                    with Vertical(id="pipeline"):
                        yield Static("Pipeline de ejecucion", classes="panel-title")
                        with Horizontal(id="phase-row"):
                            yield Static(id="phase-detect", classes="phase-card")
                            yield Static(id="phase-action", classes="phase-card")
                            yield Static(id="phase-verify", classes="phase-card")
                        yield ProgressBar(total=100, show_eta=False, id="progress")
                    yield Static(id="status")
                    with Vertical(classes="panel", id="log-panel"):
                        yield Static("Salida del backend", classes="panel-title")
                        yield RichLog(id="log", wrap=True, highlight=True)
                with Vertical(classes="panel", id="step-inspector"):
                    yield Static("Inspector", classes="panel-title")
                    yield Static(id="selected-step")
                    yield Static(id="selected-message")
                    yield Static(id="artifact-paths")
        yield Footer()

    def on_mount(self) -> None:
        self.title = "Estudio Socratico Setup 2.0"
        self.sub_title = str(self.core_path)
        alias_value = self.initial_command.alias or resolve_initial_alias()
        token_value = self.initial_command.exercism_token or load_saved_exercism_token()
        self.query_one("#alias", Input).value = alias_value
        self.query_one("#exercism-token", Input).value = token_value
        self.refresh_steps()
        self.refresh_pipeline()
        self.refresh_inspector()
        self.update_context()
        self.update_status(
            "Listo",
            "La verificacion inicial corre automaticamente. Luego puedes instalar, actualizar, reinstalar o desinstalar.",
        )
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
            exercism_token=self.query_one("#exercism-token", Input).value,
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
            env = build_core_environment(command)
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
        self.refresh_pipeline()
        self.refresh_inspector()
        self.update_context()

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
                "Estado, log y reporte quedaron registrados en el perfil local.",
            )
        for line in self.state.log_lines[-2:]:
            if line:
                self.write_log(line)
        self.refresh_steps()
        self.refresh_progress()
        self.refresh_pipeline()
        self.refresh_inspector()
        self.update_context()

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

    def refresh_pipeline(self) -> None:
        active = self.active_or_relevant_step()
        phases = self.state.phase_statuses_for(active.step_id)
        self.query_one("#phase-detect", Static).update(self.phase_card("DETECTAR", phases["detect"]))
        self.query_one("#phase-action", Static).update(self.phase_card("EJECUTAR", phases["action"]))
        self.query_one("#phase-verify", Static).update(self.phase_card("VERIFICAR", phases["verify"]))

    def refresh_inspector(self) -> None:
        step = self.active_or_relevant_step()
        label = STATUS_LABELS.get(step.status, step.status)
        css = STATUS_CLASSES.get(step.status, "pending")
        phase = f" · {step.phase}" if step.phase else ""
        self.query_one("#selected-step", Static).update(f"[{css}]{label}[/]  {step.step_id}{phase}")
        self.query_one("#selected-message", Static).update(step.message or "Esperando actividad del backend.")
        paths = "\n".join(
            line
            for line in (
                f"Estado: {compact_path(self.state.state_path)}" if self.state.state_path else "",
                f"Log: {compact_path(self.state.log_path)}" if self.state.log_path else "",
                f"Reporte: {compact_path(self.state.report_path)}" if self.state.report_path else "",
            )
            if line
        )
        self.query_one("#artifact-paths", Static).update(paths or "Los artefactos apareceran aqui al finalizar.")

    def update_context(self) -> None:
        running = "EN CURSO" if self.state.running else "LISTO"
        failures = len(self.state.failed_step_ids())
        mode_label = MODE_LABELS.get(self.state.mode, self.state.mode)
        self.query_one("#run-context", Static).update(
            f"{running}  |  modo {mode_label}  |  {self.state.completed_count}/{self.state.total_count} ok  |  fallos {failures}"
        )

    def active_or_relevant_step(self) -> StepState:
        if self.state.active_step_id:
            return self.state._step(self.state.active_step_id)
        failed = self.state.failed_step_ids()
        if failed:
            return self.state._step(failed[0])
        return next(iter(self.state.steps.values()))

    @staticmethod
    def phase_card(title: str, status: str) -> str:
        label = STATUS_LABELS.get(status, status)
        css = STATUS_CLASSES.get(status, "pending")
        return f"[{css}]{title}[/]\n[{css}]{label}[/]"

    def update_status(self, title: str, detail: str) -> None:
        message = f"[b]{title}[/b]"
        if detail:
            message += f"  {detail}"
        self.query_one("#status", Static).update(message)

    def write_log(self, message: str) -> None:
        self.query_one("#log", RichLog).write(message)


def compact_path(value: str, max_length: int = 72) -> str:
    if len(value) <= max_length:
        return value
    head, tail = value[:24], value[-(max_length - 27) :]
    return f"{head}...{tail}"


def parse_args(argv: list[str]) -> tuple[Path, SetupCommand]:
    parser = argparse.ArgumentParser()
    parser.add_argument("mode", nargs="?", default="verify")
    parser.add_argument("--core")
    parser.add_argument("--alias", default="")
    parser.add_argument("--change-github", action="store_true")
    known, passthrough = parser.parse_known_args(argv)
    mode = known.mode.lower()
    if mode not in MODE_TOKENS:
        passthrough = [known.mode, *passthrough]
        mode = "verify"
    return resolve_core_path(known.core), SetupCommand(
        mode=mode,
        alias=known.alias or resolve_initial_alias(),
        exercism_token=load_saved_exercism_token(),
        change_github=known.change_github,
        passthrough_args=tuple(passthrough),
    )


def main(argv: list[str] | None = None) -> int:
    core_path, command = parse_args(sys.argv[1:] if argv is None else argv)
    EstudioSetupDesk(core_path, command).run()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
