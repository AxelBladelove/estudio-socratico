/*
Estudio Socratico - instrucciones traducidas
Ejercicio: Grains

# Granos

Bienvenido a Granos en la pista de C de Exercism.
Si necesitas ayuda para ejecutar las pruebas o enviar tu código, consulta `HELP.md`.

## Introducción

Érase una vez un sabio sirviente que salvó la vida de un príncipe.
El rey prometió pagar lo que el sirviente soñara.
Sabiendo que al rey le encantaba el ajedrez, el sirviente le dijo al rey que le gustaría tener granos de trigo.
Un grano en la primera casilla de un tablero de ajedrez, con el número de granos duplicándose en cada casilla sucesiva.

## Instrucciones

Calcula el número de granos de trigo en un tablero de ajedrez.

Un tablero de ajedrez tiene 64 casillas.
La casilla 1 tiene un grano, la casilla 2 tiene dos granos, la casilla 3 tiene cuatro granos, y así sucesivamente, duplicándose cada vez.

Escribe código que calcule:

- el número de granos en una casilla dada
- el número total de granos en el tablero de ajedrez
*/

#include "grains.h"
uint64_t square(uint8_t index)
{

	if (index > 64)
	{
		return 0;
	}
	
	
	// primero tengo que definir el tablero
	uint64_t valtab[65]= {0};
	// luego relleno el tablero de granos, haciendo que cada uno duplique al anterior
	// para ello como no puedo modificar i, ya que me alteraria el for, usaria una variable cont
	uint64_t cont = 2;
	// necesitamos que la primera posicion del arreglo sea, y que la posicion 1 sea 1
	// la posicion siguiente seria la posicicion 2, la cual debe ser 2, hasta aqui coinciden posicion y valor
	// en la posicion 3 debe ser el doble del valor anterior
	//podemos hacer un if para empezar a multiplicar a partir de la posicion 3 del arreglo
	valtab[1] = 1;
	valtab[2] = 2;

	for (uint64_t i = 3; i <= 64; i++)
	{
		cont *= 2;
		valtab[i] = cont;
	}	
	
	return valtab[index];

	}


uint64_t total(void)
{
	// reutilizo el codigo anteior
	uint64_t valtab[65] = {0};
	uint64_t cont = 0;
	for (uint64_t i = 0; i < 64; i++)
	{
		cont++;
		valtab[i] += cont;
		cont *= 2;
		cont--;	
	}

	//hago un for que sume el valor de todas las cantidades dentro de cada indice
	uint64_t suma = 0;
	for (uint64_t i = 0; i < 64; i++)
	{
		suma += valtab[i];
	}
	return suma;
}



