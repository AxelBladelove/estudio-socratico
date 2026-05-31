from __future__ import annotations

import json
from pathlib import Path

import cv2
import numpy as np
from PIL import Image, ImageDraw, ImageFont

from extract_color_fx_palette import ROIS


ROOT = Path(__file__).resolve().parents[1]
OUTPUT_DIR = ROOT / "tools" / "output" / "color-fx-calibration"
REFERENCE = ROOT / "assets" / "reference-cadenas.png"
BEFORE = OUTPUT_DIR / "before_ui.png"
AFTER = OUTPUT_DIR / "after_ui.png"


def load_rgb(path: Path) -> np.ndarray:
    return np.array(Image.open(path).convert("RGB"))


def resize_to(image: np.ndarray, size: tuple[int, int]) -> np.ndarray:
    width, height = size
    return cv2.resize(image, (width, height), interpolation=cv2.INTER_AREA)


def absdiff_rgb(a: np.ndarray, b: np.ndarray) -> np.ndarray:
    return cv2.absdiff(a, b)


def lab_delta(a: np.ndarray, b: np.ndarray) -> float:
    a_lab = cv2.cvtColor(a, cv2.COLOR_RGB2LAB).astype(np.float32)
    b_lab = cv2.cvtColor(b, cv2.COLOR_RGB2LAB).astype(np.float32)
    return float(np.mean(np.linalg.norm(a_lab - b_lab, axis=2)))


def rgb_mae(a: np.ndarray, b: np.ndarray) -> float:
    return float(np.mean(np.abs(a.astype(np.float32) - b.astype(np.float32))))


def scaled_roi(roi: tuple[int, int, int, int], width: int, height: int) -> tuple[int, int, int, int]:
    x, y, w, h = roi
    sx = width / 1086
    sy = height / 1448
    return (
        int(round(x * sx)),
        int(round(y * sy)),
        max(1, int(round(w * sx))),
        max(1, int(round(h * sy))),
    )


def crop(image: np.ndarray, roi: tuple[int, int, int, int]) -> np.ndarray:
    x, y, w, h = roi
    return image[y : y + h, x : x + w]


def metric_set(candidate: np.ndarray, reference: np.ndarray) -> dict:
    h, w = candidate.shape[:2]
    rois = {}
    for name, base_roi in ROIS.items():
        roi = scaled_roi(base_roi, w, h)
        c = crop(candidate, roi)
        r = crop(reference, roi)
        rois[name] = {
            "rgbMae": round(rgb_mae(c, r), 4),
            "labDelta": round(lab_delta(c, r), 4),
            "roi": {"x": roi[0], "y": roi[1], "w": roi[2], "h": roi[3]},
        }
    background_names = [name for name in rois if name.startswith("background_")]
    snake_names = [name for name in rois if "snake" in name]
    node_names = [name for name in rois if "node" in name]
    ui_names = [name for name in rois if name not in set(background_names)]
    return {
        "globalRgbMae": round(rgb_mae(candidate, reference), 4),
        "globalLabDelta": round(lab_delta(candidate, reference), 4),
        "backgroundRgbMae": round(float(np.mean([rois[name]["rgbMae"] for name in background_names])), 4),
        "snakeRgbMae": round(float(np.mean([rois[name]["rgbMae"] for name in snake_names])), 4),
        "nodeRgbMae": round(float(np.mean([rois[name]["rgbMae"] for name in node_names])), 4),
        "uiRgbMae": round(float(np.mean([rois[name]["rgbMae"] for name in ui_names])), 4),
        "rois": rois,
    }


def label_image(image: np.ndarray, label: str) -> Image.Image:
    pil = Image.fromarray(image).convert("RGB")
    draw = ImageDraw.Draw(pil)
    font = ImageFont.load_default()
    draw.rectangle((8, 8, 210, 32), fill=(0, 8, 12))
    draw.text((16, 14), label, fill=(234, 255, 255), font=font)
    return pil


def save_side_by_side(path: Path, images: list[tuple[str, np.ndarray]]) -> None:
    labeled = [label_image(image, label) for label, image in images]
    width = sum(image.width for image in labeled)
    height = max(image.height for image in labeled)
    canvas = Image.new("RGB", (width, height), "#010608")
    x = 0
    for image in labeled:
        canvas.paste(image, (x, 0))
        x += image.width
    canvas.save(path)


def heatmap(diff: np.ndarray) -> np.ndarray:
    gray = cv2.cvtColor(diff, cv2.COLOR_RGB2GRAY)
    return cv2.cvtColor(cv2.applyColorMap(gray, cv2.COLORMAP_TURBO), cv2.COLOR_BGR2RGB)


def main() -> None:
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    before = load_rgb(BEFORE)
    after = load_rgb(AFTER)
    reference = resize_to(load_rgb(REFERENCE), (after.shape[1], after.shape[0]))
    if before.shape[:2] != after.shape[:2]:
        before = resize_to(before, (after.shape[1], after.shape[0]))

    before_metrics = metric_set(before, reference)
    after_metrics = metric_set(after, reference)
    improved = after_metrics["globalLabDelta"] < before_metrics["globalLabDelta"]
    improvement_pct = (
        (before_metrics["globalLabDelta"] - after_metrics["globalLabDelta"]) / before_metrics["globalLabDelta"] * 100
        if before_metrics["globalLabDelta"]
        else 0.0
    )

    overlay = cv2.addWeighted(after, 0.5, reference, 0.5, 0)
    ui_diff = absdiff_rgb(after, reference)
    background_roi = scaled_roi(ROIS["background_radial_left"], after.shape[1], after.shape[0])
    bg_after = crop(after, background_roi)
    bg_ref = crop(reference, background_roi)
    bg_diff = absdiff_rgb(bg_after, bg_ref)

    save_side_by_side(OUTPUT_DIR / "background_comparison.png", [("reference bg", bg_ref), ("after bg", bg_after)])
    Image.fromarray(bg_diff).save(OUTPUT_DIR / "background_difference.png")
    Image.fromarray(overlay).save(OUTPUT_DIR / "ui_overlay_50.png")
    Image.fromarray(ui_diff).save(OUTPUT_DIR / "ui_difference.png")
    Image.fromarray(heatmap(ui_diff)).save(OUTPUT_DIR / "ui_difference_heatmap.png")
    label_image(before, "trial_001 baseline").save(OUTPUT_DIR / "trial_001.png")
    label_image(after, "trial_002 calibrated").save(OUTPUT_DIR / "trial_002.png")

    report = {
        "reference": str(REFERENCE.relative_to(ROOT)),
        "baseline": {
            "file": str(BEFORE.relative_to(ROOT)),
            "metrics": before_metrics,
        },
        "trials": [
            {
                "id": "trial_001",
                "file": str((OUTPUT_DIR / "trial_001.png").relative_to(ROOT)),
                "source": "baseline_before_calibration",
                "metrics": before_metrics,
            },
            {
                "id": "trial_002",
                "file": str((OUTPUT_DIR / "trial_002.png").relative_to(ROOT)),
                "source": "token_calibrated_after",
                "metrics": after_metrics,
            },
        ],
        "winner": "trial_002" if improved else "trial_001",
        "defaultRecommendation": "calibrated_after" if improved else "keep_baseline",
        "globalLabDeltaImprovementPct": round(improvement_pct, 4),
    }
    (OUTPUT_DIR / "fx_metric_report.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    (OUTPUT_DIR / "trial_report.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(json.dumps({
        "winner": report["winner"],
        "defaultRecommendation": report["defaultRecommendation"],
        "globalLabDeltaImprovementPct": report["globalLabDeltaImprovementPct"],
        "beforeGlobalLabDelta": before_metrics["globalLabDelta"],
        "afterGlobalLabDelta": after_metrics["globalLabDelta"],
    }, indent=2))


if __name__ == "__main__":
    main()
