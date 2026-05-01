# Setup Windows

Esta carpeta contiene el instalador nuevo del proyecto.

## Entrada principal

Para doble click:

```text
setup/instalar.cmd
```

Desde terminal:

```bat
setup\instalar.cmd
```

## Modo verificacion

```bat
setup\instalar.cmd -SoloVerificar -SinWinget -SinExtensiones
```

## Archivos

| Archivo | Rol |
|---|---|
| `instalar.cmd` | Entrada doble-clickable para Windows |
| `instalar.ps1` | Orquestador principal |
| `utilidades.ps1` | Logs, consola, PATH y comandos |
| `herramientas.ps1` | Deteccion e instalacion con winget |
| `gcc_msys2.ps1` | Instalacion de GCC via MSYS2 |
| `vscode.ps1` | Terminal y extensiones de VS Code |
| `proyecto.ps1` | Validacion del workspace y Git local |
