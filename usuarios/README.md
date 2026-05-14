# Datos Por Usuario

Esta carpeta guarda el historial de estudio de cada persona.

Cada clon local tiene un archivo `.estudio_usuario` en la raiz. Ese archivo
contiene un nombre corto, por ejemplo:

```text
axel
```

Con ese valor, el framework usa:

```text
usuario/errores.md
usuario/logs/<ejercicio>/bloqueN.log
```

## Registro GitHub/Rama

`usuario/registro.json` vincula cada cuenta real de GitHub con su rama del
proyecto:

```text
AxelBladelove -> axel
Erickcruzho   -> erick
```

Ese registro permite que una laptop nueva reconozca la cuenta autenticada con
`gh auth` y cambie a la rama correcta, sin crear una rama nueva por accidente.
El alias, `.estudio_usuario`, la carpeta `usuario` y la rama personal
deben mantenerse sincronizados.

## En La Version 1.0

Los `errores.md` salen vacios y los logs historicos no se incluyen. Cada
estudiante genera su propio historial al compilar.

## Que Va Aqui

- `errores.md`: patrones de aprendizaje del estudiante.
- `logs/`: intentos guardados por ejercicio y bloque.

No borres esta carpeta desde una IA o script salvo que el usuario lo pida
explicitamente.
