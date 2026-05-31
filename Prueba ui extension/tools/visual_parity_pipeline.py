from __future__ import annotations

import json
import math
import re
import shutil
from dataclasses import dataclass, asdict
from pathlib import Path

import cv2
import numpy as np
from PIL import Image, ImageDraw, ImageFont
from skimage.color import deltaE_ciede2000, rgb2lab
from skimage.metrics import structural_similarity


ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "tools" / "output" / "visual-parity"
REFERENCE = OUT / "reference.png"
BASELINE = OUT / "baseline_ui.png"
CURRENT = OUT / "current_ui.png"


@dataclass(frozen=True)
class Roi:
    name: str
    x: int
    y: int
    w: int
    h: int
    priority: float
    group: str
    tier: str = "primary"


ROIS: list[Roi] = [
    Roi("background_top_left", 0, 130, 260, 330, 1.5, "background"),
    Roi("background_center", 30, 500, 260, 520, 1.6, "background"),
    Roi("background_right", 710, 310, 300, 520, 1.2, "background"),
    Roi("header_back_button", 36, 49, 246, 70, 1.0, "header"),
    Roi("header_modules_button", 871, 55, 174, 65, 1.0, "header"),
    Roi("title_text", 318, 70, 510, 53, 0.9, "text"),
    Roi("module_subtitle_text", 318, 38, 440, 96, 0.75, "text"),
    Roi("progress_panel", 59, 189, 968, 126, 1.6, "panel"),
    Roi("progress_done_segments", 88, 270, 335, 20, 1.2, "progress"),
    Roi("progress_pending_segments", 455, 270, 515, 20, 1.0, "progress"),
    Roi("code_ghost_text", 24, 515, 250, 190, 0.55, "text"),
    Roi("completed_nodes", 235, 340, 180, 225, 1.8, "nodes"),
    Roi("current_node", 374, 610, 150, 145, 1.8, "nodes"),
    Roi("locked_nodes_upper", 280, 775, 150, 225, 1.5, "nodes"),
    Roi("star_node", 395, 1010, 120, 120, 1.2, "nodes"),
    Roi("locked_node_bottom", 350, 1130, 125, 125, 1.2, "nodes"),
    Roi("snake_active", 260, 365, 180, 295, 1.8, "snake"),
    Roi("snake_locked", 285, 730, 205, 480, 1.7, "snake"),
    Roi("lesson_labels_completed", 350, 365, 275, 200, 1.0, "text"),
    Roi("lesson_labels_current", 515, 645, 220, 115, 1.1, "text"),
    Roi("lesson_labels_locked", 390, 790, 360, 430, 1.0, "text"),
    Roi("bottom_nav_panel", 72, 1300, 940, 100, 1.6, "bottom_nav"),
    Roi("bottom_nav_next_button", 925, 1318, 63, 63, 1.2, "bottom_nav"),
    Roi("sparkles_flares", 900, 210, 140, 350, 0.6, "sparkles"),
    Roi("abc_area_secondary", 650, 350, 390, 430, 0.2, "abc", "secondary"),
]


def load_rgb(path: Path) -> np.ndarray:
    return np.array(Image.open(path).convert("RGB"))


def save_rgb(path: Path, image: np.ndarray) -> None:
    Image.fromarray(np.clip(image, 0, 255).astype(np.uint8), "RGB").save(path)


def resize_reference(reference: np.ndarray, target: np.ndarray) -> np.ndarray:
    height, width = target.shape[:2]
    return cv2.resize(reference, (width, height), interpolation=cv2.INTER_AREA)


def scale_roi(roi: Roi, width: int, height: int) -> tuple[int, int, int, int]:
    sx = width / 1086
    sy = height / 1448
    return (
        max(0, int(round(roi.x * sx))),
        max(0, int(round(roi.y * sy))),
        max(1, int(round(roi.w * sx))),
        max(1, int(round(roi.h * sy))),
    )


def crop(image: np.ndarray, box: tuple[int, int, int, int]) -> np.ndarray:
    x, y, w, h = box
    return image[y : min(image.shape[0], y + h), x : min(image.shape[1], x + w)]


def delta_e(candidate: np.ndarray, reference: np.ndarray) -> np.ndarray:
    cand = rgb2lab(candidate / 255.0)
    ref = rgb2lab(reference / 255.0)
    return deltaE_ciede2000(cand, ref)


def roi_metrics(candidate: np.ndarray, reference: np.ndarray) -> dict:
    de = delta_e(candidate, reference)
    diff = np.abs(candidate.astype(np.float32) - reference.astype(np.float32))
    gray_c = cv2.cvtColor(candidate, cv2.COLOR_RGB2GRAY)
    gray_r = cv2.cvtColor(reference, cv2.COLOR_RGB2GRAY)
    edges_c = cv2.Canny(gray_c, 45, 120)
    edges_r = cv2.Canny(gray_r, 45, 120)
    ssim = structural_similarity(gray_c, gray_r, data_range=255) if min(gray_c.shape[:2]) >= 7 else 0.0
    hist_distance = 0.0
    for channel in range(3):
        h_c = cv2.calcHist([candidate], [channel], None, [32], [0, 256])
        h_r = cv2.calcHist([reference], [channel], None, [32], [0, 256])
        cv2.normalize(h_c, h_c)
        cv2.normalize(h_r, h_r)
        hist_distance += float(cv2.compareHist(h_c, h_r, cv2.HISTCMP_BHATTACHARYYA))
    hist_distance /= 3
    luminance_c = gray_c.astype(np.float32)
    luminance_r = gray_r.astype(np.float32)
    return {
        "deltaE2000Mean": round(float(np.mean(de)), 4),
        "deltaE2000P95": round(float(np.percentile(de, 95)), 4),
        "rgbMae": round(float(np.mean(diff)), 4),
        "ssim": round(float(ssim), 4),
        "edgeDifference": round(float(np.mean(np.abs(edges_c.astype(np.float32) - edges_r.astype(np.float32))) / 255), 4),
        "luminanceDifference": round(float(np.mean(np.abs(luminance_c - luminance_r))), 4),
        "histogramDistance": round(float(hist_distance), 4),
    }


def roi_score(metrics: dict) -> float:
    return (
        metrics["deltaE2000Mean"]
        + 0.22 * metrics["deltaE2000P95"]
        + 0.2 * metrics["rgbMae"]
        + 9.0 * metrics["edgeDifference"]
        + 0.12 * metrics["luminanceDifference"]
        + 8.0 * metrics["histogramDistance"]
        + 10.0 * (1.0 - metrics["ssim"])
    )


def score_image(candidate: np.ndarray, reference: np.ndarray) -> dict:
    height, width = candidate.shape[:2]
    rows = []
    weighted_primary = 0.0
    weight_primary = 0.0
    weighted_all = 0.0
    weight_all = 0.0
    for roi in ROIS:
        box = scale_roi(roi, width, height)
        cand_crop = crop(candidate, box)
        ref_crop = crop(reference, box)
        metrics = roi_metrics(cand_crop, ref_crop)
        score = round(roi_score(metrics), 4)
        row = {
            **asdict(roi),
            "scaled": {"x": box[0], "y": box[1], "w": box[2], "h": box[3]},
            "score": score,
            "metrics": metrics,
        }
        rows.append(row)
        weighted_all += score * roi.priority
        weight_all += roi.priority
        if roi.tier == "primary":
            weighted_primary += score * roi.priority
            weight_primary += roi.priority
    return {
        "primaryWeightedScore": round(weighted_primary / weight_primary, 4),
        "allWeightedScore": round(weighted_all / weight_all, 4),
        "global": roi_metrics(candidate, reference),
        "rois": rows,
    }


def comparison_report(before_metrics: dict, after_metrics: dict) -> dict:
    before_by_name = {row["name"]: row for row in before_metrics["rois"]}
    rows = []
    primary_worse = []
    primary_better = []
    secondary_worse = []
    for after in after_metrics["rois"]:
        before = before_by_name[after["name"]]
        delta = round(after["score"] - before["score"], 4)
        improvement = round(before["score"] - after["score"], 4)
        pct = round((improvement / before["score"]) * 100, 4) if before["score"] else 0.0
        item = {
            "name": after["name"],
            "group": after["group"],
            "tier": after["tier"],
            "priority": after["priority"],
            "beforeScore": before["score"],
            "afterScore": after["score"],
            "improvement": improvement,
            "improvementPct": pct,
            "afterDeltaE2000Mean": after["metrics"]["deltaE2000Mean"],
            "afterRgbMae": after["metrics"]["rgbMae"],
            "afterSsim": after["metrics"]["ssim"],
        }
        rows.append(item)
        if after["tier"] == "primary":
            if delta > 0.15:
                primary_worse.append(item)
            elif delta < -0.15:
                primary_better.append(item)
        elif delta > 0.15:
            secondary_worse.append(item)
    primary_score_delta = round(after_metrics["primaryWeightedScore"] - before_metrics["primaryWeightedScore"], 4)
    clear_accept = primary_score_delta < 0 and len(primary_worse) == 0
    repair_candidate = primary_score_delta < 0 and 0 < len(primary_worse) <= 2
    return {
        "primaryWeightedBefore": before_metrics["primaryWeightedScore"],
        "primaryWeightedAfter": after_metrics["primaryWeightedScore"],
        "primaryWeightedImprovement": round(-primary_score_delta, 4),
        "primaryWeightedImprovementPct": round((-primary_score_delta / before_metrics["primaryWeightedScore"]) * 100, 4)
        if before_metrics["primaryWeightedScore"]
        else 0.0,
        "allWeightedBefore": before_metrics["allWeightedScore"],
        "allWeightedAfter": after_metrics["allWeightedScore"],
        "primaryRoisImprovedCount": len(primary_better),
        "primaryRoisWorsenedCount": len(primary_worse),
        "secondaryRoisWorsenedCount": len(secondary_worse),
        "primaryWorsened": primary_worse,
        "secondaryWorsened": secondary_worse,
        "roiRows": rows,
        "acceptAsDefault": clear_accept,
        "candidateAcceptedWithLocalRepairRequired": repair_candidate,
        "acceptanceRule": "staged: clear default when primary weighted score improves with no primary ROI worsening; candidate when weighted score improves and <=2 localized primary ROIs need repair",
    }


RESPONSIBLE_TOKENS = {
    "background": ["--bg-base", "--bg-radial-left", "--bg-radial-right", "--glow-soft", "--shadow-deep"],
    "header": ["--header-button-bg", "--panel-border", "--cyan-core", "--glow-strong"],
    "panel": ["--panel-bg", "--panel-bg-lift", "--panel-border", "--cyan-core"],
    "progress": ["--progress-active-start", "--progress-active-end", "--progress-active-glow", "--node-lock-core"],
    "bottom_nav": ["--bottom-nav-bg", "--panel-border", "--cyan-core", "--bottom nav button border/background/shadow"],
    "nodes": ["--node-green-core", "--node-green-edge", "--current-ring", "--node-lock-core", "--node-lock-edge"],
    "snake": ["SNAKE_CONFIG.*", "--cyan-glow", "--snake-lock-core", "--snake-lock-glow"],
    "text": ["--text-hot", "--text-main", "--text-muted", "--text-cyan"],
    "sparkles": ["--sparkle-core", "--abc-glow"],
    "abc": ["--abc-glow", "--text-hot", "--panel-bg"],
}


STAGED_PHASES = [
    {
        "phase": "A_global_visual_tone",
        "scope": ["background", "vignette", "ambient glow", "global cyan intensity", "global contrast", "global bloom"],
        "frozen": ["progress/button/node-specific tokens", "snake path geometry"],
    },
    {
        "phase": "B_panels_buttons",
        "scope": ["back button", "modules button", "progress panel", "bottom nav", "next button"],
        "frozen": ["background candidate tokens"],
    },
    {
        "phase": "C_nodes",
        "scope": ["completed nodes", "current node", "locked nodes", "star node"],
        "frozen": ["background", "panels/buttons"],
    },
    {
        "phase": "D_snake_fx",
        "scope": ["snake active glow", "snake locked glow", "stroke opacity", "highlight", "blur", "blend mode"],
        "frozen": ["path d", "node/layout geometry"],
    },
    {
        "phase": "E_text",
        "scope": ["text color", "text shadow", "muted labels", "ghost code text"],
        "frozen": ["layout", "font sizes", "labels"],
    },
    {
        "phase": "F_local_repair",
        "scope": ["localized worsened primary ROIs from previous phase"],
        "frozen": ["global candidate improvements outside the repair ROI"],
    },
    {
        "phase": "codex_vlm_visual_judge",
        "scope": ["reference", "candidate", "overlay", "heatmap", "ROI score table", "semantic assertions"],
        "frozen": ["no external VLM/API; Codex performs the visual judgment in-session"],
    },
]


def trial_reports(baseline_metrics: dict, reference: np.ndarray) -> dict:
    trials = []
    accepted = []
    candidates = []
    for path in sorted(OUT.glob("trial_*.png")):
        image = load_rgb(path)
        if image.shape[:2] != reference.shape[:2]:
            image = cv2.resize(image, (reference.shape[1], reference.shape[0]), interpolation=cv2.INTER_AREA)
        metrics = score_image(image, reference)
        comparison = comparison_report(baseline_metrics, metrics)
        status = "rejected"
        if comparison["acceptAsDefault"]:
            status = "accepted_checkpoint"
        elif comparison["candidateAcceptedWithLocalRepairRequired"]:
            status = "candidate_accepted_with_local_repair_required"
        elif comparison["primaryWeightedImprovement"] > 0:
            status = "rejected"
        item = {
            "id": path.stem,
            "file": str(path.relative_to(ROOT)),
            "status": status,
            "primaryWeightedScore": metrics["primaryWeightedScore"],
            "primaryWeightedImprovement": comparison["primaryWeightedImprovement"],
            "primaryWeightedImprovementPct": comparison["primaryWeightedImprovementPct"],
            "primaryRoisWorsenedCount": comparison["primaryRoisWorsenedCount"],
            "acceptAsDefault": comparison["acceptAsDefault"],
            "candidateAcceptedWithLocalRepairRequired": comparison["candidateAcceptedWithLocalRepairRequired"],
            "primaryWorsened": comparison["primaryWorsened"],
            "probableResponsibleTokens": {
                row["name"]: RESPONSIBLE_TOKENS.get(row["group"], [])
                for row in comparison["primaryWorsened"]
            },
            "localRepairRequired": [row["name"] for row in comparison["primaryWorsened"]],
        }
        trials.append(item)
        if comparison["acceptAsDefault"]:
            accepted.append((metrics["primaryWeightedScore"], path, item))
        elif comparison["candidateAcceptedWithLocalRepairRequired"]:
            candidates.append((metrics["primaryWeightedScore"], path, item))
    if accepted:
        accepted.sort(key=lambda entry: entry[0])
        winner_path = accepted[0][1]
        winner = accepted[0][2]["id"]
        default_recommendation = "accepted_trial"
    elif candidates:
        candidates.sort(key=lambda entry: entry[0])
        winner_path = candidates[0][1]
        winner = candidates[0][2]["id"]
        default_recommendation = "candidate_requires_local_repair"
    else:
        winner_path = BASELINE
        winner = "baseline_ui"
        default_recommendation = "keep_baseline"
    shutil.copyfile(winner_path, OUT / "best_trial.png")
    report = {
        "baselinePrimaryWeightedScore": baseline_metrics["primaryWeightedScore"],
        "winner": winner,
        "defaultRecommendation": default_recommendation,
        "acceptanceRule": "staged: promote improving trials to candidates; repair <=2 localized primary regressions before defaulting",
        "stagedOptimization": STAGED_PHASES,
        "trials": trials,
    }
    (OUT / "optimization_trials.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    return report


def heatmap(candidate: np.ndarray, reference: np.ndarray) -> np.ndarray:
    de = delta_e(candidate, reference)
    norm = np.clip((de / max(1.0, np.percentile(de, 98))) * 255, 0, 255).astype(np.uint8)
    return cv2.cvtColor(cv2.applyColorMap(norm, cv2.COLORMAP_TURBO), cv2.COLOR_BGR2RGB)


def draw_roi_overlay(image: np.ndarray, metrics: dict, path: Path) -> None:
    pil = Image.fromarray(image).convert("RGBA")
    draw = ImageDraw.Draw(pil)
    font = ImageFont.load_default()
    for row in metrics["rois"]:
        box = row["scaled"]
        x, y, w, h = box["x"], box["y"], box["w"], box["h"]
        color = (1, 214, 204, 220) if row["tier"] == "primary" else (255, 214, 74, 210)
        draw.rectangle((x, y, x + w, y + h), outline=color, width=2)
        draw.text((x + 3, y + 3), row["name"], fill=color, font=font)
    pil.convert("RGB").save(path)


def palette_for_roi(image: np.ndarray, roi: Roi, box: tuple[int, int, int, int]) -> dict:
    pixels = crop(image, box).reshape(-1, 3)
    lab = rgb2lab((pixels.reshape(-1, 1, 3) / 255.0)).reshape(-1, 3)
    sample = pixels.astype(np.float32)
    k = min(4, len(sample))
    _, labels, centers = cv2.kmeans(
        sample,
        k,
        None,
        (cv2.TERM_CRITERIA_EPS + cv2.TERM_CRITERIA_MAX_ITER, 80, 0.2),
        4,
        cv2.KMEANS_PP_CENTERS,
    )
    counts = np.bincount(labels.flatten(), minlength=k)
    order = np.argsort(counts)[::-1]
    clusters = []
    for idx in order:
        rgb = np.clip(np.round(centers[idx]), 0, 255).astype(np.uint8)
        clusters.append({"hex": hex_rgb(rgb), "ratio": round(float(counts[idx] / len(sample)), 4)})
    return {
        "name": roi.name,
        "group": roi.group,
        "tier": roi.tier,
        "medianRgb": hex_rgb(np.percentile(pixels, 50, axis=0)),
        "p5Rgb": hex_rgb(np.percentile(pixels, 5, axis=0)),
        "p25Rgb": hex_rgb(np.percentile(pixels, 25, axis=0)),
        "p75Rgb": hex_rgb(np.percentile(pixels, 75, axis=0)),
        "p95Rgb": hex_rgb(np.percentile(pixels, 95, axis=0)),
        "medianLab": [round(float(x), 4) for x in np.percentile(lab, 50, axis=0)],
        "dominantColors": clusters,
        "estimatedGlowSpread": round(float(np.percentile(cv2.cvtColor(crop(image, box), cv2.COLOR_RGB2GRAY), 90) - np.percentile(cv2.cvtColor(crop(image, box), cv2.COLOR_RGB2GRAY), 50)), 4),
    }


def hex_rgb(rgb: np.ndarray) -> str:
    rgb = np.clip(np.round(rgb), 0, 255).astype(np.uint8)
    return f"#{rgb[0]:02x}{rgb[1]:02x}{rgb[2]:02x}"


def write_palette_artifacts(reference: np.ndarray) -> None:
    height, width = reference.shape[:2]
    palette = []
    for roi in ROIS:
        palette.append(palette_for_roi(reference, roi, scale_roi(roi, width, height)))
    (OUT / "palette_by_roi.json").write_text(json.dumps(palette, indent=2), encoding="utf-8")
    css_lines = [":root {"]
    for item in palette:
        token = "--roi-" + re.sub(r"[^a-z0-9]+", "-", item["name"].lower()).strip("-")
        css_lines.append(f"  {token}: {item['medianRgb']};")
    css_lines.append("}")
    (OUT / "palette_by_roi.css").write_text("\n".join(css_lines) + "\n", encoding="utf-8")
    draw_swatches(palette)
    draw_histograms(reference)


def draw_swatches(palette: list[dict]) -> None:
    font = ImageFont.load_default()
    row_h = 54
    image = Image.new("RGB", (760, row_h * len(palette)), "#010608")
    draw = ImageDraw.Draw(image)
    for index, item in enumerate(palette):
        y = index * row_h
        colors = [item["p5Rgb"], item["p25Rgb"], item["medianRgb"], item["p75Rgb"], item["p95Rgb"]]
        for c_index, color in enumerate(colors):
            draw.rectangle((c_index * 52, y, c_index * 52 + 48, y + row_h), fill=color)
        draw.text((280, y + 8), f"{item['name']} {item['tier']}", fill="#eaffff", font=font)
        draw.text((280, y + 28), "dominant " + ", ".join(c["hex"] for c in item["dominantColors"][:3]), fill="#85aca9", font=font)
    image.save(OUT / "color_swatches_by_roi.png")


def draw_histograms(reference: np.ndarray) -> None:
    width = 520
    row_h = 92
    image = Image.new("RGB", (width, row_h * len(ROIS)), "#010608")
    draw = ImageDraw.Draw(image)
    font = ImageFont.load_default()
    for index, roi in enumerate(ROIS):
        box = scale_roi(roi, reference.shape[1], reference.shape[0])
        data = crop(reference, box)
        y0 = index * row_h
        draw.text((8, y0 + 4), roi.name, fill="#eaffff", font=font)
        for channel, color in enumerate(((255, 80, 80), (80, 255, 160), (80, 180, 255))):
            hist = cv2.calcHist([data], [channel], None, [64], [0, 256]).flatten()
            hist = hist / max(1, hist.max())
            pts = []
            for i, value in enumerate(hist):
                x = 8 + i * 7
                y = y0 + 84 - int(value * 55)
                pts.append((x, y))
            if len(pts) > 1:
                draw.line(pts, fill=color, width=1)
    image.save(OUT / "roi_histograms.png")


def write_table(report: dict) -> None:
    lines = [
        "| ROI | Tier | Before | After | Delta | % | ΔE mean | RGB MAE | SSIM |",
        "| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |",
    ]
    for row in sorted(report["roiRows"], key=lambda item: (item["tier"] != "primary", item["improvement"])):
        lines.append(
            f"| {row['name']} | {row['tier']} | {row['beforeScore']:.4f} | {row['afterScore']:.4f} | "
            f"{row['improvement']:.4f} | {row['improvementPct']:.2f}% | {row['afterDeltaE2000Mean']:.4f} | "
            f"{row['afterRgbMae']:.4f} | {row['afterSsim']:.4f} |"
        )
    (OUT / "roi_score_table.md").write_text("\n".join(lines) + "\n", encoding="utf-8")


def main() -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    baseline = load_rgb(BASELINE)
    current = load_rgb(CURRENT)
    if baseline.shape[:2] != current.shape[:2]:
        baseline = cv2.resize(baseline, (current.shape[1], current.shape[0]), interpolation=cv2.INTER_AREA)
    reference = resize_reference(load_rgb(REFERENCE), current)
    save_rgb(OUT / "reference_aligned.png", reference)

    baseline_metrics = score_image(baseline, reference)
    current_metrics = score_image(current, reference)
    report = comparison_report(baseline_metrics, current_metrics)
    trials = trial_reports(baseline_metrics, reference)
    full_report = {
        "reference": str(REFERENCE.relative_to(ROOT)),
        "baseline": str(BASELINE.relative_to(ROOT)),
        "current": str(CURRENT.relative_to(ROOT)),
        "dependencies": {
            "uvAvailable": False,
            "pythonPackages": ["opencv-python/cv2", "Pillow", "numpy", "scipy", "scikit-image", "playwright"],
            "note": "uv was not installed in this shell; current Python has the heavy image stack and Playwright Chromium for the ROI pipeline.",
        },
        "baselineMetrics": baseline_metrics,
        "currentMetrics": current_metrics,
        "comparison": report,
        "optimizationTrials": trials,
    }
    (OUT / "rois.json").write_text(json.dumps([asdict(roi) for roi in ROIS], indent=2), encoding="utf-8")
    write_palette_artifacts(reference)
    draw_roi_overlay(current, current_metrics, OUT / "roi_overlay_current.png")

    overlay_before = cv2.addWeighted(baseline, 0.5, reference, 0.5, 0)
    overlay_after = cv2.addWeighted(current, 0.5, reference, 0.5, 0)
    diff_before = cv2.absdiff(baseline, reference)
    diff_after = cv2.absdiff(current, reference)
    save_rgb(OUT / "before_ui.png", baseline)
    save_rgb(OUT / "after_ui.png", current)
    save_rgb(OUT / "overlay_before.png", overlay_before)
    save_rgb(OUT / "overlay_after.png", overlay_after)
    save_rgb(OUT / "difference_before.png", diff_before)
    save_rgb(OUT / "difference_after.png", diff_after)
    save_rgb(OUT / "heatmap_before.png", heatmap(baseline, reference))
    save_rgb(OUT / "heatmap_after.png", heatmap(current, reference))
    write_table(report)
    (OUT / "metric_report.json").write_text(json.dumps(full_report, indent=2), encoding="utf-8")
    (OUT / "metric_report.md").write_text(
        "\n".join(
            [
                "# Visual Parity Metric Report",
                "",
                f"- Primary weighted before: {report['primaryWeightedBefore']}",
                f"- Primary weighted after: {report['primaryWeightedAfter']}",
                f"- Primary weighted improvement: {report['primaryWeightedImprovement']} ({report['primaryWeightedImprovementPct']}%)",
                f"- Primary ROIs worsened: {report['primaryRoisWorsenedCount']}",
                f"- Accept as default: {report['acceptAsDefault']}",
                f"- Candidate with local repair required: {report['candidateAcceptedWithLocalRepairRequired']}",
                "",
                "See `roi_score_table.md` for the component table.",
            ]
        )
        + "\n",
        encoding="utf-8",
    )
    print(json.dumps({
        "primaryWeightedBefore": report["primaryWeightedBefore"],
        "primaryWeightedAfter": report["primaryWeightedAfter"],
        "primaryWeightedImprovement": report["primaryWeightedImprovement"],
        "primaryRoisWorsenedCount": report["primaryRoisWorsenedCount"],
        "acceptAsDefault": report["acceptAsDefault"],
    }, indent=2))


if __name__ == "__main__":
    main()
