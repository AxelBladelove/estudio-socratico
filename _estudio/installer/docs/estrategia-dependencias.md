# Estrategia De Dependencias

El configurador no asume que la maquina esta bien configurada. Cada dependencia
se trata como un ciclo:

1. Detectar.
2. Evaluar si sirve.
3. Instalar o reparar si hace falta.
4. Validar con comandos reales.
5. Registrar resultado en manifest y logs.

## WinGet Primero

WinGet se usa solo si:

- `winget --info` responde.
- `winget source list` confirma source `winget`.
- El paquete tiene ID conocido.

IDs usados:

| Dependencia | WinGet ID |
|---|---|
| Node.js LTS | `OpenJS.NodeJS.LTS` |
| Python | `Python.Python.3.13` |
| Git | `Git.Git` |
| GitHub CLI | `GitHub.cli` |
| Exercism CLI | `Exercism.CLI` |
| VS Code | `Microsoft.VisualStudioCode` |
| MSYS2 | `MSYS2.MSYS2` |

## Fallback Oficial

Si WinGet no esta disponible o falla, el motor usa fuentes oficiales:

- Node.js: indice oficial de `nodejs.org`.
- Python: pagina oficial de releases de `python.org`.
- Git: release oficial `git-for-windows/git`.
- GitHub CLI: release oficial `cli/cli`.
- Exercism CLI: release oficial `exercism/cli`.
- VS Code: endpoint oficial `update.code.visualstudio.com`.
- MSYS2: release oficial `msys2/msys2-installer`.

Cuando hay SHA256 disponible se valida. Si la fuente no publica hash directo en
el asset elegido, el configurador calcula y registra el hash descargado para
diagnostico posterior.

## Toolchain C

El estandar del proyecto es:

```text
C:\msys64
C:\msys64\ucrt64\bin
mingw-w64-ucrt-x86_64-gcc
mingw-w64-ucrt-x86_64-make
make
```

Validaciones:

- `gcc --version`
- `make --version`
- `where gcc`
- `where make`
- compilacion de un C minimo
- smoke test del flujo `build.cmd` con commit omitido por variable interna

El uso normal del estudiante no cambia.
