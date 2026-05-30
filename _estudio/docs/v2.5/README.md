# Estudio Socratico 2.5

Estudio Socratico 2.5 abre una rama experimental para convertir el entorno en un sistema de aprendizaje de C asistido por IA. La version estable 2.0.16 no cambia: F9, updater, BYOK, importacion actual de Exercism y ocultamiento de archivos siguen protegidos por defecto.

## Objetivo

La IA debe ayudar cuando el ejercicio de C depende de contexto que esta oculto para principiantes: instrucciones originales, traduccion, `help.md`, tests, headers, archivos `.c`, `Makefile` y metadatos. El resultado visible debe ser minimo y didactico:

- explicar que funcion se espera;
- explicar parametros y retorno;
- sugerir o crear un `.h` minimo si el usuario activa el flag automatico;
- agregar hints a instrucciones traducidas solo si el usuario activa el flag automatico;
- no resolver el `.c`;
- no mostrar tests completos;
- no copiar `help.md` completo.

## Tooling JavaScript Con Bun

Bun es el package manager principal para 2.5. La raiz declara `packageManager: bun@1.3.13` y workspaces para:

- `_estudio/installer/ui`;
- `_estudio/soporte/vscode/estudio-exercism`.

El lock versionado es `bun.lock`. CI usa `oven-sh/setup-bun` y `bun ci`; los scripts usan `bun run` y `bunx` para el paquete VSIX. Los `package-lock.json` se eliminaron porque npm no es el flujo base.

`bunfig.toml` activa:

```toml
[install]
minimumReleaseAge = 259200
```

Esto retrasa resoluciones nuevas 3 dias para reducir riesgo de paquetes recien publicados. No se configuran `trustedDependencies` en los `package.json` versionados. El script de branding crea un paquete temporal fuera del repo y marca `sharp` como trusted solo ahi, porque `sharp` usa instalacion nativa para renderizar iconos. Ese directorio temporal no se versiona.

pnpm queda como plan B si Bun rompe un flujo critico. npm queda solo como fallback puntual documentado si una herramienta externa no funciona con Bun.

## Supply Chain

Medidas 2.5:

- `bun.lock` versionado;
- `bun ci` en CI;
- `minimumReleaseAge` de 3 dias;
- sin `node_modules`, `bin`, `obj`, `dist` ni `artifacts`;
- sin `package-lock.json`;
- sin dependencias nuevas salvo las ya necesarias;
- validacion local `bun run validate:v2.5`.

## Arquitectura IA

La extension agrega modulos CommonJS en `src/` para evitar un build step nuevo:

- `src/config.js`: defaults, merge de config local y preservacion de `apiKey`;
- `src/ai/providers.js`: fabrica de providers;
- `src/ai/geminiProvider.js`: BYOK Gemini;
- `src/ai/opencodeProvider.js`: OpenCode experimental;
- `src/ai/modelSelection.js`: selector `best-for-c-exercises`;
- `src/ai/exerciseAnalysis.js`: contexto oculto, prompt, saneamiento, dry-run y aplicacion segura;
- `src/ai/testGeneration.js`: stub apagado para tests IA.

Contrato `AiProvider`:

- `name`;
- `isAvailable()`;
- `complete(prompt, options)`;
- `listModels()` opcional.

`GeminiProvider` usa `provider`, `apiKey` y `model` del JSON local. La `apiKey` local tiene prioridad sobre `GEMINI_API_KEY` y nunca se imprime. El modelo default sigue siendo `gemini-2.5-flash`.

## OpenCode

OpenCode forma parte del stack, pero no bloquea setup ni importacion. Estados previstos:

- `notInstalled`;
- `detected`;
- `installedByEstudio`;
- `requiresConfiguration`;
- `ready`;
- `error`.

En esta iteracion, `OpenCodeProvider`:

- esta apagado por defecto con `features.opencodeIntegration=false`;
- usa `experimental.opencodeCommand`, default `opencode`;
- detecta version con `opencode --version`;
- intenta listar modelos con `opencode models --json` si esta disponible;
- devuelve unavailable si falla;
- no instala globalmente ni toca WSL.

Instalacion tecnica recomendada para pruebas:

- Windows nativo: instalar `opencode` por el metodo oficial elegido por el usuario y dejarlo en `PATH`;
- Scoop/Chocolatey/npm/binario: permitido solo como decision explicita del usuario;
- WSL: no se toca automaticamente; debe configurarse aparte;
- Estudio solo invoca el comando definido en `experimental.opencodeCommand`.

## LiteLLM Futuro

LiteLLM Proxy no entra como dependencia obligatoria en 2.5 inicial. La interfaz `AiProvider` deja listo un `LiteLLMProvider` futuro sin reescribir el analisis de ejercicios.

## Feature Flags

Example versionado:

- `features.aiExerciseAnalysis=false`;
- `features.opencodeIntegration=false`;
- `features.aiGeneratedTests=false`;
- `features.autoGitSync=false`;
- `experimental.aiExerciseAnalysisProvider=gemini`;
- `experimental.applyHeaderPatchesAutomatically=false`;
- `experimental.appendInstructionHintsAutomatically=false`.

La extension y el instalador completan campos faltantes sin borrar `apiKey`.

## Flujo De Analisis IA

Cuando se importa Exercism:

- si `aiExerciseAnalysis=false`, el flujo se comporta como 2.0.16;
- si `aiExerciseAnalysis=true`, la extension arma `ExerciseAnalysisInput` desde el ejercicio importado;
- incluye contexto oculto internamente;
- llama al provider configurado;
- genera `ExerciseAnalysisResult`;
- si los flags automaticos estan apagados, solo muestra dry-run;
- si estan encendidos y el riesgo es bajo, puede aplicar headers o apendice README.

La salida visible muestra provider, modelo, archivos detectados, prototipos sugeridos, hints y advertencias. No muestra tests completos ni copia `help.md`.

## Generacion Segura De Headers

Un parche de header solo es seguro si:

- termina en `.h`;
- no usa `..`;
- no contiene cuerpos de funcion;
- contiene prototipos o guards;
- `solutionLeakRisk=low`.

El `.c` del estudiante no se modifica.

## Comando Experimental

VS Code aporta:

```text
Estudio Socratico: Analizar ejercicio con IA (experimental)
```

Si el flag esta apagado, abre una explicacion y ofrece abrir config. Si esta encendido, analiza el archivo activo en dry-run y escribe en el output channel `Estudio Socratico IA`.

Tambien queda registrado el stub:

```text
Estudio Socratico: Generar tests de logica con IA (experimental)
```

## @test Futuro

Diseno previsto:

- flag `features.aiGeneratedTests`;
- entrada: `.c` activo;
- salida: `.estudio-tests/`, ignorado por Git;
- no cambia F9;
- no toca tests oficiales de Exercism;
- comando separado para validar estilo Exercism;
- regeneracion limpia bajo demanda.

El stub actual no escribe archivos.

## Push/Pull Automatico Futuro

Diseno previsto detras de `features.autoGitSync=false`:

- `pull --rebase` solo con repo limpio;
- detener si hay conflicto;
- push despues de commits automaticos;
- revisar que no haya `apiKey`, config local ni logs sensibles;
- modo offline;
- misma cuenta GitHub + mismo alias = mismo repo workspace.

No se implementa push/pull automatico en esta iteracion.

## Riesgos Y Mitigaciones

- Riesgo: la IA revele solucion. Mitigacion: prompt restrictivo, saneamiento visible y rechazo de parches con cuerpos de funcion.
- Riesgo: romper importacion. Mitigacion: feature flag apagado y errores IA no bloqueantes.
- Riesgo: OpenCode no instalado. Mitigacion: unavailable limpio.
- Riesgo: supply chain JS. Mitigacion: Bun, lock, `bun ci`, `minimumReleaseAge`, sin trusted dependencies innecesarias.
- Riesgo: BYOK en logs. Mitigacion: no imprimir config completa y validaciones de redaccion existentes.
