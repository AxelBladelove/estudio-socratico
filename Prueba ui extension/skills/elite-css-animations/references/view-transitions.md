# View Transitions API

Smooth page and state transitions with CSS control.

## Table of Contents

1. [Basic Usage](#basic-usage)
2. [Customizing Transitions](#customizing-transitions)
3. [Named View Transitions](#named-view-transitions)
4. [SPA Integration](#spa-integration)
5. [MPA (Cross-Document)](#mpa-cross-document)

---

## Basic Usage

### Simple Transition

```javascript
// Wrap DOM update in startViewTransition
document.startViewTransition(() => {
  // Update the DOM safely
  updateContent(container, newData);
});
```

### With Async Content

```javascript
document.startViewTransition(async () => {
  const data = await fetchData();
  updateUI(data);
});
```

### Checking Support

```javascript
if (document.startViewTransition) {
  document.startViewTransition(() => updateDOM());
} else {
  updateDOM();  // Fallback: instant update
}
```

---

## Customizing Transitions

### Default Pseudo-Elements

View transitions create these pseudo-elements:

```
::view-transition
└── ::view-transition-group(root)
    └── ::view-transition-image-pair(root)
        ├── ::view-transition-old(root)  /* Screenshot of old state */
        └── ::view-transition-new(root)  /* Live new state */
```

### Custom Fade

```css
::view-transition-old(root) {
  animation: fade-out 0.3s ease-out forwards;
}

::view-transition-new(root) {
  animation: fade-in 0.3s ease-in forwards;
}

@keyframes fade-out {
  from { opacity: 1; }
  to { opacity: 0; }
}

@keyframes fade-in {
  from { opacity: 0; }
  to { opacity: 1; }
}
```

### Slide Transition

```css
::view-transition-old(root) {
  animation: slide-out 0.4s ease-in-out;
}

::view-transition-new(root) {
  animation: slide-in 0.4s ease-in-out;
}

@keyframes slide-out {
  to { transform: translateX(-100%); }
}

@keyframes slide-in {
  from { transform: translateX(100%); }
}
```

### Scale Transition

```css
::view-transition-old(root) {
  animation: scale-down 0.3s ease-in forwards;
}

::view-transition-new(root) {
  animation: scale-up 0.3s ease-out forwards;
}

@keyframes scale-down {
  to {
    opacity: 0;
    transform: scale(0.9);
  }
}

@keyframes scale-up {
  from {
    opacity: 0;
    transform: scale(1.1);
  }
}
```

---

## Named View Transitions

Transition specific elements independently.

### Marking Elements

```css
/* In old page */
.hero-image {
  view-transition-name: hero;
}

/* In new page - same name */
.product-image {
  view-transition-name: hero;
}
```

### Styling Named Transitions

```css
/* Transition just the hero */
::view-transition-old(hero) {
  animation: fade-scale-out 0.4s ease-out;
}

::view-transition-new(hero) {
  animation: fade-scale-in 0.4s ease-out;
}

/* Different timing for different elements */
::view-transition-group(hero) {
  animation-duration: 0.5s;
}

::view-transition-group(root) {
  animation-duration: 0.3s;
}
```

### Example: Card to Full Page

```html
<!-- List page -->
<article class="card" style="view-transition-name: card-1;">
  <img src="product.jpg" style="view-transition-name: card-1-image;">
  <h2 style="view-transition-name: card-1-title;">Product Name</h2>
</article>

<!-- Detail page -->
<article class="product-detail">
  <img src="product.jpg" style="view-transition-name: card-1-image;">
  <h1 style="view-transition-name: card-1-title;">Product Name</h1>
</article>
```

```css
::view-transition-group(card-1-image) {
  animation-duration: 0.4s;
  animation-timing-function: cubic-bezier(0.4, 0, 0.2, 1);
}
```

---

## SPA Integration

### React Router Example

```jsx
import { useNavigate, useLocation } from 'react-router-dom';

function useViewTransitionNavigate() {
  const navigate = useNavigate();

  return (to) => {
    if (document.startViewTransition) {
      document.startViewTransition(() => {
        navigate(to);
      });
    } else {
      navigate(to);
    }
  };
}

// Usage
function Card({ product }) {
  const navigateWithTransition = useViewTransitionNavigate();

  return (
    <article
      onClick={() => navigateWithTransition(`/product/${product.id}`)}
      style={{ viewTransitionName: `product-${product.id}` }}
    >
      {/* content */}
    </article>
  );
}
```

### Vue Router Example

```javascript
// router/index.js
router.beforeResolve(async (to, from) => {
  if (document.startViewTransition) {
    await document.startViewTransition(async () => {
      // Route change happens here
    }).finished;
  }
});
```

### Vanilla SPA

```javascript
async function navigateTo(url) {
  const response = await fetch(url);
  const html = await response.text();
  const parser = new DOMParser();
  const doc = parser.parseFromString(html, 'text/html');
  const newContent = doc.querySelector('main');

  if (document.startViewTransition) {
    await document.startViewTransition(() => {
      document.querySelector('main').replaceWith(newContent);
      history.pushState({}, '', url);
    }).finished;
  } else {
    document.querySelector('main').replaceWith(newContent);
    history.pushState({}, '', url);
  }
}

// Handle links
document.addEventListener('click', (e) => {
  if (e.target.matches('a[data-spa]')) {
    e.preventDefault();
    navigateTo(e.target.href);
  }
});
```

---

## MPA (Cross-Document)

For traditional multi-page apps (2026+).

### Enable in CSS

```css
@view-transition {
  navigation: auto;
}
```

### Named Elements Persist

```html
<!-- page1.html -->
<header style="view-transition-name: header;">
  <nav>...</nav>
</header>

<!-- page2.html -->
<header style="view-transition-name: header;">
  <nav>...</nav>
</header>
```

The header transitions smoothly between pages.

### Same-Origin Only

Cross-document transitions only work for:
- Same origin
- HTTPS
- Pages that opt-in via CSS

---

## Transition Types

### Directional Transitions

```javascript
// Track navigation direction
let isBack = false;

window.addEventListener('popstate', () => {
  isBack = true;
});

document.startViewTransition(() => {
  updatePage();
}).finished.then(() => {
  isBack = false;
});
```

```css
/* Forward navigation */
::view-transition-old(root) {
  animation: slide-to-left 0.3s ease;
}
::view-transition-new(root) {
  animation: slide-from-right 0.3s ease;
}

/* Reverse for back navigation */
:root.back-nav::view-transition-old(root) {
  animation: slide-to-right 0.3s ease;
}
:root.back-nav::view-transition-new(root) {
  animation: slide-from-left 0.3s ease;
}
```

---

## Reduced Motion

```css
@media (prefers-reduced-motion: reduce) {
  ::view-transition-old(root),
  ::view-transition-new(root) {
    animation: none;
  }

  /* Or shorter duration */
  ::view-transition-group(*) {
    animation-duration: 0.01s;
  }
}
```

---

## Browser Support

| Browser | Same-Document | Cross-Document |
|---------|--------------|----------------|
| Chrome | ✓ 111+ | ✓ 126+ |
| Safari | ✓ 18+ | ✓ 18+ |
| Firefox | ✗ | ✗ |
| Edge | ✓ 111+ | ✓ 126+ |

### Polyfill

No full polyfill exists. Use feature detection:

```javascript
if (!document.startViewTransition) {
  // Use GSAP/Barba.js for transitions
}
```
