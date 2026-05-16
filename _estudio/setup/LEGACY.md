# Decision Sobre Scripts Legacy

## Estado 2.0

La ruta activa del instalador es:

```bat
_estudio\setup\Estudio.Setup.cmd
```

Sin argumentos abre la interfaz visual, ejecuta `verify` automaticamente y deja
elegir el modo desde la misma pantalla.

Los scripts historicos `instalar.ps1`, `proyecto.ps1`, `vscode.ps1`,
`gcc_msys2.ps1`, `herramientas.ps1` y `utilidades.ps1` quedan congelados. No
se eliminan en 2.0 para no romper clones antiguos ni referencias tecnicas, pero
no deben recibir funcionalidad nueva.

## Regla De Mantenimiento

- Cambios nuevos de setup van en `Estudio.Setup/`.
- `Estudio.Setup.cmd` es el unico wrapper recomendado.
- Los accesos directos de modo en la raiz fueron retirados; queda un unico
  `Estudio.Setup.cmd` como entrada interactiva.
- Si un script legacy se toca, debe ser solo para compatibilidad o para dirigir
  al usuario hacia el instalador 2.0.

## Empaquetado

El release limpio se genera con:

```bat
_estudio\setup\Estudio.Setup.cmd package
```

La salida queda en `_estudio/setup/Estudio.Setup/publish/release/` e incluye
carpeta, ZIP y `release-manifest.json`.
