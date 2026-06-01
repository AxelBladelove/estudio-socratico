# @property Rule

Animate CSS custom properties that previously couldn't be animated.

## Table of Contents

1. [Basic Syntax](#basic-syntax)
2. [Gradient Animations](#gradient-animations)
3. [Progress Animations](#progress-animations)
4. [Creative Uses](#creative-uses)

---

## Basic Syntax

```css
@property --property-name {
  syntax: '<type>';      /* Required: What type of value */
  initial-value: value;  /* Required: Starting value */
  inherits: true|false;  /* Required: Does it inherit? */
}
```

### Supported Syntax Types

```css
'<length>'          /* 10px, 2rem, 50vh */
'<percentage>'      /* 50% */
'<length-percentage>' /* 10px or 50% */
'<number>'          /* 0.5, 100 */
'<integer>'         /* 1, 2, 3 */
'<angle>'           /* 45deg, 0.5turn */
'<color>'           /* #fff, rgb(), hsl() */
'<image>'           /* url(), gradient */
'<time>'            /* 1s, 500ms */
'<transform-function>' /* rotate(), scale() */
```

---

## Gradient Animations

### Rotating Gradient

```css
@property --gradient-angle {
  syntax: '<angle>';
  initial-value: 0deg;
  inherits: false;
}

.gradient-bg {
  --gradient-angle: 0deg;
  background: linear-gradient(
    var(--gradient-angle),
    #667eea,
    #764ba2
  );
  animation: rotate-gradient 3s linear infinite;
}

@keyframes rotate-gradient {
  to { --gradient-angle: 360deg; }
}
```

### Conic Gradient Progress

```css
@property --progress {
  syntax: '<number>';
  initial-value: 0;
  inherits: false;
}

.progress-ring {
  --progress: 0;
  background: conic-gradient(
    var(--color-accent) calc(var(--progress) * 360deg),
    var(--color-bg-secondary) 0
  );
  border-radius: 50%;
  transition: --progress 1s ease-out;
}

.progress-ring.complete {
  --progress: 1;
}

/* Or via JavaScript */
/* element.style.setProperty('--progress', '0.75'); */
```

### Color Shift

```css
@property --color-1 {
  syntax: '<color>';
  initial-value: #667eea;
  inherits: false;
}

@property --color-2 {
  syntax: '<color>';
  initial-value: #764ba2;
  inherits: false;
}

.color-shift {
  background: linear-gradient(135deg, var(--color-1), var(--color-2));
  animation: shift-colors 5s ease infinite alternate;
}

@keyframes shift-colors {
  to {
    --color-1: #f093fb;
    --color-2: #f5576c;
  }
}
```

### Animated Gradient Border

```css
@property --border-angle {
  syntax: '<angle>';
  initial-value: 0deg;
  inherits: false;
}

.gradient-border {
  --border-angle: 0deg;
  position: relative;
  background: var(--color-bg-primary);
  border-radius: 16px;
}

.gradient-border::before {
  content: '';
  position: absolute;
  inset: -2px;
  border-radius: inherit;
  background: conic-gradient(
    from var(--border-angle),
    #667eea,
    #764ba2,
    #f093fb,
    #667eea
  );
  z-index: -1;
  animation: rotate-border 3s linear infinite;
}

@keyframes rotate-border {
  to { --border-angle: 360deg; }
}
```

---

## Progress Animations

### Numeric Counter

```css
@property --num {
  syntax: '<integer>';
  initial-value: 0;
  inherits: false;
}

.counter {
  --num: 0;
  counter-reset: num var(--num);
  animation: count 2s ease-out forwards;
}

.counter::after {
  content: counter(num);
}

@keyframes count {
  to { --num: 100; }
}
```

### Percentage Display

```css
@property --percent {
  syntax: '<integer>';
  initial-value: 0;
  inherits: false;
}

.percentage {
  --percent: 0;
  counter-reset: percent var(--percent);
  transition: --percent 1s ease-out;
}

.percentage::after {
  content: counter(percent) '%';
}

/* Set via JS or :hover */
.percentage.loaded {
  --percent: 87;
}
```

### Progress Bar Fill

```css
@property --fill {
  syntax: '<percentage>';
  initial-value: 0%;
  inherits: false;
}

.progress-bar {
  --fill: 0%;
  background: linear-gradient(
    to right,
    var(--color-accent) var(--fill),
    var(--color-bg-secondary) var(--fill)
  );
  transition: --fill 0.5s ease-out;
}

.progress-bar[data-progress="75"] {
  --fill: 75%;
}
```

---

## Creative Uses

### Morphing Blob

```css
@property --blob-1 {
  syntax: '<percentage>';
  initial-value: 60%;
  inherits: false;
}

@property --blob-2 {
  syntax: '<percentage>';
  initial-value: 40%;
  inherits: false;
}

.blob {
  border-radius: var(--blob-1) var(--blob-2) var(--blob-1) var(--blob-2);
  animation: morph 8s ease-in-out infinite;
}

@keyframes morph {
  50% {
    --blob-1: 40%;
    --blob-2: 60%;
  }
}
```

### Typewriter Cursor

```css
@property --cursor-opacity {
  syntax: '<number>';
  initial-value: 1;
  inherits: false;
}

.typewriter::after {
  content: '|';
  opacity: var(--cursor-opacity);
  animation: blink 1s step-end infinite;
}

@keyframes blink {
  50% { --cursor-opacity: 0; }
}
```

### Animated Shadow

```css
@property --shadow-spread {
  syntax: '<length>';
  initial-value: 0px;
  inherits: false;
}

.pulsing-glow {
  --shadow-spread: 0px;
  box-shadow: 0 0 var(--shadow-spread) var(--color-accent);
  animation: pulse-glow 2s ease-in-out infinite;
}

@keyframes pulse-glow {
  50% { --shadow-spread: 20px; }
}
```

### Wave Text

```css
@property --wave-offset {
  syntax: '<length>';
  initial-value: 0px;
  inherits: false;
}

.wave-text span {
  display: inline-block;
  animation: wave 1s ease-in-out infinite;
  animation-delay: calc(var(--i) * 0.1s);
}

@keyframes wave {
  50% { --wave-offset: -10px; transform: translateY(var(--wave-offset)); }
}
```

---

## Reduced Motion

```css
@media (prefers-reduced-motion: reduce) {
  .animated-property {
    animation: none;
    transition: none;
    /* Set to final state */
    --gradient-angle: 0deg;
    --progress: 1;
  }
}
```

---

## Browser Support

- Chrome 85+
- Firefox 128+
- Safari 15.4+
- Edge 85+

### Fallback

```css
/* Provide fallback for older browsers */
.gradient-bg {
  /* Fallback: static gradient */
  background: linear-gradient(45deg, #667eea, #764ba2);
}

@supports (animation-timeline: scroll()) {
  @property --gradient-angle {
    syntax: '<angle>';
    initial-value: 0deg;
    inherits: false;
  }

  .gradient-bg {
    /* Enhanced: animated gradient */
    background: linear-gradient(var(--gradient-angle), #667eea, #764ba2);
    animation: rotate-gradient 3s linear infinite;
  }
}
```
