# Datos Por Usuario

Cada clon local del proyecto debe tener un identificador de usuario en
`.estudio_usuario` (archivo local, ignorado por Git). Ese valor determina la
telemetria personal que se guarda en:

- `usuarios/<usuario>/errores.md`
- `usuarios/<usuario>/logs/<ejercicio>/bloqueN.log`

Ejemplo rapido en Windows:

```text
copy .estudio_usuario.example .estudio_usuario
```

Luego edita `.estudio_usuario` y deja una sola linea con tu slug, por ejemplo:

```text
axel
```

Si el archivo no existe, `compilar_y_grabar.bat` lo crea automaticamente usando
`ESTUDIO_USUARIO`, `git config github.user`, `git user.name` o el usuario de Windows.

Compatibilidad:

- `errores.md` y `logs/` en la raiz se conservan como legado mientras se migra.
- El script nuevo prioriza `usuarios/<usuario>/...` y solo usa el legado como
  fuente inicial si la telemetria por usuario todavia no existe.