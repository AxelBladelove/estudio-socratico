#!/usr/bin/env python
from __future__ import annotations

import argparse
import json
import math
import re
from collections import deque
from pathlib import Path
from typing import Iterable

try:
    import numpy as np
    from PIL import Image
except ImportError as exc:
    raise SystemExit(
        "Missing dependency. Run with a Python that has numpy and Pillow installed."
    ) from exc

try:
    import cv2
except ImportError:
    cv2 = None

try:
    from skimage.morphology import skeletonize as skimage_skeletonize
except ImportError:
    skimage_skeletonize = None


ROOT = Path(__file__).resolve().parents[1]
CONFIG_PATH = ROOT / "tools" / "snake-extraction.config.json"
DEBUG_DIR = ROOT / "assets" / "debug"
POINTS_JSON_PATH = ROOT / "src" / "lib" / "snakePoints.generated.json"
PATH_TS_PATH = ROOT / "src" / "lib" / "snakePath.generated.ts"
PREVIEW_SVG_PATH = DEBUG_DIR / "snake-path-preview.svg"
LAYOUT_TS_PATH = ROOT / "src" / "lib" / "layout.ts"
POINTS_RAW_PATH = DEBUG_DIR / "snake-points.raw.json"
POINTS_VISIBLE_PATH = DEBUG_DIR / "snake-points.visible.json"
POINTS_FINAL_PATH = DEBUG_DIR / "snake-points.final.json"
CURVATURE_SVG_PATH = DEBUG_DIR / "snake-curvature-debug.svg"
COMPARISON_SVG_PATH = DEBUG_DIR / "snake-comparison-overlay.svg"


def load_config(path: Path) -> dict:
    with path.open("r", encoding="utf-8") as handle:
        config = json.load(handle)
    return sync_node_centers_from_layout(config)


def sync_node_centers_from_layout(config: dict) -> dict:
    if not LAYOUT_TS_PATH.exists():
        return config
    text = LAYOUT_TS_PATH.read_text(encoding="utf-8")
    centers = {
        match.group("id"): {
            "x": int(match.group("x")),
            "y": int(match.group("y")),
        }
        for match in re.finditer(
            r"(?P<id>hello|read|strings|functions|compare|mini|cli):\s*\{\s*x:\s*(?P<x>\d+),\s*y:\s*(?P<y>\d+),",
            text,
        )
    }
    for circle in config.get("excludeCircles", []):
        if circle.get("id") in centers:
            circle.update(centers[circle["id"]])
    return config


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Extract the lesson snake from the reference image.")
    parser.add_argument("--config", default=str(CONFIG_PATH))
    parser.add_argument("--debug", action="store_true", help="Write all debug images.")
    parser.add_argument("--threshold-preview", action="store_true", help="Write only threshold-focused previews too.")
    parser.add_argument("--export-path", action="store_true", help="Export generated JSON/TS/SVG path files.")
    parser.add_argument("--roi", nargs=4, type=int, metavar=("X", "Y", "W", "H"))
    parser.add_argument("--brightness-threshold", type=float)
    parser.add_argument("--saturation-threshold", type=float)
    parser.add_argument("--min-component-area", type=int)
    parser.add_argument("--skeleton-prune-threshold", type=int)
    parser.add_argument("--point-simplification-epsilon", type=float)
    parser.add_argument("--smoothing-factor", type=float)
    parser.add_argument("--output-stroke-width", type=float)
    return parser.parse_args()


def apply_overrides(config: dict, args: argparse.Namespace) -> dict:
    if args.roi:
        x, y, w, h = args.roi
        config["roi"] = {"x": x, "y": y, "w": w, "h": h}
    mapping = {
        "brightness_threshold": "brightnessThreshold",
        "saturation_threshold": "saturationThreshold",
        "min_component_area": "minComponentArea",
        "skeleton_prune_threshold": "skeletonPruneThreshold",
        "point_simplification_epsilon": "pointSimplificationEpsilon",
        "smoothing_factor": "smoothingFactor",
        "output_stroke_width": "outputStrokeWidth",
    }
    for attr, key in mapping.items():
        value = getattr(args, attr)
        if value is not None:
            config[key] = value
    return config


def in_ranges(values: np.ndarray, ranges: Iterable[Iterable[float]]) -> np.ndarray:
    result = np.zeros(values.shape, dtype=bool)
    for start, end in ranges:
        if start <= end:
            result |= (values >= start) & (values <= end)
        else:
            result |= (values >= start) | (values <= end)
    return result


def rgb_to_hsv(rgb: np.ndarray) -> tuple[np.ndarray, np.ndarray, np.ndarray]:
    rgb_float = rgb.astype(np.float32) / 255.0
    r, g, b = rgb_float[..., 0], rgb_float[..., 1], rgb_float[..., 2]
    maxc = np.max(rgb_float, axis=-1)
    minc = np.min(rgb_float, axis=-1)
    delta = maxc - minc
    hue = np.zeros_like(maxc)
    nonzero = delta > 1e-6
    red = (maxc == r) & nonzero
    green = (maxc == g) & nonzero
    blue = (maxc == b) & nonzero
    hue[red] = ((g[red] - b[red]) / delta[red]) % 6
    hue[green] = ((b[green] - r[green]) / delta[green]) + 2
    hue[blue] = ((r[blue] - g[blue]) / delta[blue]) + 4
    hue *= 60
    saturation = np.where(maxc == 0, 0, delta / np.maximum(maxc, 1e-6))
    value = maxc * 255
    return hue, saturation, value


def shift(mask: np.ndarray, dy: int, dx: int, fill: bool) -> np.ndarray:
    result = np.full(mask.shape, fill, dtype=bool)
    h, w = mask.shape
    y_src_start = max(0, -dy)
    y_src_end = min(h, h - dy)
    x_src_start = max(0, -dx)
    x_src_end = min(w, w - dx)
    y_dst_start = max(0, dy)
    y_dst_end = min(h, h + dy)
    x_dst_start = max(0, dx)
    x_dst_end = min(w, w + dx)
    result[y_dst_start:y_dst_end, x_dst_start:x_dst_end] = mask[y_src_start:y_src_end, x_src_start:x_src_end]
    return result


def dilate(mask: np.ndarray, iterations: int) -> np.ndarray:
    result = mask.copy()
    for _ in range(iterations):
        acc = np.zeros(result.shape, dtype=bool)
        for dy in (-1, 0, 1):
            for dx in (-1, 0, 1):
                acc |= shift(result, dy, dx, False)
        result = acc
    return result


def erode(mask: np.ndarray, iterations: int) -> np.ndarray:
    result = mask.copy()
    for _ in range(iterations):
        acc = np.ones(result.shape, dtype=bool)
        for dy in (-1, 0, 1):
            for dx in (-1, 0, 1):
                acc &= shift(result, dy, dx, True)
        result = acc
    return result


def connected_components(mask: np.ndarray) -> list[list[tuple[int, int]]]:
    h, w = mask.shape
    visited = np.zeros(mask.shape, dtype=bool)
    components: list[list[tuple[int, int]]] = []
    ys, xs = np.where(mask)
    for y, x in zip(ys.tolist(), xs.tolist()):
        if visited[y, x]:
            continue
        queue = [(y, x)]
        visited[y, x] = True
        component: list[tuple[int, int]] = []
        while queue:
            cy, cx = queue.pop()
            component.append((cy, cx))
            for dy in (-1, 0, 1):
                for dx in (-1, 0, 1):
                    if dy == 0 and dx == 0:
                        continue
                    ny, nx = cy + dy, cx + dx
                    if 0 <= ny < h and 0 <= nx < w and mask[ny, nx] and not visited[ny, nx]:
                        visited[ny, nx] = True
                        queue.append((ny, nx))
        components.append(component)
    return components


def keep_components(mask: np.ndarray, min_area: int, keep_count: int) -> np.ndarray:
    components = [component for component in connected_components(mask) if len(component) >= min_area]
    components.sort(key=len, reverse=True)
    result = np.zeros(mask.shape, dtype=bool)
    for component in components[:keep_count]:
        for y, x in component:
            result[y, x] = True
    return result


def clean_mask_with_cv2(mask: np.ndarray, config: dict) -> np.ndarray:
    if cv2 is None:
        clean = mask.copy()
        clean = erode(clean, int(config["morphOpenIterations"]))
        clean = dilate(clean, int(config["morphOpenIterations"]))
        clean = dilate(clean, int(config["morphCloseIterations"]))
        clean = erode(clean, int(config["morphCloseIterations"]))
        clean = dilate(clean, int(config["morphDilateIterations"]))
        return keep_components(clean, int(config["minComponentArea"]), int(config["keepComponentCount"]))

    kernel = np.ones((3, 3), dtype=np.uint8)
    clean = mask.astype(np.uint8) * 255
    if int(config["morphOpenIterations"]) > 0:
        clean = cv2.morphologyEx(clean, cv2.MORPH_OPEN, kernel, iterations=int(config["morphOpenIterations"]))
    if int(config["morphCloseIterations"]) > 0:
        clean = cv2.morphologyEx(clean, cv2.MORPH_CLOSE, kernel, iterations=int(config["morphCloseIterations"]))
    if int(config["morphDilateIterations"]) > 0:
        clean = cv2.dilate(clean, kernel, iterations=int(config["morphDilateIterations"]))
    count, labels, stats, _ = cv2.connectedComponentsWithStats((clean > 0).astype(np.uint8), 8)
    areas = [(label, stats[label, cv2.CC_STAT_AREA]) for label in range(1, count)]
    areas = [item for item in areas if item[1] >= int(config["minComponentArea"])]
    areas.sort(key=lambda item: item[1], reverse=True)
    keep = {label for label, _ in areas[: int(config["keepComponentCount"])]}
    return np.isin(labels, list(keep))


def skeletonize(mask: np.ndarray, max_iterations: int = 160) -> np.ndarray:
    skel = mask.copy().astype(np.uint8)
    for _ in range(max_iterations):
        changed = False
        for step in (0, 1):
            p2 = skel[:-2, 1:-1]
            p3 = skel[:-2, 2:]
            p4 = skel[1:-1, 2:]
            p5 = skel[2:, 2:]
            p6 = skel[2:, 1:-1]
            p7 = skel[2:, :-2]
            p8 = skel[1:-1, :-2]
            p9 = skel[:-2, :-2]
            p1 = skel[1:-1, 1:-1]
            neighbors = p2 + p3 + p4 + p5 + p6 + p7 + p8 + p9
            transitions = (
                ((p2 == 0) & (p3 == 1)).astype(np.uint8)
                + ((p3 == 0) & (p4 == 1)).astype(np.uint8)
                + ((p4 == 0) & (p5 == 1)).astype(np.uint8)
                + ((p5 == 0) & (p6 == 1)).astype(np.uint8)
                + ((p6 == 0) & (p7 == 1)).astype(np.uint8)
                + ((p7 == 0) & (p8 == 1)).astype(np.uint8)
                + ((p8 == 0) & (p9 == 1)).astype(np.uint8)
                + ((p9 == 0) & (p2 == 1)).astype(np.uint8)
            )
            if step == 0:
                clear = (p2 * p4 * p6 == 0) & (p4 * p6 * p8 == 0)
            else:
                clear = (p2 * p4 * p8 == 0) & (p2 * p6 * p8 == 0)
            marker = (p1 == 1) & (neighbors >= 2) & (neighbors <= 6) & (transitions == 1) & clear
            if np.any(marker):
                skel[1:-1, 1:-1][marker] = 0
                changed = True
        if not changed:
            break
    return skel.astype(bool)


def prune_skeleton(skel: np.ndarray, threshold: int) -> np.ndarray:
    if threshold <= 0:
        return skel
    result = skel.copy()
    for _ in range(threshold):
        neighbor_count = np.zeros(result.shape, dtype=np.uint8)
        for dy in (-1, 0, 1):
            for dx in (-1, 0, 1):
                if dy or dx:
                    neighbor_count += shift(result, dy, dx, False).astype(np.uint8)
        endpoints = result & (neighbor_count <= 1)
        if not np.any(endpoints):
            break
        result[endpoints] = False
    return result


def bfs_path(component: list[tuple[int, int]]) -> list[tuple[int, int]]:
    points = set(component)

    def neighbors(point: tuple[int, int]) -> list[tuple[int, int]]:
        y, x = point
        return [
            (y + dy, x + dx)
            for dy in (-1, 0, 1)
            for dx in (-1, 0, 1)
            if (dy or dx) and (y + dy, x + dx) in points
        ]

    def farthest(start: tuple[int, int]) -> tuple[tuple[int, int], dict[tuple[int, int], tuple[int, int] | None]]:
        queue = deque([start])
        parent: dict[tuple[int, int], tuple[int, int] | None] = {start: None}
        last = start
        while queue:
            current = queue.popleft()
            last = current
            for nxt in neighbors(current):
                if nxt not in parent:
                    parent[nxt] = current
                    queue.append(nxt)
        return last, parent

    start = component[0]
    a, _ = farthest(start)
    b, parent = farthest(a)
    path = [b]
    while path[-1] != a:
        prev = parent[path[-1]]
        if prev is None:
            break
        path.append(prev)
    path.reverse()
    return path


def ordered_skeleton_points(skel: np.ndarray, roi: dict) -> list[tuple[float, float]]:
    components = connected_components(skel)
    components = [component for component in components if len(component) >= 3]
    paths = []
    for component in components:
        component_path = bfs_path(component)
        xy = [(float(x + roi["x"]), float(y + roi["y"])) for y, x in component_path]
        if xy and xy[0][1] > xy[-1][1]:
            xy.reverse()
        paths.append(xy)
    paths.sort(key=lambda path: min(point[1] for point in path))
    stitched: list[tuple[float, float]] = []
    for path in paths:
        if not path:
            continue
        if not stitched:
            stitched.extend(path)
            continue
        forward_gap = distance(stitched[-1], path[0])
        reverse_gap = distance(stitched[-1], path[-1])
        if reverse_gap < forward_gap:
            path = list(reversed(path))
        stitched.extend(path)
    return stitched


def distance(a: tuple[float, float], b: tuple[float, float]) -> float:
    return math.hypot(a[0] - b[0], a[1] - b[1])


def perpendicular_distance(point: tuple[float, float], start: tuple[float, float], end: tuple[float, float]) -> float:
    if start == end:
        return distance(point, start)
    numerator = abs((end[1] - start[1]) * point[0] - (end[0] - start[0]) * point[1] + end[0] * start[1] - end[1] * start[0])
    denominator = distance(start, end)
    return numerator / denominator


def rdp(points: list[tuple[float, float]], epsilon: float) -> list[tuple[float, float]]:
    if len(points) < 3:
        return points
    max_distance = 0.0
    index = 0
    for i in range(1, len(points) - 1):
        current_distance = perpendicular_distance(points[i], points[0], points[-1])
        if current_distance > max_distance:
            index = i
            max_distance = current_distance
    if max_distance > epsilon:
        return rdp(points[: index + 1], epsilon)[:-1] + rdp(points[index:], epsilon)
    return [points[0], points[-1]]


def catmull_rom_path(points: list[tuple[float, float]], smoothing: float) -> str:
    if not points:
        return ""
    if len(points) == 1:
        return f"M {points[0][0]:.1f} {points[0][1]:.1f}"
    d = [f"M {points[0][0]:.1f} {points[0][1]:.1f}"]
    tension = smoothing / 6.0
    for i in range(len(points) - 1):
        p0 = points[max(i - 1, 0)]
        p1 = points[i]
        p2 = points[i + 1]
        p3 = points[min(i + 2, len(points) - 1)]
        c1 = (p1[0] + (p2[0] - p0[0]) * tension, p1[1] + (p2[1] - p0[1]) * tension)
        c2 = (p2[0] - (p3[0] - p1[0]) * tension, p2[1] - (p3[1] - p1[1]) * tension)
        d.append(f"C {c1[0]:.1f} {c1[1]:.1f} {c2[0]:.1f} {c2[1]:.1f} {p2[0]:.1f} {p2[1]:.1f}")
    return " ".join(d)


def node_anchors(config: dict) -> list[tuple[str, tuple[float, float]]]:
    order = ["hello", "read", "strings", "functions", "compare", "mini", "cli"]
    circles = {circle["id"]: (float(circle["x"]), float(circle["y"])) for circle in config.get("excludeCircles", [])}
    return [(node_id, circles[node_id]) for node_id in order if node_id in circles]


def merge_required_anchors(points: list[tuple[float, float]], config: dict) -> tuple[list[tuple[float, float]], set[int]]:
    anchors = node_anchors(config)
    merged = list(points)
    for _, anchor in anchors:
        merged.append(anchor)
    merged.sort(key=lambda point: (point[1], point[0]))

    compact: list[tuple[float, float]] = []
    forced_indices: set[int] = set()
    anchor_values = {(round(x, 2), round(y, 2)) for _, (x, y) in anchors}
    for point in merged:
      is_anchor = (round(point[0], 2), round(point[1], 2)) in anchor_values
      if compact and distance(compact[-1], point) < (10 if is_anchor else 5):
          if is_anchor:
              compact[-1] = point
              forced_indices.add(len(compact) - 1)
          continue
      compact.append(point)
      if is_anchor:
          forced_indices.add(len(compact) - 1)
    return compact, forced_indices


def smooth_non_anchor_points(points: list[tuple[float, float]], forced_indices: set[int], config: dict) -> list[tuple[float, float]]:
    iterations = int(config.get("anchorSmoothIterations", 0))
    strength = float(config.get("anchorSmoothStrength", 0.0))
    if iterations <= 0 or strength <= 0 or len(points) < 3:
        return points
    smoothed = [(float(x), float(y)) for x, y in points]
    for _ in range(iterations):
        next_points = smoothed.copy()
        for index in range(1, len(smoothed) - 1):
            if index in forced_indices:
                continue
            prev_point = smoothed[index - 1]
            current = smoothed[index]
            next_point = smoothed[index + 1]
            target_x = (prev_point[0] + next_point[0]) / 2
            target_y = (prev_point[1] + next_point[1]) / 2
            next_points[index] = (
                current[0] * (1 - strength) + target_x * strength,
                current[1] * (1 - strength * 0.38) + target_y * (strength * 0.38),
            )
        smoothed = next_points
    return smoothed


def nearest_index(points: list[tuple[float, float]], target: tuple[float, float]) -> int:
    return min(range(len(points)), key=lambda index: distance(points[index], target))


def build_masks(image_rgb: np.ndarray, config: dict) -> tuple[np.ndarray, np.ndarray]:
    roi = config["roi"]
    crop = image_rgb[roi["y"] : roi["y"] + roi["h"], roi["x"] : roi["x"] + roi["w"]]
    hue, saturation, value = rgb_to_hsv(crop)
    bright = value >= config["brightnessThreshold"]
    saturated = saturation >= config["saturationThreshold"]
    color_match = in_ranges(hue, config["hueRanges"])
    mask = bright & saturated & color_match
    if config.get("includeLocked", True):
        locked = (
            (value >= config["lockedBrightnessThreshold"])
            & (saturation >= config["lockedSaturationMin"])
            & (saturation <= config["lockedSaturationMax"])
            & in_ranges(hue, config["lockedHueRanges"])
        )
        mask |= locked
    raw = apply_exclusions(mask, config)
    clean = clean_mask_with_cv2(raw, config)
    return raw, clean


def apply_exclusions(mask: np.ndarray, config: dict) -> np.ndarray:
    roi = config["roi"]
    result = mask.copy()
    yy, xx = np.indices(result.shape)
    for circle in config.get("excludeCircles", []):
        cx = circle["x"] - roi["x"]
        cy = circle["y"] - roi["y"]
        radius = circle["r"]
        result[((xx - cx) ** 2 + (yy - cy) ** 2) <= radius**2] = False
    for rect in config.get("excludeRects", []):
        x0 = max(0, rect["x"] - roi["x"])
        y0 = max(0, rect["y"] - roi["y"])
        x1 = min(result.shape[1], x0 + rect["w"])
        y1 = min(result.shape[0], y0 + rect["h"])
        result[y0:y1, x0:x1] = False
    return result


def full_canvas(mask_roi: np.ndarray, config: dict, size: tuple[int, int]) -> np.ndarray:
    full = np.zeros((size[1], size[0]), dtype=np.uint8)
    roi = config["roi"]
    full[roi["y"] : roi["y"] + roi["h"], roi["x"] : roi["x"] + roi["w"]] = mask_roi.astype(np.uint8) * 255
    return full


def write_debug_images(image_rgb: np.ndarray, raw: np.ndarray, clean: np.ndarray, skeleton: np.ndarray, points: list[tuple[float, float]], config: dict, threshold_preview: bool) -> None:
    DEBUG_DIR.mkdir(parents=True, exist_ok=True)
    size = (image_rgb.shape[1], image_rgb.shape[0])
    raw_full = full_canvas(raw, config, size)
    clean_full = full_canvas(clean, config, size)
    skeleton_full = full_canvas(skeleton, config, size)
    Image.fromarray(raw_full).save(DEBUG_DIR / "snake-mask.png")
    Image.fromarray(clean_full).save(DEBUG_DIR / "snake-mask-clean.png")
    Image.fromarray(skeleton_full).save(DEBUG_DIR / "snake-skeleton.png")
    overlay = image_rgb.copy()
    clean_bool = clean_full > 0
    skeleton_bool = skeleton_full > 0
    overlay[clean_bool] = (overlay[clean_bool] * 0.45 + np.array([0, 245, 232]) * 0.55).astype(np.uint8)
    overlay[skeleton_bool] = np.array([255, 216, 74], dtype=np.uint8)
    for x, y in points:
        draw_dot(overlay, int(round(x)), int(round(y)), np.array([255, 216, 74], dtype=np.uint8), radius=2)
    Image.fromarray(overlay).save(DEBUG_DIR / "snake-overlay-debug.png")
    if threshold_preview:
        preview = np.zeros_like(image_rgb)
        preview[raw_full > 0] = np.array([0, 245, 232], dtype=np.uint8)
        preview[clean_full > 0] = np.array([255, 216, 74], dtype=np.uint8)
        Image.fromarray(preview).save(DEBUG_DIR / "snake-threshold-preview.png")


def write_point_json(path: Path, points: list[tuple[float, float]]) -> None:
    path.write_text(
        json.dumps([{"x": round(x, 2), "y": round(y, 2)} for x, y in points], indent=2),
        encoding="utf-8",
    )


def write_curvature_debug(points: list[tuple[float, float]], path_d: str) -> None:
    if len(points) < 3:
        return
    circles = []
    for index in range(1, len(points) - 1):
        prev_point = points[index - 1]
        current = points[index]
        next_point = points[index + 1]
        a1 = math.atan2(current[1] - prev_point[1], current[0] - prev_point[0])
        a2 = math.atan2(next_point[1] - current[1], next_point[0] - current[0])
        turn = abs((a2 - a1 + math.pi) % (math.tau) - math.pi)
        radius = 2.4 + min(9, turn * 8)
        color = "#ff6161" if turn > 0.82 else "#ffd84a" if turn > 0.48 else "#00f5e8"
        circles.append(f'<circle cx="{current[0]:.1f}" cy="{current[1]:.1f}" r="{radius:.1f}" fill="{color}" opacity="0.85" />')
    CURVATURE_SVG_PATH.write_text(
        f"""<svg xmlns="http://www.w3.org/2000/svg" width="1086" height="1448" viewBox="0 0 1086 1448">
  <rect width="1086" height="1448" fill="#071014" />
  <path d="{path_d}" fill="none" stroke="#00f5e8" stroke-width="10" stroke-linecap="round" stroke-linejoin="round" opacity="0.55" />
  {"".join(circles)}
</svg>
""",
        encoding="utf-8",
    )


def write_comparison_svg(points: list[tuple[float, float]], path_d: str, config: dict) -> None:
    dots = "\n".join(f'<circle cx="{x:.1f}" cy="{y:.1f}" r="2.3" fill="#ffd84a" opacity="0.75" />' for x, y in points)
    width = float(config["outputStrokeWidth"])
    COMPARISON_SVG_PATH.write_text(
        f"""<svg xmlns="http://www.w3.org/2000/svg" width="1086" height="1448" viewBox="0 0 1086 1448">
  <image href="../reference-cadenas.png" x="0" y="0" width="1086" height="1448" opacity="0.62" />
  <path d="{path_d}" fill="none" stroke="#001012" stroke-width="{width + 11:.1f}" stroke-linecap="round" stroke-linejoin="round" opacity="0.48" />
  <path d="{path_d}" fill="none" stroke="#00f5e8" stroke-width="{width:.1f}" stroke-linecap="round" stroke-linejoin="round" opacity="0.72" />
  <path d="{path_d}" fill="none" stroke="#e3fffc" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round" opacity="0.62" />
  {dots}
</svg>
""",
        encoding="utf-8",
    )


def draw_dot(canvas: np.ndarray, x: int, y: int, color: np.ndarray, radius: int) -> None:
    h, w = canvas.shape[:2]
    y0, y1 = max(0, y - radius), min(h, y + radius + 1)
    x0, x1 = max(0, x - radius), min(w, x + radius + 1)
    yy, xx = np.indices((y1 - y0, x1 - x0))
    circle = (xx + x0 - x) ** 2 + (yy + y0 - y) ** 2 <= radius**2
    canvas[y0:y1, x0:x1][circle] = color


def export_generated(points: list[tuple[float, float]], path_d: str, config: dict) -> None:
    DEBUG_DIR.mkdir(parents=True, exist_ok=True)
    if PATH_TS_PATH.exists():
        (DEBUG_DIR / "snakePath.previous.ts").write_text(PATH_TS_PATH.read_text(encoding="utf-8"), encoding="utf-8")
    if POINTS_JSON_PATH.exists():
        (DEBUG_DIR / "snakePoints.previous.json").write_text(POINTS_JSON_PATH.read_text(encoding="utf-8"), encoding="utf-8")
    POINTS_JSON_PATH.write_text(
        json.dumps([{"x": round(x, 2), "y": round(y, 2)} for x, y in points], indent=2),
        encoding="utf-8",
    )
    circles = {circle["id"]: (float(circle["x"]), float(circle["y"])) for circle in config.get("excludeCircles", [])}
    read_index = nearest_index(points, circles.get("read", points[min(len(points) - 1, 1)]))
    strings_index = nearest_index(points, circles.get("strings", points[min(len(points) - 1, 2)]))
    read_index = max(1, min(read_index, len(points) - 2))
    strings_index = max(read_index + 1, min(strings_index, len(points) - 1))
    smoothing = float(config["smoothingFactor"])
    completed = catmull_rom_path(points[: read_index + 1], smoothing)
    active = catmull_rom_path(points[read_index : strings_index + 1], smoothing)
    locked = catmull_rom_path(points[strings_index:], smoothing)
    ts = "\n".join(
        [
            "export type GeneratedSnakePoint = { x: number; y: number };",
            "",
            "export const GENERATED_SNAKE_POINTS: GeneratedSnakePoint[] = [",
            *[f"  {{ x: {x:.2f}, y: {y:.2f} }}," for x, y in points],
            "];",
            "",
            f"export const GENERATED_SNAKE_PATH_D = {json.dumps(path_d)};",
            f"export const GENERATED_SNAKE_COMPLETED_PATH_D = {json.dumps(completed)};",
            f"export const GENERATED_SNAKE_ACTIVE_PATH_D = {json.dumps(active)};",
            f"export const GENERATED_SNAKE_LOCKED_PATH_D = {json.dumps(locked)};",
            f"export const GENERATED_SNAKE_WIDTH = {float(config['outputStrokeWidth']):.1f};",
            "",
        ]
    )
    PATH_TS_PATH.write_text(ts, encoding="utf-8")
    PREVIEW_SVG_PATH.write_text(build_preview_svg(points, path_d, config), encoding="utf-8")
    write_curvature_debug(points, path_d)
    write_comparison_svg(points, path_d, config)


def build_preview_svg(points: list[tuple[float, float]], path_d: str, config: dict) -> str:
    dots = "\n".join(f'<circle cx="{x:.1f}" cy="{y:.1f}" r="2" fill="#ffd84a" />' for x, y in points)
    width = float(config["outputStrokeWidth"])
    return f"""<svg xmlns="http://www.w3.org/2000/svg" width="1086" height="1448" viewBox="0 0 1086 1448">
  <rect width="1086" height="1448" fill="#071014" />
  <path d="{path_d}" fill="none" stroke="#00f5e8" stroke-width="{width}" stroke-linecap="round" stroke-linejoin="round" opacity="0.72" />
  <path d="{path_d}" fill="none" stroke="#e3fffc" stroke-width="2.4" stroke-linecap="round" stroke-linejoin="round" opacity="0.65" />
  {dots}
</svg>
"""


def main() -> None:
    args = parse_args()
    config = apply_overrides(load_config(Path(args.config)), args)
    image_path = ROOT / config["image"]
    image = Image.open(image_path).convert("RGB")
    image_rgb = np.array(image)
    raw, clean = build_masks(image_rgb, config)
    skeleton = skimage_skeletonize(clean) if skimage_skeletonize is not None else skeletonize(clean)
    skeleton = prune_skeleton(skeleton, int(config["skeletonPruneThreshold"]))
    raw_points = ordered_skeleton_points(skeleton, config["roi"])
    if len(raw_points) < 2:
        raise SystemExit("Could not extract enough snake points. Adjust threshold/config and rerun.")
    visible = rdp(raw_points, float(config["pointSimplificationEpsilon"]))
    anchored, forced_indices = merge_required_anchors(visible, config)
    refined = smooth_non_anchor_points(anchored, forced_indices, config)
    path_d = catmull_rom_path(refined, float(config["smoothingFactor"]))
    if args.debug or args.threshold_preview or args.export_path:
        DEBUG_DIR.mkdir(parents=True, exist_ok=True)
        write_point_json(POINTS_RAW_PATH, raw_points)
        write_point_json(POINTS_VISIBLE_PATH, visible)
        write_point_json(POINTS_FINAL_PATH, refined)
        write_debug_images(image_rgb, raw, clean, skeleton, refined, config, args.threshold_preview)
    if args.export_path:
        export_generated(refined, path_d, config)
    print(
        json.dumps(
            {
                "rawPoints": len(raw_points),
                "visiblePoints": len(visible),
                "finalPoints": len(refined),
                "opencv": cv2 is not None,
                "skimage": skimage_skeletonize is not None,
                "path": str(PATH_TS_PATH.relative_to(ROOT)) if args.export_path else None,
                "debugDir": str(DEBUG_DIR.relative_to(ROOT)),
            },
            indent=2,
        )
    )


if __name__ == "__main__":
    main()
