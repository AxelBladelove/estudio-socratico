# Troubleshooting

## WINGET_NOT_AVAILABLE

WinGet no existe, no esta en PATH o `source list` no responde.

Accion:

- Ejecutar Reparar.
- Si Windows no soporta WinGet, el configurador usara fallback oficial.

## NETWORK_DOWNLOAD_FAILED

Una descarga oficial fallo o fue interrumpida.

Accion:

- Revisar conexion.
- Reintentar.
- Exportar diagnostico si ocurre de forma repetida.

## MSYS2_INSTALL_FAILED

MSYS2 no quedo disponible en la ruta esperada.

Accion:

- Ejecutar Reparar.
- Verificar permisos para `C:\msys64`.
- Revisar antivirus o bloqueo de instaladores.

## GCC_VALIDATION_FAILED

`gcc` no compila un archivo C minimo.

Accion:

- Ejecutar Reparar toolchain.
- Confirmar que `C:\msys64\ucrt64\bin` aparece antes que otros GCC en PATH.

## GH_AUTH_FAILED

GitHub CLI no pudo autenticar o devolver usuario.

Accion:

- Usar Cambiar cuenta de GitHub.
- Confirmar navegador y acceso a GitHub.

## EXERCISM_TOKEN_INVALID

El token no permite descargar `hello-world` del track C.

Accion:

- Obtener un token nuevo en `https://exercism.org/settings/api_cli`.
- Pegar el token de nuevo.

## WORKSPACE_LOCKED

La carpeta elegida existe y no parece un workspace de Estudio Socratico.

Accion:

- Elegir otra carpeta.
- Usar Reparar solo si la carpeta contiene `AGENTS.md` y `_estudio/`.

## UNINSTALL_MANIFEST_MISSING

No hay manifest local para saber que limpiar.

Accion:

- Ejecutar Diagnostico.
- Limpiar manualmente solo rutas conocidas y no borrar `usuario/` sin respaldo.
