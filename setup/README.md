# Setup Windows

Esta carpeta contiene el instalador de Estudio Socratico.

## Entrada Recomendada

```bat
setup\instalar.cmd
```

Ese comando esta pensado para ejecutarse desde una terminal abierta en la raiz
del repo.

## Que Hace

- valida que el repo este completo;
- pregunta el usuario de estudio si hace falta;
- configura Git local;
- crea `.estudio_usuario`;
- prepara `usuarios/<slug>/errores.md`;
- crea o activa la rama personal;
- instala o valida herramientas base;
- instala o valida GCC/MSYS2;
- compila herramientas locales del framework;
- configura `F9` para compilar desde VS Code.

## Modo Verificacion

```bat
setup\instalar.cmd -SoloVerificar -SinWinget -SinExtensiones
```

## Modo No Interactivo

```bat
setup\instalar.cmd -SinOnboarding -UsuarioSlug axel -GitHubUsuario AxelBladelove -GitNombre AxelBladelove -GitCorreo AxelBladelove@users.noreply.github.com
```

## Archivos

| Archivo | Rol |
|---|---|
| `instalar.cmd` | Entrada doble-clickable para Windows |
| `instalar.ps1` | Orquestador principal |
| `utilidades.ps1` | Logs, consola, PATH y comandos |
| `herramientas.ps1` | Deteccion e instalacion con winget |
| `gcc_msys2.ps1` | Instalacion de GCC via MSYS2 |
| `vscode.ps1` | Terminal, extensiones y F9 |
| `proyecto.ps1` | Validacion del workspace, usuario y Git local |
