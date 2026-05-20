# Release v2

El release oficial se genera desde GitHub Actions.

## Tag

```powershell
git tag v2.0.0
git push origin v2.0.0
```

## Pipeline

Archivo:

```text
.github/workflows/release-installer.yml
```

Pasos:

1. Instala .NET 10.
2. Restaura la solucion.
3. Compila proyectos .NET.
4. Ejecuta tests.
5. Publica WinUI self-contained.
6. Publica worker elevado.
7. Publica CLI.
8. Construye MSI WiX.
9. Construye bundle Burn.
10. Genera SHA256.
11. Sube artefacto.
12. Adjunta a GitHub Release si el tag es `v2.0.0`.

## Artefactos

```text
Estudio-Socratico-Setup-v2.0.0-x64.exe
Estudio-Socratico-Setup-v2.0.0-x64.exe.sha256
```

No se commitean binarios generados.

## Smoke Test Manual Del Release

1. Descargar el `.exe` desde GitHub Releases.
2. Ejecutar con doble click.
3. Confirmar que abre la experiencia WinUI.
4. Ejecutar Diagnostico.
5. Ejecutar Configurar en una maquina limpia o VM.
6. Confirmar VS Code, F9, GitHub, Exercism y workspace.
7. Exportar diagnostico.
