# Datos Por Usuario

Esta carpeta guarda el historial de estudio de cada persona.

Cada clon local tiene un archivo `.estudio_usuario` en la raiz. Ese archivo
contiene un nombre corto, por ejemplo:

```text
axel
```

Con ese valor, el framework usa:

```text
usuarios/axel/errores.md
usuarios/axel/logs/<ejercicio>/bloqueN.log
```

## En La Version 1.0

Los `errores.md` salen vacios y los logs historicos no se incluyen. Cada
estudiante genera su propio historial al compilar.

## Que Va Aqui

- `errores.md`: patrones de aprendizaje del estudiante.
- `logs/`: intentos guardados por ejercicio y bloque.

No borres esta carpeta desde una IA o script salvo que el usuario lo pida
explicitamente.
