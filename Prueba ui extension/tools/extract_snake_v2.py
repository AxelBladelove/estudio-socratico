#!/usr/bin/env python
from __future__ import annotations

import os
import sys
import json
import math
import re
import heapq
from pathlib import Path

try:
    import numpy as np
    import cv2
    from PIL import Image
    from scipy.interpolate import splprep, splev, CubicSpline
    from skimage.morphology import skeletonize
except ImportError as exc:
    raise SystemExit(
        "Missing dependency. Make sure numpy, opencv-python, pillow, scipy, and scikit-image are installed."
    ) from exc

ROOT = Path(__file__).resolve().parents[1]
CONFIG_PATH = ROOT / "tools" / "snake-extraction.config.json"
OUTPUT_DIR = ROOT / "tools" / "output" / "snake-trace-v2"
PATH_TS_PATH = ROOT / "src" / "lib" / "snakePathV2.generated.ts"
LAYOUT_TS_PATH = ROOT / "src" / "lib" / "layout.ts"

def load_config(path: Path) -> dict:
    with path.open("r", encoding="utf-8") as handle:
        config = json.load(handle)
    return sync_node_centers_from_layout(config)

def sync_node_centers_from_layout(config: dict) -> dict:
    if not LAYOUT_TS_PATH.exists():
        return config
    text = LAYOUT_TS_PATH.read_text(encoding="utf-8")
    # Matches float/negative numbers like 317.5 and -0.5
    centers = {
        match.group("id"): {
            "x": float(match.group("x")),
            "y": float(match.group("y")),
        }
        for match in re.finditer(
            r"(?P<id>hello|read|strings|functions|compare|mini|cli):\s*\{\s*x:\s*(?P<x>[-\d.]+),\s*y:\s*(?P<y>[-\d.]+),",
            text,
        )
    }
    for circle in config.get("excludeCircles", []):
        if circle.get("id") in centers:
            circle.update(centers[circle["id"]])
    return config

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

def in_ranges(values: np.ndarray, ranges: list[list[float]]) -> np.ndarray:
    result = np.zeros(values.shape, dtype=bool)
    for start, end in ranges:
        if start <= end:
            result |= (values >= start) & (values <= end)
        else:
            result |= (values >= start) | (values <= end)
    return result

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
        x0 = max(0, int(rect["x"] - roi["x"]))
        y0 = max(0, int(rect["y"] - roi["y"]))
        x1 = min(result.shape[1], x0 + int(rect["w"]))
        y1 = min(result.shape[0], y0 + int(rect["h"]))
        result[y0:y1, x0:x1] = False
    return result

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
    
    # Morphological cleaning
    kernel = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (3, 3))
    clean = raw.astype(np.uint8)
    clean = cv2.morphologyEx(clean, cv2.MORPH_OPEN, kernel, iterations=int(config["morphOpenIterations"]))
    clean = cv2.morphologyEx(clean, cv2.MORPH_CLOSE, kernel, iterations=int(config["morphCloseIterations"]))
    clean = cv2.dilate(clean, kernel, iterations=int(config["morphDilateIterations"]))
    
    # Area thresholding to keep largest components
    num_labels, labels, stats, centroids = cv2.connectedComponentsWithStats(clean)
    sizes = stats[1:, cv2.CC_STAT_AREA]
    min_area = int(config["minComponentArea"])
    keep_count = int(config["keepComponentCount"])
    
    result = np.zeros(clean.shape, dtype=bool)
    valid_components = [(i + 1, size) for i, size in enumerate(sizes) if size >= min_area]
    valid_components.sort(key=lambda x: x[1], reverse=True)
    
    for comp_idx, _ in valid_components[:keep_count]:
        result[labels == comp_idx] = True
        
    return raw, result

def distance(a: tuple[float, float], b: tuple[float, float]) -> float:
    return math.hypot(a[0] - b[0], a[1] - b[1])

def trace_dijkstra(skeleton: np.ndarray, start_node: dict, end_node: dict, config: dict) -> list[tuple[int, int]]:
    h, w = skeleton.shape
    roi = config["roi"]
    
    start_y = int(start_node["y"] - roi["y"])
    start_x = int(start_node["x"] - roi["x"])
    end_y = int(end_node["y"] - roi["y"])
    end_x = int(end_node["x"] - roi["x"])
    
    start_y = max(0, min(h - 1, start_y))
    start_x = max(0, min(w - 1, start_x))
    end_y = max(0, min(h - 1, end_y))
    end_x = max(0, min(w - 1, end_x))
    
    dist = np.full((h, w), np.inf)
    parent = {}
    
    dist[start_y, start_x] = 0.0
    pq = [(0.0, start_y, start_x)]
    
    # Mask of exclusion circles to allow cheaper passage through nodes
    circles_mask = np.zeros((h, w), dtype=np.uint8)
    for circle in config.get("excludeCircles", []):
        cx = int(circle["x"] - roi["x"])
        cy = int(circle["y"] - roi["y"])
        r = int(circle["r"])
        cv2.circle(circles_mask, (cx, cy), r, 1, -1)
        
    while pq:
        d, y, x = heapq.heappop(pq)
        
        if d > dist[y, x]:
            continue
            
        if y == end_y and x == end_x:
            break
            
        for dy in (-1, 0, 1):
            for dx in (-1, 0, 1):
                if dy == 0 and dx == 0:
                    continue
                ny, nx = y + dy, x + dx
                if 0 <= ny < h and 0 <= nx < w:
                    step_len = math.hypot(dx, dy)
                    if skeleton[ny, nx]:
                        weight = 1.0
                    elif circles_mask[ny, nx]:
                        weight = 4.0  # Moderate cost inside exclusion circles
                    else:
                        weight = 800.0  # High penalty outside centerline and circles
                        
                    nd = d + weight * step_len
                    if nd < dist[ny, nx]:
                        dist[ny, nx] = nd
                        parent[(ny, nx)] = (y, x)
                        heapq.heappush(pq, (nd, ny, nx))
                        
    path = []
    curr = (end_y, end_x)
    if curr in parent or curr == (start_y, start_x):
        while curr != (start_y, start_x):
            path.append(curr)
            curr = parent[curr]
        path.append((start_y, start_x))
        path.reverse()
        
    return path

def compute_bezier_segments(points: list[tuple[float, float]], smoothing: float) -> list[tuple[tuple[float, float], tuple[float, float], tuple[float, float], tuple[float, float]]]:
    segments = []
    tension = smoothing / 6.0
    for i in range(len(points) - 1):
        p0 = points[max(i - 1, 0)]
        p1 = points[i]
        p2 = points[i + 1]
        p3 = points[min(i + 2, len(points) - 1)]
        c1 = (p1[0] + (p2[0] - p0[0]) * tension, p1[1] + (p2[1] - p0[1]) * tension)
        c2 = (p2[0] - (p3[0] - p1[0]) * tension, p2[1] - (p3[1] - p1[1]) * tension)
        segments.append((p1, c1, c2, p2))
    return segments

def bezier_segments_to_path_d(segments: list[tuple[tuple[float, float], tuple[float, float], tuple[float, float], tuple[float, float]]]) -> str:
    if not segments:
        return ""
    start_point = segments[0][0]
    d = [f"M {start_point[0]:.1f} {start_point[1]:.1f}"]
    for _, c1, c2, p2 in segments:
        d.append(f"C {c1[0]:.1f} {c1[1]:.1f} {c2[0]:.1f} {c2[1]:.1f} {p2[0]:.1f} {p2[1]:.1f}")
    return " ".join(d)

def main() -> None:
    config = load_config(CONFIG_PATH)
    image_path = ROOT / config["image"]
    if not image_path.exists():
        raise FileNotFoundError(f"Image not found at {image_path}")
        
    image = Image.open(image_path).convert("RGB")
    image_rgb = np.array(image)
    
    # 1. Mask Extraction
    raw_mask, clean_mask = build_masks(image_rgb, config)
    
    # 2. Skeleton / Centerline
    skeleton = skeletonize(clean_mask)
    
    # Extract centers in layout sequence
    nodes_order = ["hello", "read", "strings", "functions", "compare", "mini", "cli"]
    circles_dict = {c["id"]: c for c in config.get("excludeCircles", [])}
    
    # 3. Dijkstra Centerline Tracing
    stitched_path = []
    segments_pts = []
    for i in range(len(nodes_order) - 1):
        node_a = circles_dict[nodes_order[i]]
        node_b = circles_dict[nodes_order[i + 1]]
        segment = trace_dijkstra(skeleton, node_a, node_b, config)
        
        # Convert to global coordinates
        global_segment = [(x + config["roi"]["x"], y + config["roi"]["y"]) for y, x in segment]
        segments_pts.append(global_segment)
        
        if not stitched_path:
            stitched_path.extend(global_segment)
        else:
            stitched_path.extend(global_segment[1:])
            
    # Filter duplicate points
    unique_path = []
    for pt in stitched_path:
        if not unique_path or distance(pt, unique_path[-1]) > 0.01:
            unique_path.append(pt)
            
    # 4. Fit C2 Cubic Spline to sampled guide points (interpolating nodes exactly with 0px error)
    x_coords = [p[0] for p in unique_path]
    y_coords = [p[1] for p in unique_path]
    
    # Keep a copy of unsmoothed/directly B-spline snapped points for raw baseline diagnostics
    # This represents the old V2 behavior (with kinks) for the curvature check plot
    num_samples = 150
    u_new = np.linspace(0, 1, num_samples)
    tck_raw, u_raw = splprep([x_coords, y_coords], s=25.0)
    x_raw, y_raw = splev(u_new, tck_raw)
    raw_spline_points = list(zip(x_raw, y_raw))
    for node_name in nodes_order:
        node_center = (circles_dict[node_name]["x"], circles_dict[node_name]["y"])
        best_idx = min(range(len(raw_spline_points)), key=lambda idx: distance(raw_spline_points[idx], node_center))
        raw_spline_points[best_idx] = node_center

    # Sample guide points along the Dijkstra segments
    target_spacing = float(config.get("guidePointSpacing", 40.0))
    all_sampled_points = []
    node_indices = []
    
    for i, seg in enumerate(segments_pts):
        node_name = nodes_order[i]
        # Calculate cumulative distance along segment
        dists = [0.0]
        for k in range(1, len(seg)):
            dists.append(dists[-1] + distance(seg[k], seg[k-1]))
        total_len = dists[-1]
        
        # Decide number of subdivisions
        m = max(1, int(round(total_len / target_spacing)))
        
        seg_sampled = []
        for j in range(m):
            target_d = (j / m) * total_len
            idx = np.searchsorted(dists, target_d)
            if idx == 0:
                pt = seg[0]
            elif idx >= len(seg):
                pt = seg[-1]
            else:
                d0 = dists[idx-1]
                d1 = dists[idx]
                frac = (target_d - d0) / (d1 - d0)
                p0 = seg[idx-1]
                p1 = seg[idx]
                pt = (p0[0] + frac*(p1[0]-p0[0]), p0[1] + frac*(p1[1]-p0[1]))
            seg_sampled.append(pt)
            
        node_indices.append(len(all_sampled_points))
        all_sampled_points.extend(seg_sampled)
        
    # Add final node
    node_indices.append(len(all_sampled_points))
    all_sampled_points.append(segments_pts[-1][-1])
    
    # Fit Cubic Spline parameterized by cumulative chordal length
    pts_arr = np.array(all_sampled_points)
    dx = np.diff(pts_arr[:, 0])
    dy = np.diff(pts_arr[:, 1])
    ds = np.sqrt(dx**2 + dy**2)
    s = np.concatenate(([0.0], np.cumsum(ds)))
    
    cs_x = CubicSpline(s, pts_arr[:, 0], bc_type="not-a-knot")
    cs_y = CubicSpline(s, pts_arr[:, 1], bc_type="not-a-knot")
    
    # Evaluate Cubic Spline at 150 dense points for rendering and plots
    s_dense = np.linspace(0, s[-1], num_samples)
    dense_x = cs_x(s_dense)
    dense_y = cs_y(s_dense)
    spline_points = list(zip(dense_x, dense_y))
    
    snapped_indices = {
        "hello": node_indices[0],
        "read": node_indices[1],
        "strings": node_indices[2],
        "functions": node_indices[3],
        "compare": node_indices[4],
        "mini": node_indices[5],
        "cli": node_indices[6],
    }
    
    # 5. Convert Cubic Spline segments to exact Cubic Bezier curves
    bezier_segments = []
    n_segments = len(s) - 1
    for i in range(n_segments):
        h = s[i+1] - s[i]
        
        p1 = (float(pts_arr[i, 0]), float(pts_arr[i, 1]))
        p2 = (float(pts_arr[i+1, 0]), float(pts_arr[i+1, 1]))
        
        cx = cs_x.c[:, i]
        cy = cs_y.c[:, i]
        
        dx_start = cx[2]
        dy_start = cy[2]
        
        dx_end = 3 * cx[0] * h**2 + 2 * cx[1] * h + cx[2]
        dy_end = 3 * cy[0] * h**2 + 2 * cy[1] * h + cy[2]
        
        t1 = (dx_start * h, dy_start * h)
        t2 = (dx_end * h, dy_end * h)
        
        c1 = (p1[0] + t1[0] / 3.0, p1[1] + t1[1] / 3.0)
        c2 = (p2[0] - t2[0] / 3.0, p2[1] - t2[1] / 3.0)
        
        bezier_segments.append((p1, c1, c2, p2))
        
    idx_read = node_indices[1]
    idx_strings = node_indices[2]
    
    path_d = bezier_segments_to_path_d(bezier_segments)
    completed_d = bezier_segments_to_path_d(bezier_segments[:idx_read])
    active_d = bezier_segments_to_path_d(bezier_segments[idx_read:idx_strings])
    locked_d = bezier_segments_to_path_d(bezier_segments[idx_strings:])
    
    # 6. Save Diagnostic Outputs
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    
    size = (image_rgb.shape[1], image_rgb.shape[0])
    
    # Draw full masks
    roi = config["roi"]
    raw_full = np.zeros((size[1], size[0]), dtype=np.uint8)
    raw_full[roi["y"] : roi["y"] + roi["h"], roi["x"] : roi["x"] + roi["w"]] = raw_mask.astype(np.uint8) * 255
    
    clean_full = np.zeros((size[1], size[0]), dtype=np.uint8)
    clean_full[roi["y"] : roi["y"] + roi["h"], roi["x"] : roi["x"] + roi["w"]] = clean_mask.astype(np.uint8) * 255
    
    skeleton_full = np.zeros((size[1], size[0]), dtype=np.uint8)
    skeleton_full[roi["y"] : roi["y"] + roi["h"], roi["x"] : roi["x"] + roi["w"]] = skeleton.astype(np.uint8) * 255
    
    Image.fromarray(raw_full).save(OUTPUT_DIR / "snake_mask.png")
    Image.fromarray(clean_full).save(OUTPUT_DIR / "snake_mask_clean.png")
    Image.fromarray(skeleton_full).save(OUTPUT_DIR / "snake_skeleton.png")
    
    # Save to public assets for UI debugging
    assets_debug = ROOT / "assets" / "debug"
    assets_debug.mkdir(parents=True, exist_ok=True)
    Image.fromarray(clean_full).save(assets_debug / "snake-mask-v2.png")
    Image.fromarray(skeleton_full).save(assets_debug / "snake-skeleton-v2.png")
    
    # Save points list JSON
    raw_points_list = [{"x": float(x), "y": float(y)} for x, y in unique_path]
    (OUTPUT_DIR / "snake_points.json").write_text(json.dumps(raw_points_list, indent=2), encoding="utf-8")
    
    ordered_points_list = [{"x": float(x), "y": float(y)} for x, y in spline_points]
    (OUTPUT_DIR / "snake_points_smoothed.json").write_text(json.dumps(ordered_points_list, indent=2), encoding="utf-8")
    (OUTPUT_DIR / "snake_points_dense.json").write_text(json.dumps(ordered_points_list, indent=2), encoding="utf-8")
    
    # Save path JSON
    path_json = {
        "full_path_d": path_d,
        "completed_path_d": completed_d,
        "active_path_d": active_d,
        "locked_path_d": locked_d,
        "width": float(config["outputStrokeWidth"])
    }
    (OUTPUT_DIR / "snake_path_v2.json").write_text(json.dumps(path_json, indent=2), encoding="utf-8")
    (OUTPUT_DIR / "snake_path_smoothed.json").write_text(json.dumps(path_json, indent=2), encoding="utf-8")
    
    # Save SVG preview file (smoothed)
    dots_svg = "\n".join(f'<circle cx="{x:.1f}" cy="{y:.1f}" r="3" fill="#ffd84a" />' for x, y in spline_points)
    svg_content = f"""<svg xmlns="http://www.w3.org/2000/svg" width="1086" height="1448" viewBox="0 0 1086 1448">
  <rect width="1086" height="1448" fill="#071014" />
  <path d="{path_d}" fill="none" stroke="#00f5e8" stroke-width="{config['outputStrokeWidth']}" stroke-linecap="round" stroke-linejoin="round" opacity="0.8" />
  {dots_svg}
</svg>
"""
    (OUTPUT_DIR / "snake_path_v2_smoothed.svg").write_text(svg_content, encoding="utf-8")
    (OUTPUT_DIR / "snake_path_smoothed.svg").write_text(svg_content, encoding="utf-8")
    
    # Draw comparison overlay image (smoothed)
    overlay = image_rgb.copy()
    # Draw spline path in bright cyan
    for i in range(len(spline_points) - 1):
        p1 = spline_points[i]
        p2 = spline_points[i + 1]
        cv2.line(overlay, (int(round(p1[0])), int(round(p1[1]))), (int(round(p2[0])), int(round(p2[1]))), (0, 245, 232), 3)
    # Draw node circles
    for circle in config.get("excludeCircles", []):
        cv2.circle(overlay, (int(circle["x"]), int(circle["y"])), 8, (255, 216, 74), -1)
    Image.fromarray(overlay).save(OUTPUT_DIR / "comparison_overlay_smoothed.png")
    
    # Save also as public asset overlay for reference checks in the UI if needed
    Image.fromarray(overlay).save(assets_debug / "snake-overlay-debug-v2.png")
    Image.fromarray(overlay).save(assets_debug / "comparison_overlay_smoothed.png")

    # 7. Generate Curvature, Thickness and Anchor Diagnostic Plots using Matplotlib
    try:
        raw_curvatures = compute_curvature(raw_spline_points)
        smoothed_curvatures = compute_curvature(spline_points)
        
        plot_curvature(raw_curvatures, smoothed_curvatures, OUTPUT_DIR / "curvature_debug.png")
        plot_thickness(image_rgb, OUTPUT_DIR / "thickness_debug.png", float(config["outputStrokeWidth"]))
        plot_anchors(all_sampled_points, node_indices, cs_x, cs_y, s[-1], OUTPUT_DIR / "anchor_debug.png", nodes_order)
        
        # Save plots to public assets too so the UI can optionally display them
        plot_curvature(raw_curvatures, smoothed_curvatures, assets_debug / "curvature_debug.png")
        plot_thickness(image_rgb, assets_debug / "thickness_debug.png", float(config["outputStrokeWidth"]))
        plot_anchors(all_sampled_points, node_indices, cs_x, cs_y, s[-1], assets_debug / "anchor_debug.png", nodes_order)
        
        print("Curvature, thickness, and anchor plots generated successfully.")
    except Exception as plot_err:
        print(f"Warning: Failed to generate diagnostic plots: {plot_err}")

    raw_segments = compute_bezier_segments(raw_spline_points, float(config["smoothingFactor"]))
    raw_path_d = bezier_segments_to_path_d(raw_segments)

    # 8. Write TypeScript generated module
    ts_content = "\n".join(
        [
            "// Auto-generated by tools/extract_snake_v2.py. Do not edit directly.",
            "export type GeneratedSnakeV2Point = { x: number; y: number };",
            "",
            "export const GENERATED_SNAKE_V2_POINTS: GeneratedSnakeV2Point[] = [",
            *[f"  {{ x: {p[0]:.2f}, y: {p[1]:.2f} }}," for p in spline_points],
            "];",
            "",
            f"export const GENERATED_SNAKE_V2_PATH_D = {json.dumps(path_d)};",
            f"export const GENERATED_SNAKE_V2_RAW_PATH_D = {json.dumps(raw_path_d)};",
            f"export const GENERATED_SNAKE_V2_COMPLETED_PATH_D = {json.dumps(completed_d)};",
            f"export const GENERATED_SNAKE_V2_ACTIVE_PATH_D = {json.dumps(active_d)};",
            f"export const GENERATED_SNAKE_V2_LOCKED_PATH_D = {json.dumps(locked_d)};",
            f"export const GENERATED_SNAKE_V2_WIDTH = {float(config['outputStrokeWidth']):.1f};",
            "",
        ]
    )
    PATH_TS_PATH.write_text(ts_content, encoding="utf-8")
    
    print("Snake exact trace v2 pipeline completed successfully!")
    print(f"Artifacts exported to: {OUTPUT_DIR.relative_to(ROOT)}")
    print(f"TypeScript generator written to: {PATH_TS_PATH.relative_to(ROOT)}")

def compute_curvature(points: list[tuple[float, float]]) -> list[float]:
    n = len(points)
    curvatures = [0.0] * n
    for i in range(1, n - 1):
        p_prev = points[i - 1]
        p_curr = points[i]
        p_next = points[i + 1]
        
        v1 = (p_curr[0] - p_prev[0], p_curr[1] - p_prev[1])
        v2 = (p_next[0] - p_curr[0], p_next[1] - p_curr[1])
        
        len_v1 = math.hypot(*v1)
        len_v2 = math.hypot(*v2)
        
        if len_v1 < 1e-5 or len_v2 < 1e-5:
            continue
            
        dot = v1[0]*v2[0] + v1[1]*v2[1]
        cos_theta = dot / (len_v1 * len_v2)
        cos_theta = max(-1.0, min(1.0, cos_theta))
        curvatures[i] = math.degrees(math.acos(cos_theta))
    return curvatures

def plot_curvature(raw_curvatures: list[float], smoothed_curvatures: list[float], output_path: Path) -> None:
    import matplotlib.pyplot as plt
    plt.figure(figsize=(10, 4))
    plt.plot(raw_curvatures, label="Original Snapped Curvature (with Kinks)", color="#ff7b72", alpha=0.6, linestyle=":")
    plt.plot(smoothed_curvatures, label="C2 Cubic Spline Curvature (Perfect Smooth)", color="#00f5e8", linewidth=2)
    plt.title("Centerline Curvature Profile (Kinks Check)")
    plt.xlabel("Spline Point Index")
    plt.ylabel("Curvature Angle (Degrees)")
    plt.grid(True, linestyle=":", alpha=0.5)
    plt.legend()
    plt.tight_layout()
    plt.savefig(str(output_path), dpi=150)
    plt.close()

def plot_thickness(image_rgb: np.ndarray, output_path: Path, width: float) -> None:
    import matplotlib.pyplot as plt
    row = image_rgb[460, 250:320]
    x_coords = np.arange(250, 320)
    brightness = [int(p[0])*0.299 + int(p[1])*0.587 + int(p[2])*0.114 for p in row]
    
    plt.figure(figsize=(10, 5))
    plt.plot(x_coords, brightness, label="Reference Brightness Profile (y=460)", color="#00f5e8", linewidth=2)
    
    # We find the peak brightness in the row
    peak_idx = np.argmax(brightness)
    center_x = 250 + peak_idx
    
    plt.axvline(center_x, color="red", linestyle="--", label=f"Snake Center (x={center_x})")
    plt.axvspan(center_x - width/2, center_x + width/2, color="yellow", alpha=0.3, label=f"Calibrated Body Width ({width}px)")
    plt.axvspan(center_x - 10.0, center_x + 10.0, color="cyan", alpha=0.1, label="Calibrated Glow Width (20px)")
    
    plt.title("Snake Cross-Section Brightness vs SVG Calibration")
    plt.xlabel("Coordenada X (px)")
    plt.ylabel("Brillo Visual (0-255)")
    plt.grid(True, linestyle=":", alpha=0.6)
    plt.legend()
    plt.tight_layout()
    plt.savefig(str(output_path), dpi=150)
    plt.close()

def plot_anchors(sampled_pts: list[tuple[float, float]], node_indices: list[int], cs_x, cs_y, total_s: float, output_path: Path, nodes_order: list[str]) -> None:
    import matplotlib.pyplot as plt
    
    # Generate dense points
    s_dense = np.linspace(0, total_s, 300)
    dense_x = cs_x(s_dense)
    dense_y = cs_y(s_dense)
    
    plt.figure(figsize=(8, 10))
    # Plot spline path
    plt.plot(dense_x, dense_y, label="C2 Cubic Spline Path", color="#00f5e8", linewidth=2)
    
    # Plot sampled guide points
    sampled_arr = np.array(sampled_pts)
    plt.scatter(sampled_arr[:, 0], sampled_arr[:, 1], color="#1f77b4", s=30, label="Guide Points", zorder=3)
    
    # Plot node centers
    for idx, name in zip(node_indices, nodes_order):
        node_pt = sampled_pts[idx]
        plt.scatter(node_pt[0], node_pt[1], color="#ffd84a", s=100, edgecolor="black", linewidth=1.5, label=f"Node: {name}" if name == "hello" else "", zorder=4)
        plt.text(node_pt[0] + 12, node_pt[1], name, fontsize=9, color="#ffd84a", weight="bold")
        
    plt.title("Anchor & Guide Points Distribution")
    plt.xlabel("X (px)")
    plt.ylabel("Y (px)")
    plt.gca().invert_yaxis() # Match image coordinate system where Y grows downwards
    plt.grid(True, linestyle=":", alpha=0.5)
    plt.legend()
    plt.tight_layout()
    plt.savefig(str(output_path), dpi=150)
    plt.close()

if __name__ == "__main__":
    main()
