# Scroll-Driven Animations API

Native CSS scroll-linked animations without JavaScript.

## Table of Contents

1. [Timeline Types](#timeline-types)
2. [Document Scroll Progress](#document-scroll-progress)
3. [Element View Progress](#element-view-progress)
4. [Animation Range](#animation-range)
5. [Polyfill Setup](#polyfill-setup)
6. [Common Patterns](#common-patterns)

---

## Timeline Types

### scroll() - Document/Container Scroll Progress

Tied to the scroll position of a scrollable container.

```css
.element {
  animation: reveal linear;
  animation-timeline: scroll();  /* Default: nearest scrollable ancestor */
}
```

### view() - Element Visibility Progress

Tied to element's position within its scroll container viewport.

```css
.element {
  animation: fadeIn linear;
  animation-timeline: view();
}
```

---

## Document Scroll Progress

### Progress Bar (Full Page)

```css
.progress-bar {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  height: 3px;
  background: var(--color-accent);
  transform-origin: left;
  transform: scaleX(0);
  animation: progress linear;
  animation-timeline: scroll();
}

@keyframes progress {
  to { transform: scaleX(1); }
}
```

### Parallax Background

```css
.hero-bg {
  animation: parallax linear;
  animation-timeline: scroll();
}

@keyframes parallax {
  from { transform: translateY(0); }
  to { transform: translateY(-20%); }
}
```

### Scroll-Based Opacity

```css
.hero-content {
  animation: fadeOut linear;
  animation-timeline: scroll();
  animation-range: 0% 50%;  /* Fade out in first half of scroll */
}

@keyframes fadeOut {
  from { opacity: 1; }
  to { opacity: 0; }
}
```

### Specific Scroll Container

```css
.container {
  overflow-y: auto;
  scroll-timeline-name: --container-scroll;
}

.progress-in-container {
  animation: progress linear;
  animation-timeline: --container-scroll;
}
```

---

## Element View Progress

### Reveal on Enter

```css
.reveal {
  animation: fadeSlideIn linear both;
  animation-timeline: view();
  animation-range: entry 0% cover 30%;
}

@keyframes fadeSlideIn {
  from {
    opacity: 0;
    transform: translateY(50px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}
```

### Scale on Scroll

```css
.scale-element {
  animation: scaleUp linear both;
  animation-timeline: view();
  animation-range: entry 0% cover 50%;
}

@keyframes scaleUp {
  from { transform: scale(0.8); }
  to { transform: scale(1); }
}
```

### Image Reveal

```css
.image-reveal {
  animation: clipReveal linear both;
  animation-timeline: view();
  animation-range: entry 0% cover 40%;
}

@keyframes clipReveal {
  from { clip-path: inset(0 100% 0 0); }
  to { clip-path: inset(0 0 0 0); }
}
```

### View Timeline Axis

```css
.element {
  animation-timeline: view(block);  /* Vertical axis (default) */
  /* animation-timeline: view(inline); */  /* Horizontal axis */
  /* animation-timeline: view(x); */
  /* animation-timeline: view(y); */
}
```

---

## Animation Range

Controls when within the timeline the animation runs.

### Range Keywords

- `entry` - Element entering viewport (0% = just appeared, 100% = fully in)
- `exit` - Element exiting viewport
- `cover` - From entry start to exit end
- `contain` - When element is fully visible

### Syntax

```css
/* Start and end positions */
animation-range: entry 0% cover 50%;
/* Animation starts when element enters, ends when 50% covered */

/* Single value = start, end is timeline end */
animation-range: entry 20%;

/* Percentage of viewport */
animation-range: cover 20% cover 80%;
```

### Common Ranges

```css
/* Reveal as entering */
animation-range: entry 0% cover 30%;

/* Animate while fully visible */
animation-range: contain 0% contain 100%;

/* Animate through entire journey */
animation-range: cover 0% cover 100%;

/* Animate only on exit */
animation-range: exit 0% exit 100%;
```

### Visual Guide

```
                   ┌─────────────────────┐
                   │      VIEWPORT       │
                   │                     │
Entry 0%  ────────▶│─────────────────────│
                   │                     │
Entry 100% ───────▶│     ┌─────────┐     │◀── Cover 0% / Contain 0%
                   │     │ ELEMENT │     │
                   │     └─────────┘     │◀── Contain 100%
                   │                     │
Exit 0%   ────────▶│─────────────────────│
                   │                     │
Exit 100% ────────▶│                     │◀── Cover 100%
                   └─────────────────────┘
```

---

## Polyfill Setup

For Safari and older browsers:

```html
<script src="https://flackr.github.io/scroll-timeline/dist/scroll-timeline.js"></script>
```

### Feature Detection

```css
@supports (animation-timeline: scroll()) {
  .animated {
    animation-timeline: scroll();
  }
}

@supports not (animation-timeline: scroll()) {
  .animated {
    /* Fallback: static state */
    opacity: 1;
    transform: none;
  }
}
```

### JavaScript Feature Detection

```javascript
if ('ScrollTimeline' in window) {
  // Native support
} else {
  // Load polyfill or use GSAP
}
```

---

## Common Patterns

### Section Reveal

```css
.section {
  animation: revealSection linear both;
  animation-timeline: view();
  animation-range: entry 0% cover 30%;
}

@keyframes revealSection {
  from {
    opacity: 0;
    transform: translateY(40px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}
```

### Staggered Items (CSS-only approximation)

```css
.item {
  animation: fadeIn linear both;
  animation-timeline: view();
  animation-range: entry 0% cover 40%;
}

/* Stagger effect via transform origin */
.item:nth-child(1) { animation-range: entry 0% cover 35%; }
.item:nth-child(2) { animation-range: entry 5% cover 40%; }
.item:nth-child(3) { animation-range: entry 10% cover 45%; }
```

### Progress Through Section

```css
.section-progress {
  position: sticky;
  top: 0;
  transform-origin: left;
  transform: scaleX(0);
  animation: sectionProgress linear;
  animation-timeline: view();
  animation-range: contain 0% contain 100%;
}

@keyframes sectionProgress {
  to { transform: scaleX(1); }
}
```

### Text Highlight on Scroll

```css
.highlight-text {
  background: linear-gradient(
    to right,
    var(--color-accent) 50%,
    transparent 50%
  );
  background-size: 200% 100%;
  background-position: 100% 0;
  -webkit-background-clip: text;
  background-clip: text;
  color: transparent;
  animation: highlight linear both;
  animation-timeline: view();
  animation-range: cover 20% cover 60%;
}

@keyframes highlight {
  to { background-position: 0 0; }
}
```

### Horizontal Scroll Indicator

```css
.horizontal-container {
  overflow-x: auto;
  scroll-timeline-name: --horizontal-scroll;
  scroll-timeline-axis: x;
}

.scroll-indicator {
  animation: scrollProgress linear;
  animation-timeline: --horizontal-scroll;
}

@keyframes scrollProgress {
  from { transform: scaleX(0); }
  to { transform: scaleX(1); }
}
```

---

## Reduced Motion

**Always wrap in preference check:**

```css
/* Default: No animation, final state */
.animated {
  opacity: 1;
  transform: none;
}

@media (prefers-reduced-motion: no-preference) {
  .animated {
    opacity: 0;
    transform: translateY(30px);
    animation: reveal linear both;
    animation-timeline: view();
    animation-range: entry 0% cover 30%;
  }
}

@keyframes reveal {
  to {
    opacity: 1;
    transform: translateY(0);
  }
}
```

---

## Limitations vs GSAP

| Feature | CSS Scroll-Driven | GSAP ScrollTrigger |
|---------|-------------------|-------------------|
| Pin sections | ✗ | ✓ |
| Snap points | Limited | ✓ Full control |
| Horizontal scroll | Limited | ✓ |
| Multiple triggers | Per-element | Flexible |
| Safari support | Polyfill | Native |
| Callbacks | ✗ | ✓ |
| Fine control | Limited | Full |

**Use CSS for**: Simple reveals, progress indicators, parallax
**Use GSAP for**: Pinning, complex sequences, callbacks, cross-browser

---

## Production CSS Scroll-Driven Patterns

### Scroll Progress Bar

A page-level reading progress indicator using `animation-timeline: scroll()`:

```css
.scroll-progress {
  position: fixed;
  top: 0;
  left: 0;
  width: 100%;
  height: 2px;
  background: var(--color-accent);
  transform-origin: left;
  z-index: 50;
  animation: scroll-progress linear;
  animation-timeline: scroll();
}

@keyframes scroll-progress {
  from { transform: scaleX(0); }
  to { transform: scaleX(1); }
}
```

Uses `scaleX` (GPU-accelerated) rather than `width` (triggers layout). The `scroll()` timeline maps 0-100% scroll to 0-100% animation progress.

### Reveal on Scroll with View Timeline

Elements fade up as they enter the viewport — entirely CSS, no JavaScript:

```css
@media (prefers-reduced-motion: no-preference) {
  .reveal-on-scroll {
    animation: fade-up 0.6s both;
    animation-timeline: view();
    animation-range: entry 0% cover 30%;
  }

  @keyframes fade-up {
    from {
      opacity: 0;
      transform: translateY(20px);
    }
    to {
      opacity: 1;
      transform: translateY(0);
    }
  }
}
```

The `animation-range: entry 0% cover 30%` means the animation plays from when the element starts entering the viewport to when 30% of it is covered — a natural reveal timing.
