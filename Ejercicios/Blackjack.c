
/*Escribir un programa en C que simule un juego de “BlackJack” entre la computadora y un
jugador. La computadora será quien repartirá las cartas.
Las cartas se dan en orden, primero una carta a cada uno, después otra carta a cada uno. Se
pueden demandar cartas adicionales.
El objetivo del juego es obtener 21 puntos, o tantos puntos como sea posible sin exceder a 21 en
cada mano. Un jugador es automáticamente descalificado si las cartas en su mano exceden de
21 puntos. Las figuras cuentan 10 puntos y un As puede contar un punto u 11 puntos. Así, un
jugador puede obtener 21 puntos (“BlackJack!”) si tiene un as y una figura o un 10. Si el jugador
tiene menos puntos con sus dos primeras cartas, puede pedir una carta o más, mientras su
puntuación no pase de 21.
Utilizar la generación de números aleatorios para simular el reparto de las cartas. Una carta no
puede ser dada más de una vez. Las cartas deben aparecer dibujadas en la pantalla. La
computadora siempre se arriesga a pedir una carta adicional si el jugador va en ventaja.
Para realizar el juego Blackjack se requerirá utilizar un arreglo para representar el mazo de cartas
a repartir entre el “jugador” y la “computadora”. El mismo inicializa el arreglo en la declaración
en el main, de la siguiente forma: int mazo[MAXCARTAS] = {0}; donde MAXCARTAS es una macro
con la cantidad máxima de cartas (52) y la inicialización en 0 indica que ninguna carta ha sido
“repartida”.
Una posible representación gráfica del arreglo sería:
0 0 0 0 0 0 0 0 0 0 1 0 0 0 0 . . . 1 0 0 0 0 1 0 0
0 1 2 3 4 5 6 7 8 9 10 11 12 13 14 . . . 44 45 46 47 48 49 50 51
El programador asume que las cartas están colocadas en orden en el mazo generado. De tal
forma que en la posición 0, 4, 8, 12,…; estarían los corazones, 1, 5, 9, 13,…; estarían los
diamantes, 2, 6, 10, 14,…; estarían los tréboles y 3, 7, 11, 15,…; estarían las espadas (piques) y así
sucesivamente. Los valores en la tabla ASCII del corazón, diamante, trébol y espada son 3,4,5 y
6 respectivamente.
Para almacenar las cartas del jugador y la computadora utilizará dos arreglos:
int jugador[MAXCARTAREP], computadora[MAXCARTAREP];// (que contendrán los índices de las
cartas asignadas del arreglo mazo).
MAXCARTAREP es una macro que contiene la cantidad máxima de cartas a repartir (11).
PONTIFICIA UNIVERSIDAD CATÓLICA MADRE Y MAESTRA
FACULTAD DE CIENCIAS E INGENIERÍA Pág. 2 de 2
ESCUELA DE INGENIERÍA EN COMPUTACIÓN Y TELECOMUNICACIONES
Profesor: Alejandro Liz
Se utilizará las siguientes funciones:
int simbolocarta(int indice), que dado un indice, retorna: 0 si el símbolo correspondiente según
el índice del arreglo es un corazón, 1 si el símbolo correspondiente según el índice del arreglo es
un diamante, 2 si el símbolo correspondiente según el índice del arreglo es un trébol y 3 si el
símbolo correspondiente según el índice del arreglo es una espada (o pique).
int valorcarta(int indice), que retorna el valor de la carta de acuerdo al valor del índice. Es decir,
los primeros 4 valores son los 1 o los ases (‘A’), los siguientes 4 son los 2’s, y así sucesivamente
de tal forma que los últimos 4 son los 13’s.
int tomarcarta(int mazo[ ]), que retorna, de forma aleatoria, el índice de una carta disponible
para “repartir” asignando a la misma el valor de 1.
void showcart_xy(int indice, int px, int py), que imprime de forma gráfica, en la posición indicada
en “px” y “py”, la carta que se encuentra en el índice “indice” [0,51].
int sumacarta(int cartas[ ], int n), que retorna la suma de “n” cartas en el arreglo “cartas” que
contiene los índices de las cartas asignadas. Así,si se llama con las cartas contenidas en el arreglo
de cartas de jugador “jugador” o de la computadora “computadora” se tendría el total de la
suma óptima de las mismas. Es decir, que busque no pasarse de 21 puntos.
Se requiere:
1. Definición de las macros identificadas
2. Realización del “main” que incluye declaración de los arreglos mazo, Jugador y
Computadora.
3. Realización de las funciones: símbolocarta, valorcarta, tomarcarta.
4. Desde el “main” iniciar la iteración entre computador y jugador permitiendo seleccionar
las cartas e indicando el total que van acumulando.
5. Realizar las funciones showcart_xy y sumarcarta.
6. Controlar las situaciones del juego:
a. Indicar al jugador cuando pierde
b. La computadora debe pedir una carta adicional siempre y cuando no sume 21 y el
jugador no haya perdido.
7. El jugador podrá jugar cuantos juegos desee
8. Al finalizar el juego se debe presentar la cantidad de juegos jugados, ganados y perdidos.*/ 

#include <stdio.h>
#include <stdlib.h>


int main()
{


char ascii = 'A';

	for(ascii = 1; ascii < 300 ; ascii++){

		printf(" %c", ascii);
	}

    return 0;
}










