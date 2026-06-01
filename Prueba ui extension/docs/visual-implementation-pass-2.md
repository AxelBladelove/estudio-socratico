# Visual Implementation Pass 2

Fecha: 2026-05-31

Objetivo: segunda pasada visual enfocada solo en material premium del snake y nodos. La ilustracion ABC queda fuera de esta iteracion.

## Archivos tocados

- `src/components/LessonPath.tsx`
- `src/components/LessonNode.tsx`
- `src/lib/snake.ts`
- `src/styles.css`

## Snake material

- Se bajo el bloom global uniforme del path y se reforzo la separacion entre sombra, cuerpo, rim, highlight y especular.
- Se ajusto `SNAKE_CONFIG` para usar menos smoke/glow exterior y mas contraste pegado al trazo.
- Se recalibraron los gradients del active snake con mas profundidad tonal: verde profundo, teal oscuro, cyan, aqua y zonas casi negras.
- Se recalibro el locked snake hacia vidrio frio gris/azul, con borde cyan muy sutil y menos verde.
- Se agrego una capa `path-inner-shade` para meter sombras internas discontinuas dentro del cuerpo del snake.

## White strays

- Se agrego `snakeSpecularCut` como filtro SVG de especular estrecho.
- Se agregaron `snake-white-strays` con paths manuales cortos en curvas y conexiones principales.
- Se agregaron `snake-edge-sparks` como cortes de luz mas pequenos en bordes.
- Los white strays estan concentrados en curvas, entrada/salida de nodos y tramos locked donde la referencia conserva pequenos brillos frios.

## Gradients

- `snakeActive` ahora mezcla tonos de material en lugar de un verde plano.
- `snakeRimActive` separa el borde luminoso del cuerpo.
- `snakeLocked` tiene mas contraste metalico frio.
- `snakeInnerShade` introduce zonas oscuras internas.
- `snakeSpecularGradient` alimenta los cortes blancos/cyan discontinuos.

## Completed nodes

- Se agregaron capas internas compartidas en `LessonNode.tsx`:
  - `node-contact-glow`
  - `node-outer-ring`
  - `node-inner-shine`
- Los completed nodes tienen anillo mas definido, highlight superior/izquierdo, sombra interior inferior y glow mas concentrado.
- El check blanco mantiene presencia, con glow mas integrado y menos aspecto plano.

## Current node

- El current node recibio un ring blanco/cyan mas reconocible y con bloom controlado.
- Se reforzo el material interno: centro mas profundo, brillo superior y sombra inferior.
- El side dot ahora usa gradiente radial y borde oscuro para parecer una pieza luminosa integrada.
- El icono interior tiene mas presencia por stroke/fill y sombras de lectura.

## Locked nodes

- Los locked/challenge nodes se acercaron mas a vidrio frio: gris/azul metalico, borde superior claro, centro mas oscuro y lock icon mas presente.
- Se mantuvo un glow cyan muy sutil para que no parezcan disabled planos.

## Bloom y microflares

- Se mantuvieron microflares localizados en curvas y conexiones.
- Se redujo el exceso de mancha global y se movio la energia visual a path, nodos y conexiones.
- La intervencion no cambia geometria del path ni posiciones de nodos.

## PixiJS / WebGL

No se uso PixiJS/WebGL en esta pasada. Para este objetivo concreto, los efectos requeridos eran material, rim light, white strays y capas SVG/CSS localizadas sobre geometria existente. Meter canvas/WebGL encima habria aumentado complejidad sin mejorar la precision del snake respecto al path actual.

## Verificacion

- Build ejecutado con Bun: `bun run build`
- Resultado: correcto.
- Capturas generadas en `tools/output/visual-pass-2/`:
  - `before-side-by-side.png`
  - `after-side-by-side.png`
  - `before-ui-crop.png`
  - `after-ui-crop.png`
  - `before-after-comparison.png`
  - `before-after-diff-amplified.png`
  - `reference-crop.png`
  - `after-reference-crop.png`
  - `after-reference-vs-ui-diff-amplified.png`

## Lectura visual

La mejora principal esta en la definicion del material: menos glow uniforme y mas borde/rim/highlight interno. El diff amplificado confirma que los cambios quedaron localizados en snake y nodos, sin reabrir layout, textos, bottom nav, header o ABC.

## Pendiente para tercera pasada

- Ajustar manualmente la geometria fina de algunos white strays para que sigan aun mejor el borde externo exacto de la referencia.
- Revisar el current icon como una pasada separada si se quiere mas paridad con el icono de referencia.
- Trabajar la ilustracion ABC en una iteracion propia.
- Hacer una pasada de comparacion por ROI centrada solo en snake/nodos para medir si algun locked node quedo demasiado brillante.
