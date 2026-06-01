# Visual Audit Pass 1

Reading this as: premium dark educational game UI, with a liquid neon/glass material language, leaning toward SVG/CSS layered effects before adding WebGL.

Artifacts used:
- `tools/output/visual-audit/current-vs-reference-clean.png`
- `tools/output/visual-audit/reference-crop.png`
- `tools/output/visual-audit/ui-crop.png`
- `tools/output/visual-audit/reference-vs-ui-diff-amplified.png`
- `tools/output/visual-audit/reference-vs-ui-diff-heat.png`

Note: the requested reference path says `public/assets/reference-cadenas.png`, but this repo currently has `assets/reference-cadenas.png` and the app loads it through `/assets/reference-cadenas.png`.

## 1. Fondo y atmosfera

Esta bien:
- La base ya vive en un azul petroleo casi negro, no en negro plano.
- Hay vignetting, textura ligera y algo de codigo fantasma.
- La pantalla no cae en el look de gradiente generico.

Se aleja de la referencia:
- El centro de la UI actual tiene una columna vertical verdosa demasiado visible en la parte media-baja. La referencia tiene profundidad atmosferica, pero no una mancha central tan continua.
- El fondo actual se siente mas liso y digital; la referencia tiene microtextura mas distribuida y menos banding.

Falta:
- Ruido fino casi imperceptible con opacidad muy baja, idealmente separado de los radiales.
- Particulas puntuales mas pequenas y menos regulares, con variacion de opacidad.

Demasiado fuerte:
- `radial-gradient` central verdoso en `.background-layer` y `.app` cuando se suma con el glow del path.

Primera pasada:
- Ajuste fino en `src/styles.css`: bajar opacidad de los radiales verdes grandes, subir textura puntual muy sutil, mantener vinyeta.

## 2. Header pills

Esta bien:
- Los pills tienen glass oscuro, borde cyan tenue, blur y highlight superior.
- La jerarquia del texto y los iconos esta cerca.

Se aleja:
- El borde actual es demasiado uniforme. En la referencia el borde parece tener luz acumulada en esquinas y zonas superiores, no un contorno plano.
- El interior del pill actual tiene poco contraste entre parte superior iluminada y base profunda.

Falta:
- Segundo highlight interno de 1px en la parte superior.
- Sombra exterior mas profunda pegada al componente, no solo glow cyan.
- Micro-sheen diagonal muy leve.

Demasiado debil:
- `box-shadow` cyan del pill esta muy contenido, pero falta separacion oscura por debajo.

Primera pasada:
- Solo `src/styles.css`, clases `.pill`, `.pill::before`, iconos internos.

## 3. Titulo y subtitulo

Esta bien:
- La escala, posicion y pesos ya estan muy cerca.
- `MODULO 2` en cyan pequeno y el titulo blanco funcionan.

Se aleja:
- El texto actual esta un poco mas nitido/plano que la referencia, que tiene halo minimo y sombra oscura mas suave.
- El subtitulo actual puede verse demasiado limpio respecto al gris-cyan apagado de referencia.

Falta:
- Text shadow con dos capas: sombra oscura corta y halo cyan minimo.

Solo ajuste fino:
- `src/styles.css` en `.module-label`, `.main-title`, `.subtitle-text`.

## 4. Progress card

Esta bien:
- La tarjeta ya tiene glass oscuro, borde fino, blur y profundidad.
- Los numeros cyan/verde ya leen como metricas principales.

Se aleja:
- La tarjeta actual se siente mas rectangular y mas pesada en el borde inferior.
- En la referencia el volumen interno esta mas distribuido: parte superior con brillo, centro profundo, borde inferior con sombra suave.

Falta:
- Borde interior secundario casi invisible.
- Sombra debajo mas ancha pero menos opaca.
- Separacion visual entre texto y barra por material, no por layout.

Demasiado fuerte:
- El background interno del card actual puede levantar demasiado el centro con verde/cyan.

Primera pasada:
- `src/styles.css` en `.progress-card`, `.progress-card::before`.

## 5. Barras de progreso

Esta bien:
- Las barras activas ya tienen forma de capsula, gradiente verde-cyan y glow.
- Las inactivas no son planas del todo.

Se aleja:
- El tramo activo parece una fila de segmentos CSS muy regular. En referencia la luz activa se siente mas fisica: borde brillante, centro saturado y glow leve por segmento.
- Las inactivas actuales son algo claras y uniformes.

Falta:
- Highlight superior de 1px por segmento.
- Sombra interna inferior mas marcada.
- Glow activo mas fino, no mas grande.

Demasiado debil:
- El contraste entre segmento activo y borde interior.

Solo ajuste fino:
- `src/styles.css` en `.progress-bars span`, `.progress-bars .done`, `.progress-bars .partial`.

## 6. Snake path

Esta bien:
- Ya esta dividido en completed, active y locked.
- Usa varias capas SVG: sombra, depth, smoke, glow, body, edge, core light, sparkle, highlight.
- `stroke-linecap` y `stroke-linejoin` redondeados estan correctos.
- La geometria general ya conecta los nodos sin grandes cortes.

Se aleja:
- En la UI actual el snake se ve mas gris/teal y menos liquido que la referencia en el tramo activo.
- El cuerpo del snake tiene poca lectura de borde brillante contra centro oscuro. En referencia hay una cinta con centro material, borde iluminado y pequenos reflejos blancos.
- Los highlights discontinuos existen, pero visualmente no aparecen con suficiente intencion: no parecen reflejos especulares localizados.
- En el tramo locked la cinta actual se vuelve demasiado opaca/gris uniforme en algunos puntos y pierde el cyan frio del borde.

Falta:
- Una capa de borde cyan fino separada del cuerpo principal, con stroke mas estrecho y opacidad controlada.
- Una capa de sombra inferior oscura mas pegada al path, menos ancha que el glow.
- Mas contraste entre `path-body`, `path-edge` y `path-highlight`.
- Dasharrays por segmento ajustados manualmente para que los brillos aparezcan en curvas clave, especialmente entrada/salida del nodo current.

Demasiado fuerte:
- `path-smoke` y `node-liquid-blooms` suman niebla amplia pero no aportan suficiente definicion.

Mejora profunda:
- `src/components/LessonPath.tsx` y `src/styles.css`.
- Mantener path, cambiar material/layers.

## 7. Smoke, bloom y glow del path

Esta bien:
- Ya hay filtros con `feTurbulence`, `feDisplacementMap` y blur organico.
- El glow no es una sola linea simple.

Se aleja:
- La referencia no tiene humo uniforme por todo el path; el bloom se concentra en nodos, curvas y transiciones.
- El glow actual es demasiado homogeneo en el snake superior y demasiado apagado en zonas donde la referencia acumula luz.

Falta:
- Bloom localizado alrededor de conexiones nodo-path.
- Halos pequenos en puntos de alta energia, no una nube continua.
- Variacion de opacidad por segmento.

Demasiado debil:
- Los `snake-flares` estan `display: none`; podrian ser utiles si se convierten en micro-reflejos controlados.

Demasiado fuerte:
- Los blooms circulares grandes si se suben mas se convierten rapidamente en manchas. Mantenerlos sutiles.

Mejora profunda:
- `LessonPath.tsx`: usar grupos de flare/bloom por nodo y segmentos con clases especificas.
- `styles.css`: bajar humo global y subir reflejos locales.

## 8. Nodos completados

Esta bien:
- Tienen gradientes complejos, conic gradients, borde claro, sombra interna y check blanco.
- No son botones planos.

Se aleja:
- El borde de referencia se siente mas como un anillo energetico limpio. El actual tiene glow exterior con textura pero el aro no siempre queda definido.
- El centro actual es algo mas saturado/verde uniforme; la referencia tiene mas volumen radial y menos ruido visible.

Falta:
- Anillo interior fino separado del border CSS.
- Highlight especular localizado arriba-izquierda y sombra inferior interna mas oscura.
- Acumulacion de luz donde el snake entra/sale.

Demasiado fuerte:
- `::before` y `::after` de `.lesson-node` pueden crear halo organico generico alrededor de todos los nodos.

Primera pasada:
- `src/styles.css` en `.lesson-node.completed`, `.lesson-node::before`, `.lesson-node::after`.

## 9. Nodo actual

Esta bien:
- Es mas grande, tiene side dot, aro, bloom y CTA.
- La jerarquia como punto focal existe.

Se aleja:
- En la referencia el aro exterior es mucho mas luminoso y limpio. En la UI actual el aro se pierde contra el material del nodo.
- El interior del nodo actual esta demasiado oscuro/vidrioso y el icono queda pequeno, con menos presencia que la referencia.
- El side dot se ve correcto en posicion pero podria tener mas luz contenida y menos borde duro.

Falta:
- Aro exterior con dos capas: ring blanco-cyan fino y bloom externo suave.
- Glow puntual en el punto donde el snake toca el nodo.
- Gradiente radial interno con centro mas profundo y borde mas iluminado.

Demasiado debil:
- `box-shadow` del ring actual: hay glow, pero no suficiente linea blanca/cyan definida.

Mejora profunda:
- `src/styles.css` para `.lesson-node.current`, `.lesson-node.current::after`, `.current-side-dot`.
- Posible ajuste menor en `LessonNode.tsx` si hace falta una capa extra de ring dedicada, pero no mover layout.

## 10. Nodos bloqueados

Esta bien:
- Tienen material gris/azul, conic gradient y lock claro.
- No se ven totalmente muertos.

Se aleja:
- La referencia tiene locked nodes mas metalicos/vidrio frio, con borde superior claro y centro mas profundo. Los actuales tienen buen volumen, pero el borde puede verse lechoso y algo uniforme.
- El lock icon en algunos nodos se ve demasiado tenue respecto a la referencia.

Falta:
- Borde frio mas definido arriba-izquierda.
- Sombra interior inferior con mas contraste.
- Glow cyan muy fino en la zona donde el path pasa detras.

Demasiado debil:
- El cyan frio del locked path/nodo casi desaparece en zonas bajas.

Solo ajuste fino:
- `src/styles.css` en `.lesson-node.locked`, `.lesson-node.challenge`, iconos locked.

## 11. Labels y CTA

Esta bien:
- Labels estan alineados y no invaden demasiado el path.
- El CTA existe como capsula.

Se aleja:
- El CTA `SIGUE AQUI` actual se siente mas como un pequeno boton CSS que como capsula premium.
- Algunos labels se ven mas blancos/duros que la referencia, especialmente en la zona baja.

Falta:
- CTA con borde interior, glow fino y fondo mas profundo.
- Text shadow mas corto en labels para que no parezcan flotando sobre una sombra grande.

Demasiado debil:
- Halo del CTA y separacion material del fondo.

Solo ajuste fino:
- `src/styles.css` en `.lesson-label`, `.lesson-label button`, `.cta-text`.

## 12. Ilustracion ABC

Esta bien:
- La decoracion esta en el lugar correcto y usa cyan/glow.
- El trazo punteado y las elipses ayudan a leerla como ambientacion.

Se aleja:
- En la UI actual la ilustracion compite demasiado: las comillas blancas son grandes y con mucho peso visual.
- El globo actual tiene un borde/glow mas intenso y limpio que la referencia; parece un elemento principal, no decorativo.
- El texto `abc` y las comillas se sienten mas opacos y grandes.

Falta:
- Transparencia mas baja en el interior.
- Stroke cyan mas fino y glow mas controlado.
- Menos fill blanco en comillas; deberian ser mas livianas o mas translúcidas.

Demasiado fuerte:
- `.abc-art .quote`, `.abc-art text`, `filter: url("#abcGlow")`, y `mix-blend-mode: screen` juntos.

Mejora profunda moderada:
- `src/styles.css` para `.abc-art`.
- `src/components/Stage.tsx` solo si se decide separar comillas/texto en capas con opacidad distinta.

## 13. Codigo fantasma

Esta bien:
- Es sutil y no roba lectura.
- Funciona como textura pedagogica.

Se aleja:
- En la referencia el codigo se siente un poco mas integrado al fondo; en UI actual algunas lineas aparecen como bloque vertical demasiado rectangular.

Falta:
- Ligera mascara/gradiente de fade por linea o por bloque.
- Menos regularidad en opacidad entre lineas.

Solo ajuste fino:
- `src/styles.css` en `.code-art`.

## 14. Bottom nav

Esta bien:
- La barra esta alineada y tiene glass oscuro, borde y CTA circular.
- El boton derecho tiene buen glow cyan.

Se aleja:
- La referencia tiene un bottom nav mas contenido y atmosferico; la UI actual se ve mas pesada y mas rectangular.
- El texto `Siguiente:` en la UI actual aparece demasiado brillante/blanco en el diff y compite con el boton.
- El CTA derecho tiene buen brillo, pero su borde se siente mas boton generico que luz contenida.

Falta:
- Highlight superior mas fino y sombra inferior mas difusa.
- Boton de continuar con ring interno y centro mas oscuro.
- Menos glow en la barra completa, mas glow en el CTA.

Demasiado fuerte:
- Peso visual de `.bottom-nav` y del texto interior.

Solo ajuste fino:
- `src/styles.css` en `.bottom-nav`, `.bottom-nav button`, `.bottom-next-text`, `.bottom-lesson-text`.

## 15. Particulas y microdetalles

Esta bien:
- Hay particulas en background y sparkles en ABC.
- El path tiene dash highlights y filtros organicos.

Se aleja:
- Las particulas actuales son pocas y muy regulares.
- La referencia tiene microdetalles concentrados alrededor de energia: current node, ABC, path bright turns.

Falta:
- 6-10 puntos de luz muy pequenos, colocados manualmente alrededor de current node, ABC y path superior.
- Variacion de blur: algunas particulas nitidas, otras muy suaves.

Demasiado debil:
- `snake-flares` apagado.

Primera pasada:
- Activar microflares controlados en SVG o pseudo-elementos, no llenar el fondo.

## 16. Performance visual

Riesgos actuales:
- `LessonPath.tsx` usa varios filtros SVG con `feTurbulence`, `feDisplacementMap`, `feGaussianBlur`, `drop-shadow` y muchas capas sobre paths largos. Esto puede ser caro en GPUs debiles.
- `.lesson-node::before` y `::after` con blur en todos los nodos suman repaints si se animan en el futuro.
- `mix-blend-mode: screen` en path, nodes y ABC puede encarecer compositing.
- `backdrop-filter` en pills, progress card y bottom nav esta bien para esta pantalla, pero debe mantenerse acotado.

Esta bien:
- No hay animaciones continuas pesadas activas.
- La app esta en Vite/React simple, sin estado global innecesario para render visual.

Recomendacion:
- No introducir PixiJS/WebGL en la primera reparacion. Con SVG/CSS se puede acercar mucho mas al material de referencia.
- Si luego se necesita smoke/particles dinamicos, PixiJS deberia limitarse a una capa canvas ambiental detras de UI, no reemplazar los nodos ni el snake SVG.

## Efectos faltantes

- Ring energetico real en nodo actual: borde blanco-cyan fino mas bloom controlado.
- Reflejos especulares discontinuos mejor posicionados en snake.
- Acumulacion de luz en entradas/salidas de nodos.
- Borde cyan fino separado del cuerpo del snake.
- Microtextura/noise menos regular en fondo y halos.
- Glass con doble borde/inner stroke en pills, progress card y bottom nav.

## Efectos demasiado debiles

- Highlight del snake en curvas clave.
- Aro exterior del nodo current.
- Separacion oscura debajo del snake.
- Glow frio de locked nodes/path.
- Material del CTA `SIGUE AQUI`.

## Efectos demasiado fuertes

- Ilustracion ABC: comillas y `abc` demasiado presentes.
- Mancha vertical verdosa del fondo cuando se suma al glow del path.
- Halos genericos de nodos si se incrementan mas.
- Peso visual del bottom nav.

## Ajuste fino vs mejora profunda

Ajuste fino:
- Fondo.
- Header pills.
- Titulo/subtitulo.
- Progress card.
- Barras de progreso.
- Labels/CTA.
- Locked nodes.
- Codigo fantasma.
- Bottom nav.

Mejora profunda:
- Snake material completo.
- Smoke/bloom localizado del path.
- Nodo actual.
- Completed nodes si se busca paridad mas alta.
- ABC si se quiere dejarla realmente decorativa.

## Archivos que tocaria en primera pasada

- `src/styles.css`: tokens, background, glass panels, progress bars, node material, bottom nav, ABC, labels.
- `src/components/LessonPath.tsx`: reorganizar capas de path y flares locales sin tocar geometria.
- `src/lib/snake.ts`: ajustar constantes visuales de `SNAKE_CONFIG`; no path geometry.
- `src/components/LessonNode.tsx`: solo si hace falta insertar capas extra para ring/connector highlights.
- `src/components/Stage.tsx`: solo si la decoracion ABC necesita separar subcapas con clases mas finas.

No tocaria en primera pasada:
- `src/lib/layout.ts`.
- Textos.
- Posiciones.
- Header layout.
- Bottom nav layout.
- Geometria del path.

## Orden recomendado de implementacion

1. Congelar comparacion: mantener `Side by side` limpio y generar captura antes/despues.
2. Bajar ruido visual grande: reducir mancha verde del fondo y glow global del snake.
3. Reconstruir snake material: sombra pegada, cuerpo definido, borde cyan fino, highlights discontinuos mas visibles.
4. Reparar nodo current: ring exterior, bloom controlado, side dot y profundidad interna.
5. Reparar completed nodes: anillo definido, specular highlight, entrada/salida del snake.
6. Ajustar locked path/nodes: gris azul metalico con cyan frio minimo.
7. Bajar ABC: comillas/texto menos opacos, stroke mas fino, glow menos competitivo.
8. Ajustar glass panels: pills, progress card y bottom nav con doble borde y sombra mas fisica.
9. Ajustar progress bars y CTA.
10. Revalidar con crop side-by-side y diff amplificado.

## Top 5 mejoras primero

1. Redefinir el snake con capas mas finas: sombra pegada, cuerpo teal oscuro, borde cyan, highlight discontinuo y bloom controlado.
2. Hacer el nodo current mas parecido a la referencia: ring blanco-cyan definido, halo contenido y centro con volumen.
3. Bajar la ilustracion ABC para que vuelva a ser decorativa, no un segundo foco.
4. Reducir manchas verdes globales y mover la energia hacia nodos/conexiones.
5. Dar a completed/locked nodes un material mas fisico: anillo definido, sombra interior, especular localizado y glow frio/saturado segun estado.
