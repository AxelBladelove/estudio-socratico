# Visual Implementation Pass 1

Source audit: `docs/visual-audit-pass-1.md`

Goal: improve the liquid/neon material of the snake and nodes while keeping layout, text, labels, ABC illustration geometry/style, and path geometry stable.

## Files Changed

- `src/components/LessonPath.tsx`
- `src/lib/snake.ts`
- `src/styles.css`

## Build

Command:

```powershell
bun run build
```

Result: passed.

## Visual Artifacts

- `tools/output/visual-pass-1/before-side-by-side.png`
- `tools/output/visual-pass-1/after-side-by-side.png`
- `tools/output/visual-pass-1/before-after-comparison.png`
- `tools/output/visual-pass-1/before-after-diff-amplified.png`
- `tools/output/visual-pass-1/after-reference-vs-ui-diff-amplified.png`

Measured crop delta, before UI vs after UI:

```txt
mean_rgb_delta: [0.71, 1.29, 1.22]
rms_rgb_delta: [2.5, 3.08, 2.87]
```

Measured crop diff, reference vs final UI:

```txt
mean_rgb_diff: [6.54, 11.35, 12.57]
rms_rgb_diff: [24.62, 27.77, 27.31]
```

The global metric is not the acceptance target for this pass because the ABC illustration was intentionally ignored and still dominates part of the diff. Visual validation focused on the snake/nodes/fondo material regions.

## What Changed

### Snake Path

- Added `snakeTightBloom` for a closer, less cloudy bloom layer.
- Reduced the displacement/blur strength in `snakeOrganicGlow` to avoid the previous soft haze feeling.
- Added `snakeContactBloom` for localized light accumulation near node/path connections.
- Added new `snakeRimActive` and `snakeRimLocked` gradients.
- Added `.path-rim` layers between body and highlight for a more defined cyan edge.
- Rebalanced `SNAKE_CONFIG`:
  - narrower shadow and smoke;
  - stronger but controlled body/rim/highlight contrast;
  - lower smoke opacity;
  - slightly stronger sparkle/highlight.
- Enabled and tuned microflares on the snake instead of leaving them disabled.
- Added localized connector bloom ellipses around completed/current/locked transitions.

### Current Node

- Strengthened the white-cyan ring with a clearer exterior line.
- Added more internal depth through darker lower radial shading.
- Increased controlled bloom without making a huge cloud.
- Improved side dot material with a radial highlight, darker rim, inner shadow, and stronger contained glow.

### Completed Nodes

- Made the ring brighter and more defined.
- Added stronger specular highlight top-left.
- Increased interior depth with darker lower shadow.
- Cleaned the check glow and stroke presence.
- Reworked pseudo-element halos so they read more like energy rings and less like generic blur.

### Locked Nodes

- Shifted material toward colder metallic glass.
- Increased top/edge highlight.
- Deepened the center/lower shading.
- Made the lock icon slightly clearer.
- Added a very subtle cyan-cold glow without turning locked nodes green.

### Background

- Reduced large green/cyan radial washes in `.app` and `.background-layer`.
- Kept the deep blue-petroleum atmosphere.
- Added slightly finer particle texture with lower-radius dots.
- Preserved the ghost code layer.

## CSS/SVG Usage

This pass used CSS and inline SVG filters/layers:

- SVG filters: `feGaussianBlur`, `feTurbulence`, `feDisplacementMap`, `feMerge`.
- SVG gradients: active/locked body, active/locked rim, connector bloom gradients.
- CSS material layers: conic gradients, radial gradients, inset shadows, drop shadows, blend modes, dash patterns.
- No path geometry changes.

## PixiJS / WebGL

PixiJS/WebGL was not added in this pass.

Reason: the target changes were static material/rendering changes tightly coupled to SVG strokes and CSS node layers. Adding PixiJS now would introduce a separate canvas compositing layer, possible z-index/scale drift, and more performance risk before exhausting the SVG/CSS material stack. The installed `pixijs` skill is still useful for a later ambient particle or smoke layer if we decide CSS/SVG cannot produce enough organic atmosphere.

## What Improved

- Snake has a clearer edge/body/highlight separation.
- Smoke is less uniform and less cloudy.
- Node/path transitions now have localized energy instead of relying only on global glow.
- Completed nodes have more premium material contrast.
- Current node has a cleaner focal ring and better side dot.
- Locked nodes read more like cold glass/metal, less like flat disabled buttons.
- Background is less green-washed and keeps energy closer to the learning path.

## Still Pending For Pass 2

- ABC illustration was intentionally untouched and still competes visually with the path/current node.
- Current node interior icon still needs a separate pass if we want closer reference parity.
- Snake highlights need manual placement refinement per curve; current dash patterns are improved but not pixel-matched.
- Completed-to-current transition could use one or two more hand-placed glints.
- Bottom nav/progress/header glass were not tuned in this pass.
- A PixiJS ambient layer can be explored later for very subtle floating particles/smoke behind the SVG, but should not replace the SVG snake.
