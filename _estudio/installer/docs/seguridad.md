# Seguridad

## Principios

- La UI no corre elevada.
- El worker elevado es un ejecutable separado.
- No existe API generica `RunCommand(string command)` expuesta a la UI.
- Cada accion elevada es una operacion concreta y validada.
- Los comandos se ejecutan con `FileName` y lista de argumentos, no como string
  concatenado.
- Los logs redactan tokens, secretos y cabeceras de autorizacion.
- El manifest decide que puede desinstalarse sin borrar datos ajenos.

## Secretos

No se registran:

- tokens de GitHub
- token de Exercism
- contrasenas
- cabeceras `Authorization`
- argumentos `--token`

La clase `SecretRedactor` se aplica a logs, errores y resultados de comandos.

## Elevacion

Operaciones permitidas:

- `InstallMsys2`
- `AddMachinePath`
- `InstallWingetPackage`
- `RunOfficialInstaller`
- `RemoveManagedDependency`
- `RepairPath`

El worker valida extensiones de instalador, rutas bajo `%LocalAppData%` cuando
borra elementos gestionados y parametros como IDs de WinGet.

## Desinstalacion

La desinstalacion segura usa:

```text
%LocalAppData%\EstudioSocratico\installer-manifest.json
```

No borra dependencias que ya existian antes salvo limpieza agresiva confirmada
por el usuario y restringida a directorios gestionados.
