# Visual Effects

CSS visual effects for premium design: clip-path, masks, filters, and blend modes.

## Table of Contents

1. [Clip-Path Animations](#clip-path-animations)
2. [CSS Masks](#css-masks)
3. [Backdrop Filter](#backdrop-filter)
4. [Mix Blend Mode](#mix-blend-mode)
5. [Combined Effects](#combined-effects)

---

## Clip-Path Animations

### Basic Shapes

```css
/* Circle */
.circle-reveal {
  clip-path: circle(0% at 50% 50%);
  transition: clip-path 0.6s ease;
}
.circle-reveal.visible {
  clip-path: circle(100% at 50% 50%);
}

/* Ellipse */
.ellipse-reveal {
  clip-path: ellipse(0% 0% at 50% 50%);
  transition: clip-path 0.6s ease;
}
.ellipse-reveal.visible {
  clip-path: ellipse(100% 100% at 50% 50%);
}

/* Inset (Rectangle) */
.inset-reveal {
  clip-path: inset(0 100% 0 0);  /* Hidden from right */
  transition: clip-path 0.6s ease;
}
.inset-reveal.visible {
  clip-path: inset(0 0 0 0);
}
```

### Directional Reveals

```css
/* Left to right */
.reveal-ltr {
  clip-path: inset(0 100% 0 0);
}
.reveal-ltr.visible {
  clip-path: inset(0 0 0 0);
}

/* Right to left */
.reveal-rtl {
  clip-path: inset(0 0 0 100%);
}
.reveal-rtl.visible {
  clip-path: inset(0 0 0 0);
}

/* Top to bottom */
.reveal-ttb {
  clip-path: inset(0 0 100% 0);
}
.reveal-ttb.visible {
  clip-path: inset(0 0 0 0);
}

/* Bottom to top */
.reveal-btt {
  clip-path: inset(100% 0 0 0);
}
.reveal-btt.visible {
  clip-path: inset(0 0 0 0);
}
```

### Polygon Shapes

```css
/* Triangle */
.triangle {
  clip-path: polygon(50% 0%, 0% 100%, 100% 100%);
}

/* Hexagon */
.hexagon {
  clip-path: polygon(50% 0%, 100% 25%, 100% 75%, 50% 100%, 0% 75%, 0% 25%);
}

/* Arrow */
.arrow {
  clip-path: polygon(0% 20%, 60% 20%, 60% 0%, 100% 50%, 60% 100%, 60% 80%, 0% 80%);
}

/* Animated polygon */
.morph-shape {
  clip-path: polygon(50% 0%, 100% 50%, 50% 100%, 0% 50%);  /* Diamond */
  transition: clip-path 0.5s ease;
}
.morph-shape:hover {
  clip-path: polygon(0% 0%, 100% 0%, 100% 100%, 0% 100%);  /* Square */
}
```

### Image Hover Effect

```css
.image-container {
  position: relative;
  overflow: hidden;
}

.image-container img {
  clip-path: inset(0);
  transition: clip-path 0.4s ease, transform 0.4s ease;
}

.image-container:hover img {
  clip-path: inset(10px);
  transform: scale(1.05);
}
```

---

## CSS Masks

### Gradient Mask

```css
.fade-edges {
  mask-image: linear-gradient(
    to right,
    transparent,
    black 10%,
    black 90%,
    transparent
  );
}
```

### Image Mask

```css
.masked-image {
  mask-image: url('mask.svg');
  mask-size: cover;
  mask-position: center;
}
```

### Animated Mask

```css
.reveal-mask {
  mask-image: linear-gradient(
    to right,
    black 0%,
    transparent 0%
  );
  mask-size: 200% 100%;
  mask-position: 100% 0;
  transition: mask-position 0.8s ease;
}

.reveal-mask.visible {
  mask-position: 0 0;
}
```

### Text Mask (Knockout Effect)

```css
.text-knockout {
  background: url('texture.jpg') center/cover;
  -webkit-background-clip: text;
  background-clip: text;
  color: transparent;
}
```

### SVG Mask

```html
<svg width="0" height="0">
  <defs>
    <mask id="blob-mask">
      <path fill="white" d="...blob path..." />
    </mask>
  </defs>
</svg>
```

```css
.blob-masked {
  mask: url(#blob-mask);
}
```

---

## Backdrop Filter

### Frosted Glass

```css
.glass-card {
  background: rgba(255, 255, 255, 0.1);
  backdrop-filter: blur(10px) saturate(180%);
  border: 1px solid rgba(255, 255, 255, 0.2);
  border-radius: 16px;
}
```

### Dark Glass

```css
.dark-glass {
  background: rgba(0, 0, 0, 0.3);
  backdrop-filter: blur(20px);
  border: 1px solid rgba(255, 255, 255, 0.1);
}
```

### Glass Navigation

```css
.glass-nav {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  background: rgba(255, 255, 255, 0.8);
  backdrop-filter: blur(10px);
  border-bottom: 1px solid rgba(0, 0, 0, 0.1);
}

@supports not (backdrop-filter: blur(10px)) {
  .glass-nav {
    background: rgba(255, 255, 255, 0.95);
  }
}
```

### Modal Overlay

```css
.modal-backdrop {
  background: rgba(0, 0, 0, 0.5);
  backdrop-filter: blur(4px);
}
```

### Combined Filters

```css
.complex-glass {
  backdrop-filter:
    blur(10px)
    saturate(150%)
    brightness(1.1)
    contrast(1.1);
}
```

---

## Mix Blend Mode

### Text Over Image

```css
.hero {
  position: relative;
}

.hero-text {
  mix-blend-mode: difference;
  color: white;
}
```

### Color Overlay

```css
.image-with-overlay {
  position: relative;
}

.image-with-overlay::after {
  content: '';
  position: absolute;
  inset: 0;
  background: var(--color-accent);
  mix-blend-mode: multiply;
  opacity: 0.6;
}
```

### Blend Modes Reference

```css
/* Darkening modes */
.darken { mix-blend-mode: darken; }
.multiply { mix-blend-mode: multiply; }
.color-burn { mix-blend-mode: color-burn; }

/* Lightening modes */
.lighten { mix-blend-mode: lighten; }
.screen { mix-blend-mode: screen; }
.color-dodge { mix-blend-mode: color-dodge; }

/* Contrast modes */
.overlay { mix-blend-mode: overlay; }
.soft-light { mix-blend-mode: soft-light; }
.hard-light { mix-blend-mode: hard-light; }

/* Inversion modes */
.difference { mix-blend-mode: difference; }
.exclusion { mix-blend-mode: exclusion; }

/* Component modes */
.hue { mix-blend-mode: hue; }
.saturation { mix-blend-mode: saturation; }
.color { mix-blend-mode: color; }
.luminosity { mix-blend-mode: luminosity; }
```

### Duotone Effect

```css
.duotone {
  position: relative;
  background: var(--color-primary);
}

.duotone img {
  mix-blend-mode: luminosity;
  filter: grayscale(100%) contrast(1.2);
}
```

---

## Combined Effects

### Premium Card Hover

```css
.premium-card {
  position: relative;
  overflow: hidden;
  border-radius: 16px;
}

.premium-card-bg {
  position: absolute;
  inset: 0;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  clip-path: circle(0% at 50% 50%);
  transition: clip-path 0.5s cubic-bezier(0.4, 0, 0.2, 1);
}

.premium-card:hover .premium-card-bg {
  clip-path: circle(150% at 50% 50%);
}

.premium-card-content {
  position: relative;
  z-index: 1;
  padding: 2rem;
}
```

### Reveal with Glass

```css
.glass-reveal {
  background: rgba(255, 255, 255, 0.1);
  backdrop-filter: blur(10px);
  clip-path: inset(100% 0 0 0);
  transition:
    clip-path 0.6s cubic-bezier(0.4, 0, 0.2, 1),
    backdrop-filter 0.3s ease;
}

.glass-reveal.visible {
  clip-path: inset(0 0 0 0);
}
```

### Spotlight Effect

```css
.spotlight {
  position: relative;
}

.spotlight::after {
  content: '';
  position: absolute;
  inset: 0;
  background: radial-gradient(
    circle at var(--mouse-x, 50%) var(--mouse-y, 50%),
    transparent 0%,
    rgba(0, 0, 0, 0.8) 50%
  );
  pointer-events: none;
}
```

```javascript
// Track mouse for spotlight
element.addEventListener('mousemove', (e) => {
  const rect = element.getBoundingClientRect();
  const x = ((e.clientX - rect.left) / rect.width) * 100;
  const y = ((e.clientY - rect.top) / rect.height) * 100;
  element.style.setProperty('--mouse-x', `${x}%`);
  element.style.setProperty('--mouse-y', `${y}%`);
});
```

---

## Reduced Motion

```css
@media (prefers-reduced-motion: reduce) {
  .clip-animated,
  .mask-animated {
    clip-path: none;
    mask: none;
    transition: none;
  }
}
```

---

## Browser Support Notes

- **clip-path**: Full support, including animations
- **mask-image**: Use `-webkit-mask-image` for Safari
- **backdrop-filter**: Full support; provide fallback background
- **mix-blend-mode**: Full support; may affect stacking contexts

---

## Production Animation Patterns

### Shimmer Loading Skeleton

A placeholder animation for content loading states:

```css
.shimmer {
  background: linear-gradient(
    90deg,
    var(--color-surface-alt, #f2f2f2) 25%,
    var(--color-border-soft, #efefef) 50%,
    var(--color-surface-alt, #f2f2f2) 75%
  );
  background-size: 200% 100%;
  animation: shimmer 1.5s infinite;
}

@keyframes shimmer {
  0% { background-position: 200% 0; }
  100% { background-position: -200% 0; }
}

@media (prefers-reduced-motion: reduce) {
  .shimmer {
    animation: none;
    background: var(--color-surface-alt, #f2f2f2);
  }
}
```

### Hover-Lift Card

Subtle elevation on hover for interactive cards:

```css
.hover-lift {
  transition: transform 0.3s cubic-bezier(0.4, 0, 0.2, 1),
              box-shadow 0.3s cubic-bezier(0.4, 0, 0.2, 1);
}

.hover-lift:hover {
  transform: translateY(-4px);
  box-shadow: 0 8px 30px rgba(0, 0, 0, 0.08);
}

@media (prefers-reduced-motion: reduce) {
  .hover-lift:hover {
    transform: none;
  }
}
```

### CSS-Only Ticker/Marquee

Infinite horizontal scroll for testimonial strips, partner logos, or announcement bars:

```css
.ticker {
  overflow: hidden;
}

.ticker-track {
  display: flex;
  width: max-content;
  animation: ticker-scroll 40s linear infinite;
}

@keyframes ticker-scroll {
  from { transform: translateX(0); }
  to { transform: translateX(-50%); }
}

@media (prefers-reduced-motion: reduce) {
  .ticker-track {
    animation: none;
  }
}
```

Duplicate the content inside `.ticker-track` so there's no visible gap when the animation loops. The `-50%` translation moves exactly one full copy's width.

### Pulse Dot (Urgency Indicator)

For live status indicators or scarcity badges:

```css
.pulse-dot {
  width: 6px;
  height: 6px;
  border-radius: 50%;
  background: currentColor;
  animation: pulse 2s ease-in-out infinite;
}

@keyframes pulse {
  0%, 100% { opacity: 1; transform: scale(1); }
  50% { opacity: 0.5; transform: scale(0.75); }
}

@media (prefers-reduced-motion: reduce) {
  .pulse-dot {
    animation: none;
  }
}
```

---

## Micro-Interaction Patterns

Production CSS micro-interactions that create premium feel. All use CSS transitions (no JavaScript).

### Rolling Text Hover

Nav links with dual-layer text that reveals on hover — the bottom label slides up as the top slides away:

```css
.rolling-text {
  position: relative;
  overflow: hidden;
  display: inline-block;
}

.rolling-text span {
  display: block;
  transition: transform 0.35s cubic-bezier(0.22, 1, 0.36, 1);
}

.rolling-text span:last-child {
  position: absolute;
  top: 0;
  left: 0;
  transform: translateY(100%);
}

.rolling-text:hover span:first-child {
  transform: translateY(-100%);
}

.rolling-text:hover span:last-child {
  transform: translateY(0);
}
```

```html
<a class="rolling-text" href="/about">
  <span>About</span>
  <span>About</span>
</a>
```

### Icon Rotation on Hover

SVG icon rotates on button/link hover:

```css
.icon-rotate {
  display: inline-flex;
  align-items: center;
  gap: 0.5rem;
}

.icon-rotate svg {
  transition: transform 0.35s cubic-bezier(0.4, 0, 0.2, 1);
}

.icon-rotate:hover svg {
  transform: rotate(90deg);
}
```

### Condensing Navbar

Navbar shrinks in height and width on scroll (controlled by a scroll-state class in JS):

```css
.navbar {
  height: 5rem;
  max-width: 100%;
  padding: 0.75rem 2rem;
  border-radius: 0;
  transition:
    height 0.8s cubic-bezier(0.22, 1, 0.36, 1),
    max-width 0.8s cubic-bezier(0.22, 1, 0.36, 1),
    padding 0.8s cubic-bezier(0.22, 1, 0.36, 1),
    border-radius 0.8s cubic-bezier(0.22, 1, 0.36, 1),
    background-color 0.8s cubic-bezier(0.22, 1, 0.36, 1);
}

.navbar.condensed {
  height: 3.25rem;
  max-width: 48rem;
  padding: 0.375rem 1.5rem;
  border-radius: 9999px;
  background: rgba(26, 22, 20, 0.85);
  backdrop-filter: blur(12px);
}
```

### Accordion with grid-template-rows

Smooth accordion open/close without JavaScript height calculation:

```css
.accordion-content {
  display: grid;
  grid-template-rows: 0fr;
  transition: grid-template-rows 300ms cubic-bezier(0.22, 1, 0.36, 1);
}

.accordion-content.open {
  grid-template-rows: 1fr;
}

.accordion-content > div {
  overflow: hidden;
}

.accordion-icon {
  transition: transform 300ms cubic-bezier(0.22, 1, 0.36, 1);
}

.accordion.open .accordion-icon {
  transform: rotate(45deg);
}
```

The `grid-template-rows: 0fr → 1fr` trick enables smooth height animation without knowing the content height. The inner `overflow: hidden` div prevents content from being visible during the 0fr state.

### Form Input Underline Focus

Input focus effect using border-bottom transition:

```css
.input-underline {
  border: none;
  border-bottom: 1px solid var(--color-border);
  background: transparent;
  padding: 0.75rem 0;
  transition: border-color 0.3s;
}

.input-underline:focus {
  outline: none;
  border-color: var(--color-accent);
}
```

### Pill Toggle Selection

Interactive pill/tag selection with instant color flip:

```css
.pill {
  padding: 0.5rem 1rem;
  border: 1px solid var(--color-border);
  border-radius: 9999px;
  background: transparent;
  cursor: pointer;
  transition: background 0.25s, border-color 0.25s, color 0.25s;
}

.pill.active {
  background: var(--color-accent);
  border-color: var(--color-accent);
  color: white;
}
```

### Key Easing Values

Production sites converge on two primary easing curves:

| Easing | Value | Use |
|--------|-------|-----|
| **Primary ease-out** | `cubic-bezier(0.22, 1, 0.36, 1)` | Nav transitions, accordion, hover |
| **Standard** | `cubic-bezier(0.4, 0, 0.2, 1)` | Button and icon transitions |
| **Spring** | `cubic-bezier(0.34, 1.56, 0.64, 1)` | Playful bounces, badges |
