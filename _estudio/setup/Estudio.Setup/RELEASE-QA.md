# Release Candidate QA 2.0.0-rc1

Este documento es la checklist humana para validar el release candidate local desde el ZIP limpio hasta VS Code con F9.

## Preparacion previa

1. Usa el ZIP local `publish\release\EstudioSocratico-2.0.0-rc1-win-x64.zip`.
2. Ejecuta la prueba en una carpeta temporal limpia.
3. Ten listas una cuenta real de GitHub y una cuenta real de Exercism.
4. Si vas a validar el fork y los remotes, prepara tambien la URL del repo oficial esperado.

## Comprobacion del ZIP

1. Extraer `EstudioSocratico-2.0.0-rc1-win-x64.zip` en carpeta temporal.
2. Confirmar que la raiz visible tenga solo:
   - `Instalar Estudio Socrático.exe`
   - `README.txt`
   - `payload\`
3. Confirmar que no exista nada visible como:
   - `src`
   - `_estudio`
   - `*.csproj`
   - scripts internos (`*.cmd`, `*.ps1`)
4. Confirmar que no exista `runtime-config.private.json`.
5. Confirmar que no aparezcan tokens, claves ni logs con secretos dentro de la carpeta extraida.

## Caso A — Instalacion desde cero

1. Extraer ZIP en carpeta temporal.
2. Doble clic en `Instalar Estudio Socrático.exe`.
3. Confirmar que abre UI Windows, no TUI/Textual.
4. Pantalla 1: Bienvenida correcta.
5. Pantalla 2: revision rapida correcta.
6. Pantalla 3: conectar GitHub.
7. Volver a la app y confirmar que detecta GitHub automaticamente.
8. Conectar Exercism.
9. Pegar token.
10. Confirmar que valida token sin lenguaje tecnico.
11. Confirmar que prepara ejercicios de C o guia al usuario si falta activar el track.
12. Pantalla 4: instalacion por bloques humanos.
13. Confirmar que no muestra nombres internos tipo `msys2-toolchain failed`.
14. Pantalla 5: final.
15. Pulsar `Abrir Estudio Socrático`.
16. Confirmar que VS Code abre el workspace correcto.
17. Confirmar que el workspace esta fuera del ZIP.
18. Confirmar `origin` = fork del estudiante.
19. Confirmar `upstream` = repo oficial.
20. Confirmar que F9 funciona.

## Caso B — Reinstalacion / reparacion

1. Ejecutar el instalador una segunda vez.
2. Confirmar que detecta lo ya instalado.
3. Confirmar que no repite pasos innecesarios.
4. Confirmar que no rompe el workspace.
5. Confirmar que puede reparar un bloque fallido.

## Caso C — Fallos guiados

Simular o documentar como verificar estos casos:

1. Sin internet.
2. Carpeta sin permisos.
3. GitHub no autenticado.
4. Token de Exercism invalido.
5. MSYS2 falla.
6. VS Code no existe.
7. Fork ya existe.
8. Workspace ya existe.

Para cada fallo, confirmar siempre estas tres cosas:

1. El mensaje visible es humano.
2. Existe opcion de solucion guiada.
3. El detalle tecnico queda oculto pero disponible si hace falta soporte.

## Evidencia recomendada

1. Captura de la raiz del ZIP extraido.
2. Captura de Pantalla 3 despues de volver desde GitHub.
3. Captura del mensaje de Exercism despues de pegar el token.
4. Captura de la Pantalla 5 final.
5. Captura de VS Code abierto en el workspace real.
6. Salida de `git remote -v` dentro del workspace abierto.
7. Confirmacion visual o por build task de que F9 funciona.