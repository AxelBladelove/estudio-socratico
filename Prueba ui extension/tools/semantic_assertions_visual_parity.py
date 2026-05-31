from __future__ import annotations

import json
from pathlib import Path

from playwright.sync_api import sync_playwright


ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "tools" / "output" / "visual-parity"
REPORT = OUT / "semantic_assertions_report.json"


def rect_payload(locator):
    box = locator.bounding_box()
    if box is None:
        return None
    return {key: round(float(value), 3) for key, value in box.items()}


def center_y(box: dict) -> float:
    return box["y"] + box["height"] / 2


def assertion(name: str, passed: bool, severity: str, details: dict) -> dict:
    return {"name": name, "passed": bool(passed), "severity": severity, "details": details}


def run(url: str = "http://127.0.0.1:5173/") -> dict:
    OUT.mkdir(parents=True, exist_ok=True)
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
        stage_box = page.locator(".stage").bounding_box()
        stage_scale = (stage_box["width"] / 1086) if stage_box else 1

        checks: list[dict] = []
        next_text = page.locator('[data-cal-id="bottomNav.nextText"]')
        lesson_text = page.locator('[data-cal-id="bottomNav.lessonText"]')
        menu_icon = page.locator('[data-cal-id="bottomNav.menuIcon"]')
        arrow_button = page.locator('[data-cal-id="bottomNav.arrowButton"]')
        arrow_icon = page.locator('[data-cal-id="bottomNav.arrowIcon"]')

        next_box = rect_payload(next_text)
        lesson_box = rect_payload(lesson_text)
        menu_box = rect_payload(menu_icon)
        arrow_box = rect_payload(arrow_button)
        arrow_icon_box = rect_payload(arrow_icon)
        gap = None
        vertical_delta = None
        if next_box and lesson_box:
            gap = round(lesson_box["x"] - (next_box["x"] + next_box["width"]), 3)
            vertical_delta = round(abs(center_y(next_box) - center_y(lesson_box)), 3)
        bottom_nav_text = page.locator(".bottom-nav").inner_text(timeout=5000).replace("\n", " ")
        checks.append(
            assertion(
                "bottom_nav_next_button",
                bool(
                    next_text.count() == 1
                    and lesson_text.count() == 1
                    and "SiguienteFunciones string" not in bottom_nav_text
                    and gap is not None
                    and 4 <= gap <= 16
                    and vertical_delta is not None
                    and vertical_delta <= 3
                    and menu_box is not None
                    and arrow_box is not None
                    and arrow_icon_box is not None
                    and 54 * stage_scale <= arrow_box["width"] <= 72 * stage_scale
                    and 54 * stage_scale <= arrow_box["height"] <= 72 * stage_scale
                ),
                "critical",
                {
                    "stageScale": round(stage_scale, 4),
                    "text": bottom_nav_text,
                    "nextTextBox": next_box,
                    "lessonTextBox": lesson_box,
                    "gapPx": gap,
                    "verticalCenterDeltaPx": vertical_delta,
                    "menuIconBox": menu_box,
                    "arrowButtonBox": arrow_box,
                    "arrowIconBox": arrow_icon_box,
                    "expectedGapPx": {"min": 4, "max": 16},
                },
            )
        )

        cta_box = page.locator('[data-cal-id="cta.box"]')
        cta_text = page.locator('[data-cal-id="cta.text"]')
        cta_box_rect = rect_payload(cta_box)
        cta_text_rect = rect_payload(cta_text)
        cta_color = cta_text.evaluate("el => getComputedStyle(el).color") if cta_text.count() == 1 else None
        checks.append(
            assertion(
                "current_lesson_cta",
                bool(
                    cta_box.count() == 1
                    and cta_text.count() == 1
                    and cta_text.inner_text(timeout=5000).strip() == "SIGUE AQUÍ"
                    and cta_box.is_visible()
                    and cta_text.is_visible()
                    and cta_box_rect
                    and cta_text_rect
                    and cta_box_rect["width"] >= 110 * stage_scale
                    and cta_text_rect["width"] >= 55 * stage_scale
                    and cta_text_rect["height"] > 8 * stage_scale
                    and cta_color not in {None, "rgba(0, 0, 0, 0)"}
                ),
                "critical",
                {
                    "box": cta_box_rect,
                    "textBox": cta_text_rect,
                    "stageScale": round(stage_scale, 4),
                    "text": cta_text.inner_text(timeout=5000).strip() if cta_text.count() == 1 else "",
                    "computedColor": cta_color,
                },
            )
        )

        segments = page.locator('[data-cal-id^="progress.segment."]')
        segment_items = []
        for index in range(segments.count()):
            item = segments.nth(index)
            box = rect_payload(item)
            cls = item.get_attribute("class")
            segment_items.append({"index": index + 1, "class": cls, "box": box})
        active = [item for item in segment_items if item["class"] and ("done" in item["class"] or "partial" in item["class"])]
        gaps = []
        for left, right in zip(segment_items, segment_items[1:]):
            if left["box"] and right["box"]:
                gaps.append(round(right["box"]["x"] - (left["box"]["x"] + left["box"]["width"]), 3))
        heights = [item["box"]["height"] for item in segment_items if item["box"]]
        checks.append(
            assertion(
                "progress_done_segments",
                bool(
                    len(segment_items) == 14
                    and len(active) == 5
                    and all(3 <= gap <= 7 for gap in gaps[:8])
                    and heights
                    and all(4 <= height <= 8 for height in heights)
                ),
                "critical",
                {
                    "totalSegments": len(segment_items),
                    "activeSegments": len(active),
                    "gapsPx": gaps,
                    "heightsPx": heights,
                    "segments": segment_items,
                },
            )
        )

        debug_panel_count = page.locator(".debug-panel").count()
        debug_reveal_count = page.locator(".debug-reveal").count()
        debug_visible_text = page.locator("body").inner_text(timeout=5000)
        checks.append(
            assertion(
                "debug_contamination",
                bool(debug_panel_count == 0 and debug_reveal_count == 0 and "DEBUG" not in debug_visible_text.upper()),
                "critical",
                {
                    "debugPanelCount": debug_panel_count,
                    "debugRevealCount": debug_reveal_count,
                    "bodyContainsDebug": "DEBUG" in debug_visible_text.upper(),
                },
            )
        )

        primary_text_targets = {
            "mainTitle.text": "Cadenas de caracteres",
            "subtitle.text": "char, strings y texto",
            "label.hello.title": "Hola mundo",
            "label.strings.title": "Strings básicos",
            "label.cli.title": "Reto: agenda CLI",
        }
        text_details = {}
        text_ok = True
        for cal_id, expected in primary_text_targets.items():
            loc = page.locator(f'[data-cal-id="{cal_id}"]')
            box = rect_payload(loc)
            text = loc.inner_text(timeout=5000).strip() if loc.count() == 1 else ""
            ok = loc.count() == 1 and loc.is_visible() and expected in text and box is not None and box["width"] > 20 and box["height"] > 8
            text_ok = text_ok and ok
            text_details[cal_id] = {"expected": expected, "text": text, "box": box, "ok": ok}
        checks.append(assertion("primary_text_integrity", text_ok, "critical", text_details))

        critical_failed = [check for check in checks if check["severity"] == "critical" and not check["passed"]]
        report = {
            "url": url,
            "passed": len(critical_failed) == 0,
            "criticalFailed": [check["name"] for check in critical_failed],
            "assertions": checks,
            "consoleErrors": console_errors,
            "pageErrors": page_errors,
        }
        REPORT.write_text(json.dumps(report, indent=2), encoding="utf-8")
        browser.close()
        return report


def main() -> None:
    report = run()
    print(json.dumps({"passed": report["passed"], "criticalFailed": report["criticalFailed"]}, indent=2))


if __name__ == "__main__":
    main()
