from __future__ import annotations

import argparse
import json
from pathlib import Path

from playwright.sync_api import sync_playwright


ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "tools" / "output" / "visual-parity"


def capture(url: str, output: Path) -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    output = output if output.is_absolute() else ROOT / output
    with sync_playwright() as p:
        browser = p.chromium.launch(headless=True)
        page = browser.new_page(viewport={"width": 624, "height": 720}, device_scale_factor=1)
        console_errors: list[str] = []
        page_errors: list[str] = []
        page.on("console", lambda message: console_errors.append(message.text) if message.type == "error" else None)
        page.on("pageerror", lambda error: page_errors.append(str(error)))
        page.goto(url, wait_until="domcontentloaded")
        page.wait_for_timeout(900)
        solo = page.get_by_role("button", name="Solo UI", exact=True)
        if solo.count() == 1:
            solo.click(timeout=5000)
            page.wait_for_timeout(150)
        hide = page.get_by_role("button", name="Ocultar panel de debug", exact=True)
        if hide.count() == 1:
            hide.click(timeout=5000)
            page.wait_for_timeout(150)
        stage = page.locator(".stage")
        box = stage.bounding_box()
        if box is None:
            raise RuntimeError("No .stage element found")
        page.screenshot(path=str(output), clip=box)
        (OUT / "capture_report.json").write_text(
            json.dumps(
                {
                    "url": url,
                    "output": str(output.relative_to(ROOT)),
                    "viewport": {"width": 624, "height": 720},
                    "stageBox": box,
                    "consoleErrors": console_errors,
                    "pageErrors": page_errors,
                },
                indent=2,
            ),
            encoding="utf-8",
        )
        browser.close()


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--url", default="http://127.0.0.1:5173/")
    parser.add_argument("--out", default=str(OUT / "current_ui.png"))
    args = parser.parse_args()
    capture(args.url, Path(args.out))


if __name__ == "__main__":
    main()
