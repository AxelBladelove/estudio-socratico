from __future__ import annotations

import json
import math
import re
from pathlib import Path

import cv2
import numpy as np
from PIL import Image, ImageDraw, ImageFont


ROOT = Path(__file__).resolve().parents[1]
DEBUG_DIR = ROOT / "assets" / "debug"
V1_PATH = ROOT / "src" / "lib" / "snakePath.generated.ts"
V2_PATH = ROOT / "src" / "lib" / "snakePathV2.generated.ts"
REFERENCE_SKELETON = DEBUG_DIR / "snake-skeleton-v2.png"
REFERENCE_MASK = DEBUG_DIR / "snake-mask-v2.png"

OUT_JSON = DEBUG_DIR / "snake-before-after-report.json"
OUT_MD = DEBUG_DIR / "snake-before-after-report.md"
OUT_PNG = DEBUG_DIR / "snake-before-after-comparison.png"
OUT_SVG = DEBUG_DIR / "snake-before-after-comparison.svg"

WIDTH = 1086
HEIGHT = 1448


def read_const(path: Path, name: str) -> str:
    text = path.read_text(encoding="utf-8")
    match = re.search(rf"export const {re.escape(name)} = \"([^\"]+)\";", text)
    if not match:
        raise ValueError(f"Missing {name} in {path}")
    return match.group(1)


def read_width(path: Path, name: str) -> float:
    text = path.read_text(encoding="utf-8")
    match = re.search(rf"export const {re.escape(name)} = ([0-9.]+);", text)
    if not match:
        raise ValueError(f"Missing {name} in {path}")
    return float(match.group(1))


def parse_path(d: str) -> tuple[tuple[float, float], list[tuple[tuple[float, float], tuple[float, float], tuple[float, float]]]]:
    tokens = re.findall(r"[MC]|-?\d+(?:\.\d+)?", d)
    if not tokens or tokens[0] != "M":
        raise ValueError("Only M/C path data is supported")
    index = 1
    current = (float(tokens[index]), float(tokens[index + 1]))
    start = current
    index += 2
    curves = []
    while index < len(tokens):
        command = tokens[index]
        if command != "C":
            raise ValueError(f"Unsupported path command {command}")
        index += 1
        c1 = (float(tokens[index]), float(tokens[index + 1]))
        c2 = (float(tokens[index + 2]), float(tokens[index + 3]))
        end = (float(tokens[index + 4]), float(tokens[index + 5]))
        curves.append((c1, c2, end))
        current = end
        index += 6
    return start, curves


def cubic(p0, c1, c2, p3, t: float) -> tuple[float, float]:
    inv = 1 - t
    x = inv**3 * p0[0] + 3 * inv**2 * t * c1[0] + 3 * inv * t**2 * c2[0] + t**3 * p3[0]
    y = inv**3 * p0[1] + 3 * inv**2 * t * c1[1] + 3 * inv * t**2 * c2[1] + t**3 * p3[1]
    return x, y


def sample_path(d: str, samples_per_curve: int = 80) -> np.ndarray:
    start, curves = parse_path(d)
    points = [start]
    p0 = start
    for c1, c2, end in curves:
        for step in range(1, samples_per_curve + 1):
            points.append(cubic(p0, c1, c2, end, step / samples_per_curve))
        p0 = end
    return np.array(points, dtype=np.float32)


def binarize(path: Path) -> np.ndarray:
    image = cv2.imread(str(path), cv2.IMREAD_GRAYSCALE)
    if image is None:
        raise FileNotFoundError(path)
    _, binary = cv2.threshold(image, 10, 255, cv2.THRESH_BINARY)
    return binary


def rasterize(points: np.ndarray, stroke_width: float) -> np.ndarray:
    canvas = np.zeros((HEIGHT, WIDTH), dtype=np.uint8)
    polyline = np.round(points).astype(np.int32).reshape((-1, 1, 2))
    cv2.polylines(canvas, [polyline], isClosed=False, color=255, thickness=max(1, int(round(stroke_width))), lineType=cv2.LINE_AA)
    _, binary = cv2.threshold(canvas, 10, 255, cv2.THRESH_BINARY)
    return binary


def sample_distances(points: np.ndarray, distance_map: np.ndarray) -> np.ndarray:
    coords = np.round(points).astype(np.int32)
    coords[:, 0] = np.clip(coords[:, 0], 0, WIDTH - 1)
    coords[:, 1] = np.clip(coords[:, 1], 0, HEIGHT - 1)
    return distance_map[coords[:, 1], coords[:, 0]]


def directed_distance_from_mask(mask: np.ndarray, candidate_points: np.ndarray) -> np.ndarray:
    candidate_line = rasterize(candidate_points, 1)
    distance_to_candidate = cv2.distanceTransform(255 - candidate_line, cv2.DIST_L2, 5)
    ys, xs = np.where(mask > 0)
    return distance_to_candidate[ys, xs]


def metrics(name: str, d: str, stroke_width: float, reference_skeleton: np.ndarray, reference_mask: np.ndarray) -> dict:
    points = sample_path(d)
    distance_to_reference = cv2.distanceTransform(255 - reference_skeleton, cv2.DIST_L2, 5)
    path_to_ref = sample_distances(points, distance_to_reference)
    ref_to_path = directed_distance_from_mask(reference_skeleton, points)
    candidate_mask = rasterize(points, stroke_width)
    intersection = np.logical_and(candidate_mask > 0, reference_mask > 0).sum()
    union = np.logical_or(candidate_mask > 0, reference_mask > 0).sum()
    iou = intersection / union if union else 0
    return {
        "name": name,
        "pathToReferenceMeanPx": round(float(path_to_ref.mean()), 3),
        "pathToReferenceP95Px": round(float(np.percentile(path_to_ref, 95)), 3),
        "referenceToPathMeanPx": round(float(ref_to_path.mean()), 3),
        "referenceToPathP95Px": round(float(np.percentile(ref_to_path, 95)), 3),
        "maskIoU": round(float(iou), 5),
        "strokeWidthPx": stroke_width,
        "sampleCount": int(len(points)),
    }


def score(metric: dict) -> float:
    return (
        metric["maskIoU"] * 100
        - metric["pathToReferenceMeanPx"] * 1.7
        - metric["referenceToPathMeanPx"] * 1.3
        - metric["pathToReferenceP95Px"] * 0.45
        - metric["referenceToPathP95Px"] * 0.35
    )


def draw_panel(title: str, d: str, stroke_width: float, color: tuple[int, int, int], reference_mask: np.ndarray) -> Image.Image:
    base = Image.new("RGB", (WIDTH, HEIGHT), (10, 15, 18))
    ref = Image.fromarray(reference_mask).convert("L")
    reference_layer = Image.new("RGBA", (WIDTH, HEIGHT), (255, 255, 255, 0))
    reference_layer.putalpha(ref.point(lambda value: 72 if value > 0 else 0))
    base = Image.alpha_composite(base.convert("RGBA"), reference_layer)

    draw = ImageDraw.Draw(base)
    points = sample_path(d)
    line = [tuple(map(float, point)) for point in points]
    shadow_line = [(x + 1, y + 3) for x, y in line]
    draw.line(shadow_line, fill=(0, 0, 0, 150), width=int(round(stroke_width + 10)), joint="curve")
    draw.line(line, fill=(*color, 90), width=int(round(stroke_width + 10)), joint="curve")
    draw.line(line, fill=(*color, 230), width=int(round(stroke_width)), joint="curve")
    draw.line(line, fill=(235, 255, 252, 170), width=2, joint="curve")
    draw.text((24, 28), title, fill=(235, 245, 245, 255), font=ImageFont.load_default())
    return base.convert("RGB")


def write_svg(v1_d: str, v2_d: str) -> None:
    OUT_SVG.write_text(
        f"""<svg xmlns="http://www.w3.org/2000/svg" width="{WIDTH}" height="{HEIGHT}" viewBox="0 0 {WIDTH} {HEIGHT}">
  <rect width="{WIDTH}" height="{HEIGHT}" fill="#0a0f12"/>
  <image href="snake-mask-v2.png" x="0" y="0" width="{WIDTH}" height="{HEIGHT}" opacity="0.32"/>
  <path d="{v1_d}" fill="none" stroke="#ff5b5b" stroke-width="14.5" stroke-linecap="round" stroke-linejoin="round" opacity="0.72"/>
  <path d="{v2_d}" fill="none" stroke="#18fac9" stroke-width="12.5" stroke-linecap="round" stroke-linejoin="round" opacity="0.88"/>
  <text x="24" y="36" fill="#f4fbfb" font-family="Arial" font-size="24">Snake before/after comparison</text>
  <text x="24" y="70" fill="#ff8d8d" font-family="Arial" font-size="18">red = baseline V1</text>
  <text x="24" y="96" fill="#18fac9" font-family="Arial" font-size="18">cyan = final V2</text>
</svg>
""",
        encoding="utf-8",
    )


def main() -> None:
    v1_d = read_const(V1_PATH, "GENERATED_SNAKE_PATH_D")
    v2_d = read_const(V2_PATH, "GENERATED_SNAKE_V2_PATH_D")
    v1_width = read_width(V1_PATH, "GENERATED_SNAKE_WIDTH")
    v2_width = read_width(V2_PATH, "GENERATED_SNAKE_V2_WIDTH")
    reference_skeleton = binarize(REFERENCE_SKELETON)
    reference_mask = binarize(REFERENCE_MASK)

    baseline = metrics("baseline-v1", v1_d, v1_width, reference_skeleton, reference_mask)
    final = metrics("final-v2", v2_d, v2_width, reference_skeleton, reference_mask)
    baseline["score"] = round(score(baseline), 3)
    final["score"] = round(score(final), 3)
    verdict = "final-v2" if final["score"] > baseline["score"] else "baseline-v1"

    report = {
        "referenceSkeleton": str(REFERENCE_SKELETON.relative_to(ROOT)),
        "referenceMask": str(REFERENCE_MASK.relative_to(ROOT)),
        "artifacts": {
            "png": str(OUT_PNG.relative_to(ROOT)),
            "svg": str(OUT_SVG.relative_to(ROOT)),
            "json": str(OUT_JSON.relative_to(ROOT)),
            "markdown": str(OUT_MD.relative_to(ROOT)),
        },
        "metrics": [baseline, final],
        "winner": verdict,
        "finalImprovesBaseline": verdict == "final-v2",
    }
    OUT_JSON.write_text(json.dumps(report, indent=2), encoding="utf-8")

    before = draw_panel("BEFORE - baseline V1", v1_d, v1_width, (255, 91, 91), reference_mask)
    after = draw_panel("AFTER - final V2", v2_d, v2_width, (24, 250, 201), reference_mask)
    combined = Image.new("RGB", (WIDTH * 2, HEIGHT), (10, 15, 18))
    combined.paste(before, (0, 0))
    combined.paste(after, (WIDTH, 0))
    ImageDraw.Draw(combined).line([(WIDTH, 0), (WIDTH, HEIGHT)], fill=(255, 255, 255), width=2)
    combined.save(OUT_PNG)
    write_svg(v1_d, v2_d)

    improvement = {
        "scoreDelta": round(final["score"] - baseline["score"], 3),
        "maskIoUDelta": round(final["maskIoU"] - baseline["maskIoU"], 5),
        "pathMeanDeltaPx": round(final["pathToReferenceMeanPx"] - baseline["pathToReferenceMeanPx"], 3),
        "refMeanDeltaPx": round(final["referenceToPathMeanPx"] - baseline["referenceToPathMeanPx"], 3),
    }
    OUT_MD.write_text(
        "\n".join(
            [
                "# Snake Path Before/After Report",
                "",
                f"Winner: **{verdict}**",
                "",
                "| Variant | Score | Mask IoU | Path->ref mean | Path->ref p95 | Ref->path mean | Ref->path p95 | Stroke |",
                "| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |",
                f"| baseline-v1 | {baseline['score']} | {baseline['maskIoU']} | {baseline['pathToReferenceMeanPx']} | {baseline['pathToReferenceP95Px']} | {baseline['referenceToPathMeanPx']} | {baseline['referenceToPathP95Px']} | {baseline['strokeWidthPx']} |",
                f"| final-v2 | {final['score']} | {final['maskIoU']} | {final['pathToReferenceMeanPx']} | {final['pathToReferenceP95Px']} | {final['referenceToPathMeanPx']} | {final['referenceToPathP95Px']} | {final['strokeWidthPx']} |",
                "",
                "Improvement deltas are final minus baseline:",
                "",
                f"- scoreDelta: {improvement['scoreDelta']}",
                f"- maskIoUDelta: {improvement['maskIoUDelta']}",
                f"- pathMeanDeltaPx: {improvement['pathMeanDeltaPx']}",
                f"- refMeanDeltaPx: {improvement['refMeanDeltaPx']}",
                "",
                "Artifacts:",
                f"- PNG: `{OUT_PNG.relative_to(ROOT)}`",
                f"- SVG: `{OUT_SVG.relative_to(ROOT)}`",
                f"- JSON: `{OUT_JSON.relative_to(ROOT)}`",
            ]
        )
        + "\n",
        encoding="utf-8",
    )

    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
