from __future__ import annotations

import json
import math
import sys
from dataclasses import dataclass
from pathlib import Path

import cv2
import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy.interpolate import CubicSpline, splprep, splev
from scipy.ndimage import gaussian_filter1d
from skimage.morphology import skeletonize

ROOT = Path(__file__).resolve().parents[1]
TOOLS_DIR = ROOT / "tools"
if str(TOOLS_DIR) not in sys.path:
    sys.path.insert(0, str(TOOLS_DIR))

import compare_snake_paths as compare
import extract_snake_v2 as extract


DEBUG_DIR = ROOT / "assets" / "debug"
OUTPUT_DIR = ROOT / "tools" / "output" / "snake-optimization"
V2_TS_PATH = ROOT / "src" / "lib" / "snakePathV2.generated.ts"
REPORT_JSON = DEBUG_DIR / "snake-optimization-report.json"
REPORT_MD = DEBUG_DIR / "snake-optimization-report.md"
COMPARISON_PNG = DEBUG_DIR / "snake-optimization-before-after.png"
TRIALS_SVG = DEBUG_DIR / "snake-optimization-trials.svg"
ROUNDED_COMPARISON_PNG = DEBUG_DIR / "snake-rounded-ui-vs-reference-lower.png"

WIDTH = 1086
HEIGHT = 1448
NODE_ORDER = ["hello", "read", "strings", "functions", "compare", "mini", "cli"]


@dataclass(frozen=True)
class Candidate:
    name: str
    spacing: float
    sigma: float
    bc_type: str
    path_d: str
    raw_path_d: str
    completed_d: str
    active_d: str
    locked_d: str
    points: list[tuple[float, float]]
    metric: dict


def cumulative_distances(points: list[tuple[float, float]]) -> list[float]:
    distances = [0.0]
    for index in range(1, len(points)):
        distances.append(distances[-1] + extract.distance(points[index], points[index - 1]))
    return distances


def sample_segment(segment: list[tuple[float, float]], spacing: float) -> list[tuple[float, float]]:
    if len(segment) < 2:
        return segment
    distances = cumulative_distances(segment)
    total = distances[-1]
    count = max(2, int(math.ceil(total / spacing)) + 1)
    sampled: list[tuple[float, float]] = []
    for step in range(count):
        target = total * step / (count - 1)
        idx = int(np.searchsorted(distances, target))
        if idx <= 0:
            sampled.append(segment[0])
            continue
        if idx >= len(segment):
            sampled.append(segment[-1])
            continue
        d0 = distances[idx - 1]
        d1 = distances[idx]
        frac = 0.0 if d1 == d0 else (target - d0) / (d1 - d0)
        p0 = segment[idx - 1]
        p1 = segment[idx]
        sampled.append((p0[0] + (p1[0] - p0[0]) * frac, p0[1] + (p1[1] - p0[1]) * frac))
    return sampled


def smooth_segment(points: list[tuple[float, float]], sigma: float) -> list[tuple[float, float]]:
    if sigma <= 0 or len(points) < 4:
        return points
    arr = np.array(points, dtype=np.float64)
    smoothed_x = gaussian_filter1d(arr[:, 0], sigma=sigma, mode="nearest")
    smoothed_y = gaussian_filter1d(arr[:, 1], sigma=sigma, mode="nearest")
    smoothed = list(zip(smoothed_x.tolist(), smoothed_y.tolist()))
    smoothed[0] = points[0]
    smoothed[-1] = points[-1]
    return smoothed


def smooth_polyline(points: list[tuple[float, float]], sigma: float) -> list[tuple[float, float]]:
    if sigma <= 0 or len(points) < 4:
        return points
    arr = np.array(points, dtype=np.float64)
    smoothed_x = gaussian_filter1d(arr[:, 0], sigma=sigma, mode="nearest")
    smoothed_y = gaussian_filter1d(arr[:, 1], sigma=sigma, mode="nearest")
    smoothed = list(zip(smoothed_x.tolist(), smoothed_y.tolist()))
    smoothed[0] = points[0]
    smoothed[-1] = points[-1]
    return smoothed


def resample_polyline(points: list[tuple[float, float]], spacing: float) -> list[tuple[float, float]]:
    distances = cumulative_distances(points)
    total = distances[-1]
    count = max(2, int(math.ceil(total / spacing)) + 1)
    sampled = []
    for step in range(count):
        target = total * step / (count - 1)
        idx = int(np.searchsorted(distances, target))
        if idx <= 0:
            sampled.append(points[0])
            continue
        if idx >= len(points):
            sampled.append(points[-1])
            continue
        d0 = distances[idx - 1]
        d1 = distances[idx]
        frac = 0.0 if d1 == d0 else (target - d0) / (d1 - d0)
        p0 = points[idx - 1]
        p1 = points[idx]
        sampled.append((p0[0] + (p1[0] - p0[0]) * frac, p0[1] + (p1[1] - p0[1]) * frac))
    return sampled


def build_reference_trace(config: dict) -> tuple[np.ndarray, np.ndarray, list[list[tuple[float, float]]], dict[str, dict]]:
    image_rgb = np.array(Image.open(ROOT / config["image"]).convert("RGB"))
    _, clean_mask = extract.build_masks(image_rgb, config)
    skeleton = skeletonize(clean_mask)
    roi = config["roi"]
    circles = {circle["id"]: circle for circle in config.get("excludeCircles", [])}
    traced_segments: list[list[tuple[float, float]]] = []

    for index in range(len(NODE_ORDER) - 1):
        segment = extract.trace_dijkstra(skeleton, circles[NODE_ORDER[index]], circles[NODE_ORDER[index + 1]], config)
        global_segment = [(x + roi["x"], y + roi["y"]) for y, x in segment]
        if len(global_segment) < 2:
            raise RuntimeError(f"Could not trace segment {NODE_ORDER[index]} -> {NODE_ORDER[index + 1]}")
        traced_segments.append(global_segment)

    clean_full = np.zeros((HEIGHT, WIDTH), dtype=np.uint8)
    skeleton_full = np.zeros((HEIGHT, WIDTH), dtype=np.uint8)
    clean_full[roi["y"] : roi["y"] + roi["h"], roi["x"] : roi["x"] + roi["w"]] = clean_mask.astype(np.uint8) * 255
    skeleton_full[roi["y"] : roi["y"] + roi["h"], roi["x"] : roi["x"] + roi["w"]] = skeleton.astype(np.uint8) * 255
    Image.fromarray(clean_full).save(DEBUG_DIR / "snake-mask-v2.png")
    Image.fromarray(skeleton_full).save(DEBUG_DIR / "snake-skeleton-v2.png")
    return clean_full, skeleton_full, traced_segments, circles


def assemble_points(
    traced_segments: list[list[tuple[float, float]]],
    circles: dict[str, dict],
    spacing: float,
    sigma: float,
) -> tuple[list[tuple[float, float]], dict[str, int]]:
    all_points: list[tuple[float, float]] = []
    node_indices: dict[str, int] = {}
    for index, segment in enumerate(traced_segments):
        sampled = smooth_segment(sample_segment(segment, spacing), sigma)
        start_name = NODE_ORDER[index]
        end_name = NODE_ORDER[index + 1]
        sampled[0] = (float(circles[start_name]["x"]), float(circles[start_name]["y"]))
        sampled[-1] = (float(circles[end_name]["x"]), float(circles[end_name]["y"]))
        if not all_points:
            node_indices[start_name] = 0
            all_points.extend(sampled)
        else:
            node_indices[start_name] = len(all_points) - 1
            all_points.extend(sampled[1:])
        node_indices[end_name] = len(all_points) - 1
    return all_points, node_indices


def cubic_spline_segments(points: list[tuple[float, float]], bc_type: str) -> list[tuple[tuple[float, float], tuple[float, float], tuple[float, float], tuple[float, float]]]:
    arr = np.array(points, dtype=np.float64)
    distances = np.array(cumulative_distances(points), dtype=np.float64)
    keep = np.concatenate(([True], np.diff(distances) > 1e-4))
    arr = arr[keep]
    distances = distances[keep]
    cs_x = CubicSpline(distances, arr[:, 0], bc_type=bc_type)
    cs_y = CubicSpline(distances, arr[:, 1], bc_type=bc_type)
    segments = []
    for index in range(len(distances) - 1):
        h = distances[index + 1] - distances[index]
        p1 = (float(arr[index, 0]), float(arr[index, 1]))
        p2 = (float(arr[index + 1, 0]), float(arr[index + 1, 1]))
        cx = cs_x.c[:, index]
        cy = cs_y.c[:, index]
        dx_start = cx[2]
        dy_start = cy[2]
        dx_end = 3 * cx[0] * h**2 + 2 * cx[1] * h + cx[2]
        dy_end = 3 * cy[0] * h**2 + 2 * cy[1] * h + cy[2]
        c1 = (p1[0] + dx_start * h / 3.0, p1[1] + dy_start * h / 3.0)
        c2 = (p2[0] - dx_end * h / 3.0, p2[1] - dy_end * h / 3.0)
        segments.append((p1, c1, c2, p2))
    return segments


def build_hybrid_bottom_rounded_points(
    traced_segments: list[list[tuple[float, float]]],
    circles: dict[str, dict],
    spacing: float,
    upper_sigma: float,
    lower_sigma: float,
) -> tuple[list[tuple[float, float]], dict[str, int]]:
    all_points: list[tuple[float, float]] = []
    node_indices: dict[str, int] = {}
    for index, segment in enumerate(traced_segments):
        start_name = NODE_ORDER[index]
        end_name = NODE_ORDER[index + 1]
        segment_sigma = upper_sigma if index < 2 else lower_sigma
        sampled = resample_polyline(smooth_polyline(segment, segment_sigma), spacing)
        sampled[0] = (float(circles[start_name]["x"]), float(circles[start_name]["y"]))
        sampled[-1] = (float(circles[end_name]["x"]), float(circles[end_name]["y"]))
        if not all_points:
            node_indices[start_name] = 0
            all_points.extend(sampled)
        else:
            node_indices[start_name] = len(all_points) - 1
            all_points.extend(sampled[1:])
        node_indices[end_name] = len(all_points) - 1
    return all_points, node_indices


def make_bottom_rounded_candidate(
    traced_segments: list[list[tuple[float, float]]],
    circles: dict[str, dict],
    spacing: float,
    upper_sigma: float,
    lower_sigma: float,
    bc_type: str,
    skeleton: np.ndarray,
    mask: np.ndarray,
    nodes_mask: np.ndarray,
    stroke_width: float,
) -> Candidate:
    points, node_indices = build_hybrid_bottom_rounded_points(traced_segments, circles, spacing, upper_sigma, lower_sigma)
    raw_segments = extract.compute_bezier_segments(points, 0.88)
    bezier_segments = cubic_spline_segments(points, bc_type)
    path_d = extract.bezier_segments_to_path_d(bezier_segments)
    raw_path_d = extract.bezier_segments_to_path_d(raw_segments)
    read_index = node_indices["read"]
    strings_index = node_indices["strings"]
    completed_d = extract.bezier_segments_to_path_d(bezier_segments[:read_index])
    active_d = extract.bezier_segments_to_path_d(bezier_segments[read_index:strings_index])
    locked_d = extract.bezier_segments_to_path_d(bezier_segments[strings_index:])
    name = f"bottom-rounded_spacing-{spacing:g}_upper-{upper_sigma:g}_lower-{lower_sigma:g}_{bc_type}"
    metric = evaluate_path(name, path_d, stroke_width, skeleton, mask, nodes_mask)
    metric.update({"spacing": spacing, "upperSigma": upper_sigma, "lowerSigma": lower_sigma, "bcType": bc_type, "segmentCount": len(bezier_segments)})
    return Candidate(name, spacing, lower_sigma, bc_type, path_d, raw_path_d, completed_d, active_d, locked_d, points, metric)


def make_global_spline_candidate(
    traced_segments: list[list[tuple[float, float]]],
    circles: dict[str, dict],
    source_sigma: float,
    spline_smoothing: float,
    anchor_spacing: float,
    catmull_tension: float,
    skeleton: np.ndarray,
    mask: np.ndarray,
    nodes_mask: np.ndarray,
    stroke_width: float,
) -> Candidate:
    source: list[tuple[float, float]] = []
    for index, segment in enumerate(traced_segments):
        if index == 0:
            source.extend(segment)
        else:
            source.extend(segment[1:])
    source = smooth_polyline(source, source_sigma)
    source[0] = (float(circles["hello"]["x"]), float(circles["hello"]["y"]))
    source[-1] = (float(circles["cli"]["x"]), float(circles["cli"]["y"]))
    arr = np.array(source, dtype=np.float64)
    try:
        tck, _ = splprep([arr[:, 0], arr[:, 1]], s=spline_smoothing, k=3)
        sample_count = max(140, int(len(source) * 0.65))
        u = np.linspace(0, 1, sample_count)
        x, y = splev(u, tck)
        dense = list(zip(np.asarray(x).tolist(), np.asarray(y).tolist()))
    except Exception:
        dense = source
    dense[0] = (float(circles["hello"]["x"]), float(circles["hello"]["y"]))
    dense[-1] = (float(circles["cli"]["x"]), float(circles["cli"]["y"]))
    points = resample_polyline(dense, anchor_spacing)
    node_indices: dict[str, int] = {}
    for node_name in NODE_ORDER:
        node_point = (float(circles[node_name]["x"]), float(circles[node_name]["y"]))
        idx = min(range(len(points)), key=lambda point_index: extract.distance(points[point_index], node_point))
        points[idx] = node_point
        node_indices[node_name] = idx
    raw_segments = extract.compute_bezier_segments(points, catmull_tension)
    path_d = extract.bezier_segments_to_path_d(raw_segments)
    read_index = node_indices["read"]
    strings_index = node_indices["strings"]
    completed_d = extract.bezier_segments_to_path_d(raw_segments[:read_index])
    active_d = extract.bezier_segments_to_path_d(raw_segments[read_index:strings_index])
    locked_d = extract.bezier_segments_to_path_d(raw_segments[strings_index:])
    name = f"global-spline_src-{source_sigma:g}_s-{spline_smoothing:g}_spacing-{anchor_spacing:g}_t-{catmull_tension:g}"
    metric = evaluate_path(name, path_d, stroke_width, skeleton, mask, nodes_mask)
    metric.update(
        {
            "sourceSigma": source_sigma,
            "splineSmoothing": spline_smoothing,
            "anchorSpacing": anchor_spacing,
            "catmullTension": catmull_tension,
            "segmentCount": len(raw_segments),
        }
    )
    return Candidate(name, anchor_spacing, source_sigma, "global-spline", path_d, path_d, completed_d, active_d, locked_d, points, metric)


def curvature_metric(path_d: str) -> dict:
    points = compare.sample_path(path_d, samples_per_curve=8)
    vectors = np.diff(points, axis=0)
    lengths = np.linalg.norm(vectors, axis=1)
    valid = lengths > 1e-4
    vectors = vectors[valid]
    lengths = lengths[valid]
    if len(vectors) < 3:
        return {"curvatureP95Deg": 0.0, "curvatureMaxDeg": 0.0}
    unit = vectors / lengths[:, None]
    dots = np.sum(unit[:-1] * unit[1:], axis=1)
    dots = np.clip(dots, -1.0, 1.0)
    angles = np.degrees(np.arccos(dots))
    return {
        "curvatureP95Deg": round(float(np.percentile(angles, 95)), 3),
        "curvatureMaxDeg": round(float(np.max(angles)), 3),
    }


def lower_spike_metric(path_d: str) -> dict:
    points = compare.sample_path(path_d, samples_per_curve=8)
    lower = points[points[:, 1] >= 760]
    if len(lower) < 5:
        return {"lowerCurvatureP95Deg": 0.0, "lowerCurvatureMaxDeg": 0.0, "lowerSpikeCount": 0}
    vectors = np.diff(lower, axis=0)
    lengths = np.linalg.norm(vectors, axis=1)
    valid = lengths > 1e-4
    vectors = vectors[valid]
    lengths = lengths[valid]
    if len(vectors) < 3:
        return {"lowerCurvatureP95Deg": 0.0, "lowerCurvatureMaxDeg": 0.0, "lowerSpikeCount": 0}
    unit = vectors / lengths[:, None]
    dots = np.sum(unit[:-1] * unit[1:], axis=1)
    dots = np.clip(dots, -1.0, 1.0)
    angles = np.degrees(np.arccos(dots))
    return {
        "lowerCurvatureP95Deg": round(float(np.percentile(angles, 95)), 3),
        "lowerCurvatureMaxDeg": round(float(np.max(angles)), 3),
        "lowerSpikeCount": int(np.sum(angles > 7.0)),
    }


def node_mask(circles: dict[str, dict]) -> np.ndarray:
    mask = np.zeros((HEIGHT, WIDTH), dtype=np.uint8)
    for circle in circles.values():
        cv2.circle(mask, (int(circle["x"]), int(circle["y"])), int(circle["r"]), 255, -1)
    return mask


def evaluate_path(
    name: str,
    path_d: str,
    stroke_width: float,
    skeleton: np.ndarray,
    mask: np.ndarray,
    nodes_mask: np.ndarray,
) -> dict:
    points = compare.sample_path(path_d, samples_per_curve=20)
    distance_to_reference = cv2.distanceTransform(255 - skeleton, cv2.DIST_L2, 5)
    coords = np.round(points).astype(np.int32)
    coords[:, 0] = np.clip(coords[:, 0], 0, WIDTH - 1)
    coords[:, 1] = np.clip(coords[:, 1], 0, HEIGHT - 1)
    outside_nodes = nodes_mask[coords[:, 1], coords[:, 0]] == 0
    path_distances = distance_to_reference[coords[outside_nodes, 1], coords[outside_nodes, 0]]
    if len(path_distances) == 0:
        path_distances = distance_to_reference[coords[:, 1], coords[:, 0]]

    candidate_line = compare.rasterize(points, 1)
    distance_to_candidate = cv2.distanceTransform(255 - candidate_line, cv2.DIST_L2, 5)
    ys, xs = np.where(skeleton > 0)
    reference_distances = distance_to_candidate[ys, xs]

    candidate_mask = compare.rasterize(points, stroke_width)
    visible_candidate = np.logical_and(candidate_mask > 0, nodes_mask == 0)
    visible_reference = mask > 0
    intersection = np.logical_and(visible_candidate, visible_reference).sum()
    union = np.logical_or(visible_candidate, visible_reference).sum()
    iou = intersection / union if union else 0.0
    curve = curvature_metric(path_d)
    lower_curve = lower_spike_metric(path_d)

    metric = {
        "name": name,
        "pathToReferenceMeanPx": round(float(path_distances.mean()), 3),
        "pathToReferenceP95Px": round(float(np.percentile(path_distances, 95)), 3),
        "referenceToPathMeanPx": round(float(reference_distances.mean()), 3),
        "referenceToPathP95Px": round(float(np.percentile(reference_distances, 95)), 3),
        "maskIoU": round(float(iou), 5),
        "strokeWidthPx": stroke_width,
        "sampleCount": int(len(points)),
        **curve,
        **lower_curve,
    }
    metric["score"] = round(
        metric["maskIoU"] * 145
        - metric["referenceToPathMeanPx"] * 8.0
        - metric["referenceToPathP95Px"] * 2.4
        - metric["pathToReferenceMeanPx"] * 2.8
        - metric["pathToReferenceP95Px"] * 0.8
        - metric["curvatureP95Deg"] * 2.0
        - metric["curvatureMaxDeg"] * 0.32,
        3,
    )
    metric["roundedScore"] = round(
        metric["score"]
        - metric["lowerCurvatureP95Deg"] * 4.2
        - metric["lowerCurvatureMaxDeg"] * 0.85
        - metric["lowerSpikeCount"] * 0.7,
        3,
    )
    return metric


def make_candidate(
    traced_segments: list[list[tuple[float, float]]],
    circles: dict[str, dict],
    spacing: float,
    sigma: float,
    bc_type: str,
    skeleton: np.ndarray,
    mask: np.ndarray,
    nodes_mask: np.ndarray,
    stroke_width: float,
) -> Candidate:
    points, node_indices = assemble_points(traced_segments, circles, spacing, sigma)
    raw_segments = extract.compute_bezier_segments(points, 0.88)
    bezier_segments = cubic_spline_segments(points, bc_type)
    path_d = extract.bezier_segments_to_path_d(bezier_segments)
    raw_path_d = extract.bezier_segments_to_path_d(raw_segments)
    read_index = node_indices["read"]
    strings_index = node_indices["strings"]
    completed_d = extract.bezier_segments_to_path_d(bezier_segments[:read_index])
    active_d = extract.bezier_segments_to_path_d(bezier_segments[read_index:strings_index])
    locked_d = extract.bezier_segments_to_path_d(bezier_segments[strings_index:])
    name = f"spacing-{spacing:g}_sigma-{sigma:g}_{bc_type}"
    metric = evaluate_path(name, path_d, stroke_width, skeleton, mask, nodes_mask)
    metric.update({"spacing": spacing, "sigma": sigma, "bcType": bc_type, "segmentCount": len(bezier_segments)})
    return Candidate(name, spacing, sigma, bc_type, path_d, raw_path_d, completed_d, active_d, locked_d, points, metric)


def write_generated_ts(candidate: Candidate, stroke_width: float) -> None:
    content = "\n".join(
        [
            "// Auto-generated by tools/optimize_snake_path.py. Do not edit directly.",
            "export type GeneratedSnakeV2Point = { x: number; y: number };",
            "",
            "export const GENERATED_SNAKE_V2_POINTS: GeneratedSnakeV2Point[] = [",
            *[f"  {{ x: {x:.2f}, y: {y:.2f} }}," for x, y in candidate.points],
            "];",
            "",
            f"export const GENERATED_SNAKE_V2_PATH_D = {json.dumps(candidate.path_d)};",
            f"export const GENERATED_SNAKE_V2_RAW_PATH_D = {json.dumps(candidate.raw_path_d)};",
            f"export const GENERATED_SNAKE_V2_COMPLETED_PATH_D = {json.dumps(candidate.completed_d)};",
            f"export const GENERATED_SNAKE_V2_ACTIVE_PATH_D = {json.dumps(candidate.active_d)};",
            f"export const GENERATED_SNAKE_V2_LOCKED_PATH_D = {json.dumps(candidate.locked_d)};",
            f"export const GENERATED_SNAKE_V2_WIDTH = {stroke_width:.1f};",
            "",
        ]
    )
    V2_TS_PATH.write_text(content, encoding="utf-8")


def draw_path_panel(title: str, path_d: str, stroke_width: float, color: tuple[int, int, int], reference_mask: np.ndarray) -> Image.Image:
    base = Image.new("RGB", (WIDTH, HEIGHT), (8, 12, 14))
    ref = Image.fromarray(reference_mask).convert("L")
    ref_layer = Image.new("RGBA", (WIDTH, HEIGHT), (255, 255, 255, 0))
    ref_layer.putalpha(ref.point(lambda value: 80 if value > 0 else 0))
    base_rgba = Image.alpha_composite(base.convert("RGBA"), ref_layer)
    draw = ImageDraw.Draw(base_rgba)
    points = compare.sample_path(path_d, samples_per_curve=14)
    line = [tuple(map(float, point)) for point in points]
    draw.line([(x + 1.5, y + 4) for x, y in line], fill=(0, 0, 0, 160), width=int(round(stroke_width + 10)), joint="curve")
    draw.line(line, fill=(*color, 90), width=int(round(stroke_width + 10)), joint="curve")
    draw.line(line, fill=(*color, 235), width=max(1, int(round(stroke_width))), joint="curve")
    draw.line(line, fill=(235, 255, 252, 165), width=2, joint="curve")
    draw.text((24, 28), title, fill=(235, 245, 245, 255), font=ImageFont.load_default())
    return base_rgba.convert("RGB")


def draw_lower_rounded_comparison(current_d: str, best: Candidate, reference_mask: np.ndarray, stroke_width: float) -> None:
    crop = (250, 650, 530, 1220)
    reference_full = Image.new("RGB", (WIDTH, HEIGHT), (8, 12, 14))
    ref_alpha = Image.fromarray(reference_mask).convert("L")
    reference_layer = Image.new("RGBA", (WIDTH, HEIGHT), (24, 250, 201, 0))
    reference_layer.putalpha(ref_alpha.point(lambda value: 235 if value > 0 else 0))
    reference_full = Image.alpha_composite(reference_full.convert("RGBA"), reference_layer).convert("RGB")
    ImageDraw.Draw(reference_full).text((24, 28), "ROUNDED REFERENCE - LOWER", fill=(235, 245, 245), font=ImageFont.load_default())
    ref_panel = reference_full.crop(crop)
    ui_panel = draw_path_panel("ROUNDED UI FINAL - LOWER", current_d, stroke_width, (255, 216, 74), reference_mask).crop(crop)
    final_panel = draw_path_panel("SELECTED NO-SPIKE TRIAL - LOWER", best.path_d, stroke_width, (24, 250, 201), reference_mask).crop(crop)
    combined = Image.new("RGB", (ref_panel.width * 3, ref_panel.height), (8, 12, 14))
    combined.paste(ref_panel, (0, 0))
    combined.paste(ui_panel, (ref_panel.width, 0))
    combined.paste(final_panel, (ref_panel.width * 2, 0))
    draw = ImageDraw.Draw(combined)
    draw.line([(ref_panel.width, 0), (ref_panel.width, ref_panel.height)], fill=(255, 255, 255), width=2)
    draw.line([(ref_panel.width * 2, 0), (ref_panel.width * 2, ref_panel.height)], fill=(255, 255, 255), width=2)
    combined.save(ROUNDED_COMPARISON_PNG)


def write_visual_artifacts(baseline_d: str, current_d: str, best: Candidate, reference_mask: np.ndarray, stroke_width: float) -> None:
    before = draw_path_panel("BASELINE V1", baseline_d, 14.5, (255, 91, 91), reference_mask)
    current = draw_path_panel("CURRENT V2 BEFORE OPT", current_d, stroke_width, (255, 216, 74), reference_mask)
    after = draw_path_panel("FINAL OPTIMIZED V2", best.path_d, stroke_width, (24, 250, 201), reference_mask)
    combined = Image.new("RGB", (WIDTH * 3, HEIGHT), (8, 12, 14))
    combined.paste(before, (0, 0))
    combined.paste(current, (WIDTH, 0))
    combined.paste(after, (WIDTH * 2, 0))
    draw = ImageDraw.Draw(combined)
    draw.line([(WIDTH, 0), (WIDTH, HEIGHT)], fill=(255, 255, 255), width=2)
    draw.line([(WIDTH * 2, 0), (WIDTH * 2, HEIGHT)], fill=(255, 255, 255), width=2)
    combined.save(COMPARISON_PNG)

    TRIALS_SVG.write_text(
        f"""<svg xmlns="http://www.w3.org/2000/svg" width="{WIDTH}" height="{HEIGHT}" viewBox="0 0 {WIDTH} {HEIGHT}">
  <rect width="{WIDTH}" height="{HEIGHT}" fill="#081012"/>
  <image href="snake-mask-v2.png" x="0" y="0" width="{WIDTH}" height="{HEIGHT}" opacity="0.34"/>
  <path d="{baseline_d}" fill="none" stroke="#ff5b5b" stroke-width="14.5" stroke-linecap="round" stroke-linejoin="round" opacity="0.48"/>
  <path d="{current_d}" fill="none" stroke="#ffd84a" stroke-width="{stroke_width}" stroke-linecap="round" stroke-linejoin="round" opacity="0.58"/>
  <path d="{best.path_d}" fill="none" stroke="#18fac9" stroke-width="{stroke_width}" stroke-linecap="round" stroke-linejoin="round" opacity="0.9"/>
  <text x="24" y="36" fill="#f4fbfb" font-family="Arial" font-size="24">Snake optimization trials</text>
  <text x="24" y="70" fill="#ff8d8d" font-family="Arial" font-size="18">red = baseline V1</text>
  <text x="24" y="96" fill="#ffd84a" font-family="Arial" font-size="18">yellow = current V2 before optimization</text>
  <text x="24" y="122" fill="#18fac9" font-family="Arial" font-size="18">cyan = selected final optimized V2</text>
</svg>
""",
        encoding="utf-8",
    )


def main() -> None:
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    DEBUG_DIR.mkdir(parents=True, exist_ok=True)
    config = extract.load_config(extract.CONFIG_PATH)
    reference_mask, reference_skeleton, traced_segments, circles = build_reference_trace(config)
    nodes_mask = node_mask(circles)
    stroke_width = 12.5

    baseline_d = compare.read_const(compare.V1_PATH, "GENERATED_SNAKE_PATH_D")
    current_d = compare.read_const(compare.V2_PATH, "GENERATED_SNAKE_V2_PATH_D")
    baseline_metric = evaluate_path("baseline-v1", baseline_d, 14.5, reference_skeleton, reference_mask, nodes_mask)
    current_metric = evaluate_path("current-v2-before-optimization", current_d, stroke_width, reference_skeleton, reference_mask, nodes_mask)

    candidates: list[Candidate] = []
    for spacing in [6.0, 8.0, 10.0, 12.0, 14.0, 16.0, 20.0, 24.0, 30.0, 36.0]:
        for sigma in [0.0, 0.35, 0.65, 1.0, 1.35, 1.75, 2.25, 2.75, 3.5, 4.5, 5.5]:
            for bc_type in ["natural", "not-a-knot"]:
                try:
                    candidates.append(
                        make_candidate(traced_segments, circles, spacing, sigma, bc_type, reference_skeleton, reference_mask, nodes_mask, stroke_width)
                    )
                except Exception as exc:
                    print(f"trial failed spacing={spacing} sigma={sigma} bc={bc_type}: {exc}")

    for spacing in [5.0, 6.0, 7.0, 8.0, 10.0, 12.0]:
        for upper_sigma in [0.85, 1.0, 1.25]:
            for lower_sigma in [1.5, 1.9, 2.3, 2.8, 3.4, 4.0, 4.8, 5.8, 7.0]:
                for bc_type in ["natural", "not-a-knot"]:
                    try:
                        candidates.append(
                            make_bottom_rounded_candidate(
                                traced_segments,
                                circles,
                                spacing,
                                upper_sigma,
                                lower_sigma,
                                bc_type,
                                reference_skeleton,
                                reference_mask,
                                nodes_mask,
                                stroke_width,
                            )
                        )
                    except Exception as exc:
                        print(f"bottom trial failed spacing={spacing} upper={upper_sigma} lower={lower_sigma} bc={bc_type}: {exc}")

    for source_sigma in [0.75, 1.25, 1.8, 2.4, 3.2]:
        for spline_smoothing in [250.0, 500.0, 900.0, 1400.0, 2200.0, 3400.0, 5200.0, 7600.0]:
            for anchor_spacing in [8.0, 10.0, 12.0, 14.0, 16.0, 20.0, 24.0]:
                for catmull_tension in [0.45, 0.55, 0.65, 0.78]:
                    try:
                        candidates.append(
                            make_global_spline_candidate(
                                traced_segments,
                                circles,
                                source_sigma,
                                spline_smoothing,
                                anchor_spacing,
                                catmull_tension,
                                reference_skeleton,
                                reference_mask,
                                nodes_mask,
                                stroke_width,
                            )
                        )
                    except Exception as exc:
                        print(
                            f"global spline failed src={source_sigma} s={spline_smoothing} spacing={anchor_spacing} tension={catmull_tension}: {exc}"
                        )

    eligible_visual = [
        candidate
        for candidate in candidates
        if candidate.metric["referenceToPathMeanPx"] <= 1.25
        and candidate.metric["pathToReferenceMeanPx"] <= 1.25
        and candidate.metric["maskIoU"] >= 0.77
    ]
    ranked = sorted(
        eligible_visual or candidates,
        key=lambda candidate: (
            candidate.metric["lowerSpikeCount"] * -1,
            candidate.metric["lowerCurvatureMaxDeg"] * -1,
            candidate.metric["referenceToPathMeanPx"] * -1,
            candidate.metric["roundedScore"],
        ),
        reverse=True,
    )
    best = ranked[0]
    final_improves_current = (
        best.metric["lowerSpikeCount"] < current_metric["lowerSpikeCount"]
        or (
            best.metric["lowerSpikeCount"] == current_metric["lowerSpikeCount"]
            and best.metric["roundedScore"] > current_metric["roundedScore"]
        )
    )
    if final_improves_current:
        write_generated_ts(best, stroke_width)
    else:
        best = Candidate(
            "current-v2-before-optimization",
            0,
            0,
            "existing",
            current_d,
            compare.read_const(compare.V2_PATH, "GENERATED_SNAKE_V2_RAW_PATH_D"),
            compare.read_const(compare.V2_PATH, "GENERATED_SNAKE_V2_COMPLETED_PATH_D"),
            compare.read_const(compare.V2_PATH, "GENERATED_SNAKE_V2_ACTIVE_PATH_D"),
            compare.read_const(compare.V2_PATH, "GENERATED_SNAKE_V2_LOCKED_PATH_D"),
            [],
            current_metric,
        )

    write_visual_artifacts(baseline_d, current_d, best, reference_mask, stroke_width)
    draw_lower_rounded_comparison(current_d, best, reference_mask, stroke_width)

    report = {
        "winner": best.name,
        "finalImprovesCurrentV2": final_improves_current,
        "artifacts": {
            "comparisonPng": str(COMPARISON_PNG.relative_to(ROOT)),
            "roundedLowerComparisonPng": str(ROUNDED_COMPARISON_PNG.relative_to(ROOT)),
            "trialsSvg": str(TRIALS_SVG.relative_to(ROOT)),
            "reportJson": str(REPORT_JSON.relative_to(ROOT)),
            "reportMarkdown": str(REPORT_MD.relative_to(ROOT)),
        },
        "metrics": {
            "baselineV1": baseline_metric,
            "currentV2BeforeOptimization": current_metric,
            "selectedFinal": best.metric,
        },
        "topTrials": [candidate.metric for candidate in ranked[:12]],
    }
    REPORT_JSON.write_text(json.dumps(report, indent=2), encoding="utf-8")
    REPORT_MD.write_text(
        "\n".join(
            [
                "# Snake Optimization Report",
                "",
                f"Winner: **{best.name}**",
                f"Final improves current V2: **{final_improves_current}**",
                "",
                "| Variant | Score | Rounded score | IoU | Ref->path mean | Ref->path p95 | Path->ref mean | Lower curv p95 | Lower spike count |",
                "| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |",
                f"| baseline-v1 | {baseline_metric['score']} | {baseline_metric['roundedScore']} | {baseline_metric['maskIoU']} | {baseline_metric['referenceToPathMeanPx']} | {baseline_metric['referenceToPathP95Px']} | {baseline_metric['pathToReferenceMeanPx']} | {baseline_metric['lowerCurvatureP95Deg']} | {baseline_metric['lowerSpikeCount']} |",
                f"| current-v2-before | {current_metric['score']} | {current_metric['roundedScore']} | {current_metric['maskIoU']} | {current_metric['referenceToPathMeanPx']} | {current_metric['referenceToPathP95Px']} | {current_metric['pathToReferenceMeanPx']} | {current_metric['lowerCurvatureP95Deg']} | {current_metric['lowerSpikeCount']} |",
                f"| selected-final | {best.metric['score']} | {best.metric['roundedScore']} | {best.metric['maskIoU']} | {best.metric['referenceToPathMeanPx']} | {best.metric['referenceToPathP95Px']} | {best.metric['pathToReferenceMeanPx']} | {best.metric['lowerCurvatureP95Deg']} | {best.metric['lowerSpikeCount']} |",
                "",
                "Top trials:",
                *[
                    f"- {candidate.metric['name']}: rounded score {candidate.metric['roundedScore']}, IoU {candidate.metric['maskIoU']}, ref mean {candidate.metric['referenceToPathMeanPx']}px, lower curvature p95 {candidate.metric['lowerCurvatureP95Deg']}deg, lower spikes {candidate.metric['lowerSpikeCount']}"
                    for candidate in ranked[:8]
                ],
                "",
                "Artifacts:",
                f"- `{COMPARISON_PNG.relative_to(ROOT)}`",
                f"- `{ROUNDED_COMPARISON_PNG.relative_to(ROOT)}`",
                f"- `{TRIALS_SVG.relative_to(ROOT)}`",
                f"- `{REPORT_JSON.relative_to(ROOT)}`",
            ]
        )
        + "\n",
        encoding="utf-8",
    )
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
