from __future__ import annotations

import json
from pathlib import Path

import cv2
import numpy as np
from PIL import Image, ImageDraw, ImageFont


ROOT = Path(__file__).resolve().parents[1]
REFERENCE = ROOT / "assets" / "reference-cadenas.png"
OUTPUT_DIR = ROOT / "tools" / "output" / "color-fx-calibration"
PALETTE_JSON = OUTPUT_DIR / "palette.json"
PALETTE_CSS = OUTPUT_DIR / "palette.css"
SAMPLES_PNG = OUTPUT_DIR / "color_samples.png"
ROI_DEBUG_PNG = OUTPUT_DIR / "roi_debug.png"


ROIS = {
    "background_base": (10, 30, 160, 210),
    "background_radial_left": (132, 312, 220, 720),
    "background_radial_right": (650, 320, 315, 760),
    "panel_progress": (618, 140, 412, 190),
    "header_buttons": (30, 40, 260, 92),
    "bottom_nav": (38, 1280, 1010, 118),
    "active_snake_core": (270, 380, 240, 320),
    "active_snake_glow": (230, 350, 330, 400),
    "locked_snake_core": (285, 705, 250, 470),
    "locked_snake_glow": (250, 680, 310, 520),
    "completed_node_core": (245, 360, 240, 250),
    "completed_node_glow": (210, 330, 310, 310),
    "current_node_outline": (350, 620, 180, 170),
    "locked_node_gray": (265, 800, 240, 330),
    "text_white": (650, 360, 330, 420),
    "text_muted": (650, 420, 330, 560),
    "cyan_accent": (650, 410, 260, 330),
    "abc_illustration_glow": (730, 420, 260, 330),
    "tiny_sparkles_stars": (780, 100, 250, 420),
}

TOKEN_MAP = {
    "background_base": "--bg-base",
    "background_radial_left": "--bg-radial-left",
    "background_radial_right": "--bg-radial-right",
    "panel_progress": "--panel-bg",
    "header_buttons": "--header-button-bg",
    "bottom_nav": "--bottom-nav-bg",
    "active_snake_core": "--cyan-core",
    "active_snake_glow": "--cyan-glow",
    "locked_snake_core": "--snake-lock-core",
    "locked_snake_glow": "--snake-lock-glow",
    "completed_node_core": "--node-green-core",
    "completed_node_glow": "--node-green-edge",
    "current_node_outline": "--current-ring",
    "locked_node_gray": "--node-lock-core",
    "text_white": "--text-main",
    "text_muted": "--text-muted",
    "cyan_accent": "--text-cyan",
    "abc_illustration_glow": "--abc-glow",
    "tiny_sparkles_stars": "--sparkle-core",
}


def rgb_to_hex(rgb: np.ndarray | tuple[int, int, int]) -> str:
    arr = np.asarray(rgb, dtype=np.uint8)
    return f"#{arr[0]:02x}{arr[1]:02x}{arr[2]:02x}"


def luminance(rgb: np.ndarray) -> np.ndarray:
    return rgb[..., 0] * 0.2126 + rgb[..., 1] * 0.7152 + rgb[..., 2] * 0.0722


def choose_pixels(name: str, crop: np.ndarray) -> np.ndarray:
    hsv = cv2.cvtColor(crop, cv2.COLOR_RGB2HSV)
    sat = hsv[..., 1]
    val = hsv[..., 2]
    hue = hsv[..., 0] * 2
    lum = luminance(crop)
    mask = np.ones(crop.shape[:2], dtype=bool)

    if "background" in name:
        mask = lum < np.percentile(lum, 55)
    elif "glow" in name or "cyan" in name or "snake" in name or "abc" in name or "sparkle" in name:
        mask = (sat > 32) & (val > 42) & (hue >= 105) & (hue <= 205)
    elif "node_green" in name or "completed_node" in name:
        mask = (sat > 45) & (val > 70) & (hue >= 120) & (hue <= 180)
    elif "locked" in name or "gray" in name:
        mask = (val > 35) & (val < 210) & (sat < 105)
    elif "text_white" in name:
        mask = (sat < 70) & (val > 170)
    elif "text_muted" in name:
        mask = (sat < 95) & (val > 88) & (val < 190)
    elif "panel" in name or "button" in name or "nav" in name:
        mask = (val > 8) & (val < 110)

    pixels = crop[mask]
    if len(pixels) < 25:
        pixels = crop.reshape(-1, 3)
    return pixels


def kmeans_color(pixels: np.ndarray, cluster_count: int = 3) -> tuple[np.ndarray, list[dict]]:
    data = pixels.astype(np.float32)
    criteria = (cv2.TERM_CRITERIA_EPS + cv2.TERM_CRITERIA_MAX_ITER, 60, 0.2)
    k = min(cluster_count, len(data))
    _, labels, centers = cv2.kmeans(data, k, None, criteria, 4, cv2.KMEANS_PP_CENTERS)
    counts = np.bincount(labels.flatten(), minlength=k)
    order = np.argsort(counts)[::-1]
    clusters = []
    for idx in order:
        center = np.clip(np.round(centers[idx]), 0, 255).astype(np.uint8)
        clusters.append({"hex": rgb_to_hex(center), "count": int(counts[idx]), "ratio": round(float(counts[idx] / len(data)), 4)})
    dominant_hex = clusters[0]["hex"].lstrip("#")
    dominant_rgb = np.array([int(dominant_hex[i : i + 2], 16) for i in (0, 2, 4)], dtype=np.uint8)
    return dominant_rgb, clusters


def main() -> None:
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    image = np.array(Image.open(REFERENCE).convert("RGB"))
    debug = Image.fromarray(image).convert("RGBA")
    draw = ImageDraw.Draw(debug)
    font = ImageFont.load_default()
    palette = {"source": str(REFERENCE.relative_to(ROOT)), "zones": {}, "tokens": {}}

    for index, (name, (x, y, w, h)) in enumerate(ROIS.items()):
        crop = image[y : y + h, x : x + w]
        pixels = choose_pixels(name, crop)
        median = np.median(pixels, axis=0).astype(np.uint8)
        p75 = np.percentile(pixels, 75, axis=0).astype(np.uint8)
        dominant, clusters = kmeans_color(pixels)
        token = TOKEN_MAP[name]
        chosen = dominant
        if name in {"text_white", "current_node_outline", "tiny_sparkles_stars"}:
            chosen = p75
        if name in {"background_base", "panel_progress", "header_buttons", "bottom_nav"}:
            chosen = median
        hex_value = rgb_to_hex(chosen)
        palette["zones"][name] = {
            "roi": {"x": x, "y": y, "w": w, "h": h},
            "token": token,
            "hex": hex_value,
            "median": rgb_to_hex(median),
            "p75": rgb_to_hex(p75),
            "dominantClusters": clusters,
            "sampleCount": int(len(pixels)),
        }
        palette["tokens"][token] = hex_value
        color = tuple(int(c) for c in chosen) + (220,)
        draw.rectangle((x, y, x + w, y + h), outline=color, width=3)
        draw.text((x + 4, y + 4), name, fill=color, font=font)

    palette["tokens"].update(
        {
            "--bg-deep": "#010608",
            "--panel-border": "#335664",
            "--cyan-muted": "#5daeb6",
            "--glow-strong": "rgba(18, 255, 220, 0.62)",
            "--glow-soft": "rgba(18, 255, 220, 0.18)",
            "--shadow-deep": "rgba(0, 0, 0, 0.58)",
        }
    )

    PALETTE_JSON.write_text(json.dumps(palette, indent=2), encoding="utf-8")
    css_lines = [":root {"]
    for token, value in palette["tokens"].items():
        css_lines.append(f"  {token}: {value};")
    css_lines.append("}")
    PALETTE_CSS.write_text("\n".join(css_lines) + "\n", encoding="utf-8")
    debug.save(ROI_DEBUG_PNG)

    swatch_w = 260
    swatch_h = 56
    sample = Image.new("RGB", (swatch_w * 2, swatch_h * len(palette["zones"])), "#071014")
    sample_draw = ImageDraw.Draw(sample)
    for row, (name, info) in enumerate(palette["zones"].items()):
        y = row * swatch_h
        color = info["hex"]
        sample_draw.rectangle((0, y, swatch_w, y + swatch_h), fill=color)
        sample_draw.text((swatch_w + 12, y + 8), f"{info['token']} {color}", fill="#eaffff", font=font)
        sample_draw.text((swatch_w + 12, y + 28), name, fill="#9cb8bf", font=font)
    sample.save(SAMPLES_PNG)
    print(json.dumps({"palette": str(PALETTE_JSON.relative_to(ROOT)), "css": str(PALETTE_CSS.relative_to(ROOT))}, indent=2))


if __name__ == "__main__":
    main()
