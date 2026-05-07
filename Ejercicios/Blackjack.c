
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

Se utilizará las siguientes funciones:

int simbolocarta(int indice), que dado un indice, retorna:
0 si el símbolo correspondiente según el índice del arreglo es un corazón,
1 si el símbolo correspondiente según el índice del arreglo es un diamante,
2 si el símbolo correspondiente según el índice del arreglo es un trébol y
3 si el símbolo correspondiente según el índice del arreglo es una espada (o pique).

int valorcarta(int indice), que retorna el valor de la carta de acuerdo al valor del índice. Es decir,
los primeros 4 valores son los 1 o los ases (‘A’), los siguientes 4 son los 2’s, y así sucesivamente
de tal forma que los últimos 4 son los 13’s.

int tomarcarta(int mazo[ ]), que retorna, de forma aleatoria, el índice de una carta disponible
para “repartir” asignando a la misma el valor de 1.

void showcart_xy(int indice, int px, int py), que imprime de forma gráfica, en la posición indicada
en “px” y “py”, la carta que se encuentra en el índice “indice” [0,51].

int sumacarta(int cartas[ ], int n), que retorna la suma de “n” cartas en el arreglo “cartas” que
de cartas de jugador “jugador” o de la computadora “computadora” se tendría el total de la
suma óptima de las mismas. Es decir, que busque no pasarse de 21 puntos.

Se requiere:

contiene los índices de las cartas asignadas. Así,si se llama con las cartas contenidas en el arreglo
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
#include <time.h>
#include <conio.h>

#define MAXCARTAS 52
#define MAXCARTAREP 11
#define BLACKJACK 21
#define ESI 218
#define ESD 191
#define LH 196
#define EII 192
#define EID 217
#define LV 179
#define REPARTO_INICIAL 2
#define RESULTADO_NINGUNO 0
#define RESULTADO_GANADO 1
#define RESULTADO_PERDIDO 2
#define RESULTADO_EMPATE 3
#define SIMBOLO_ASCII_INICIAL 3


int randrange(int liminf, int limsup);
int simbolocarta(int indice);
int valorcarta(int indice);
int tomarcarta(int mazo[ ]);
void showcart_xy(int indice, int px, int py);
int sumacarta(int cartas[ ], int n);
int valcart(int indice);
void showpoints_xy(int points, int px, int py);
void showbuttons_xy(void);



int main()
{
   srand(time(NULL));

   int manos_jugadas = 0;
   int manos_ganadas = 0;
   int manos_perdidas = 0;
   char tecla;
   int jugando = 1;

   while (jugando == 1)
   {
      system("cls");

      int mazo[MAXCARTAS] = {0};
      int jugador[MAXCARTAREP];
      int computadora[MAXCARTAREP];
      int cantcart_jugador = 0;
      int cantcart_computadora = 0;
      int puntos_jugador = 0;
      int puntos_computadora = 0;
      int mano_actual = 1;
      int resultado_mano = RESULTADO_NINGUNO;

      for (int i = 0; i < REPARTO_INICIAL; i++)
      {
         computadora[cantcart_computadora] = tomarcarta(mazo);
         showcart_xy(computadora[cantcart_computadora], (2 + (cantcart_computadora * 10)), 1);
         cantcart_computadora += 1;
         puntos_computadora = sumacarta(computadora, cantcart_computadora);
         showpoints_xy(puntos_computadora, 1, 9);

         Sleep(700);

         jugador[cantcart_jugador] = tomarcarta(mazo);
         showcart_xy(jugador[cantcart_jugador], (2 + (cantcart_jugador * 10)), 20);
         cantcart_jugador += 1;
         puntos_jugador = sumacarta(jugador, cantcart_jugador);
         showpoints_xy(puntos_jugador, 1, 28);

         Sleep(700);
      }

      if (puntos_jugador == BLACKJACK && puntos_computadora == BLACKJACK)
      {
         manos_jugadas++;
         resultado_mano = RESULTADO_EMPATE;
         mano_actual = 0;
      }
      else if (puntos_computadora == BLACKJACK)
      {
         manos_jugadas++;
         manos_perdidas++;
         resultado_mano = RESULTADO_PERDIDO;
         mano_actual = 0;
      }
      else if (puntos_jugador == BLACKJACK)
      {
         manos_jugadas++;
         manos_ganadas++;
         resultado_mano = RESULTADO_GANADO;
         mano_actual = 0;
      }

      while (mano_actual == 1)
      {
         showbuttons_xy();

         gotoxy(1, 31);
         tecla = getch();

         switch (tecla)
         {
         case 'e':
         case 'E':
            jugador[cantcart_jugador] = tomarcarta(mazo);
            showcart_xy(jugador[cantcart_jugador], (2 + (cantcart_jugador * 10)), 20);
            cantcart_jugador += 1;
            puntos_jugador = sumacarta(jugador, cantcart_jugador);
            showpoints_xy(puntos_jugador, 1, 28);

            if (puntos_jugador == BLACKJACK)
            {
               manos_jugadas++;
               manos_ganadas++;
               resultado_mano = RESULTADO_GANADO;
               mano_actual = 0;
            }
            else if (puntos_jugador > BLACKJACK)
            {
               manos_jugadas++;
               manos_perdidas++;
               resultado_mano = RESULTADO_PERDIDO;
               mano_actual = 0;
            }
            break;

         case 'r':
         case 'R':
            while (puntos_computadora < BLACKJACK && puntos_computadora < puntos_jugador)
            {
               computadora[cantcart_computadora] = tomarcarta(mazo);
               showcart_xy(computadora[cantcart_computadora], (2 + (cantcart_computadora * 10)), 1);
               cantcart_computadora += 1;
               puntos_computadora = sumacarta(computadora, cantcart_computadora);
               showpoints_xy(puntos_computadora, 1, 9);

               Sleep(700);
            }

            if (puntos_computadora > BLACKJACK || puntos_jugador > puntos_computadora)
            {
               manos_ganadas++;
               resultado_mano = RESULTADO_GANADO;
            }
            else if (puntos_jugador == puntos_computadora)
            {
               resultado_mano = RESULTADO_EMPATE;
            }
            else
            {
               manos_perdidas++;
               resultado_mano = RESULTADO_PERDIDO;
            }

            manos_jugadas++;
            mano_actual = 0;
            break;

         case 'q':
         case 'Q':
            jugando = 0;
            mano_actual = 0;
            break;

         default:
            gotoxy(1, 31);
            printf("Tecla invalida, usa E, R o Q\n\n");
            break;
         }
      }

      if (jugando == 1)
      {
         gotoxy(1, 30);

         printf("Jugadas: %d  Ganadas: %d  Perdidas: %d\n", manos_jugadas, manos_ganadas, manos_perdidas);

         if (resultado_mano == RESULTADO_GANADO)
         {
            printf("Resultado: Ganaste la mano\n\n");
         }
         else if (resultado_mano == RESULTADO_PERDIDO)
         {
            printf("Resultado: Perdiste la mano\n\n");
         }
         else if (resultado_mano == RESULTADO_EMPATE)
         {
            printf("Resultado: Empate\n\n");
         }

         printf("Presiona A para jugar otra mano o D para salir\n");

         do
         {
            tecla = getch();

            if (tecla == 'd' || tecla == 'D')
            {
               jugando = 0;
            }
            else if (tecla != 'a' && tecla != 'A')
            {
               printf("Tecla invalida. Usa A o D\n");
            }

         } while (tecla != 'a' && tecla != 'A' && tecla != 'd' && tecla != 'D');
      }
   }

   gotoxy(1, 30);
   printf("Resumen final -> Jugadas: %d  Ganadas: %d  Perdidas: %d\n", manos_jugadas, manos_ganadas, manos_perdidas);

   gotoxy(1, 31);

   return 0;    
}





/*
   Función: simbolocarta
   Argumentos: int indice. Indica la posición de la carta en el mazo.
   Objetivo: Determinar el símbolo correspondiente a la carta indicada.
   Retorno: Valor entero que representa el símbolo de la carta.
*/
int simbolocarta(int indice)
{
   return indice % 4;
}


/*
   Función: valorcarta
   Argumentos: int indice. Indica la posición de la carta en el mazo.
   Objetivo: Determinar el valor original de la carta según su índice.
   Retorno: Valor entero entre 1 y 13.
*/
int valorcarta(int indice)
{
   return (indice / 4) + 1;
}


/*
   Función: tomarcarta
   Argumentos: int mazo[]. Arreglo que indica cuáles cartas ya fueron repartidas.
   Objetivo: Seleccionar una carta disponible al azar y marcarla como repartida.
   Retorno: Índice entero de la carta tomada.
*/
int tomarcarta(int mazo[])
{
   int indice;
   do
   {
      indice = randrange(0, MAXCARTAS - 1);
   } while (mazo[indice]);
   mazo[indice] = 1;
   return indice;
}


/*
   Función: randrange
   Argumentos: int liminf, int limsup. Límites inferior y superior del rango.
   Objetivo: Generar un número aleatorio dentro del rango indicado.
   Retorno: Valor entero aleatorio entre liminf y limsup.
*/
int randrange(int liminf, int limsup)
{
   return (rand() % (limsup - liminf + 1)) + liminf;
}


/*
   Función: showcart_xy
   Argumentos: int indice, int px, int py. Indican la carta y la posición.
   Objetivo: Dibujar en pantalla la carta indicada en las coordenadas dadas.
   Retorno: Ninguno.
*/
void showcart_xy(int indice, int px, int py)
{
   gotoxy(px, py);
   printf("%c%c%c%c%c%c%c%c%c", ESI, LH, LH, LH, LH, LH, LH, LH, ESD);
   gotoxy(px, py + 1);
   if (valorcarta(indice) == 1)
   {
      printf("%c%-2c     %c", LV, 'A', LV);
   }
   else
   {
      printf("%c%-2d     %c", LV, valcart(indice), LV);
   }

   gotoxy(px, py + 2);
   printf("%c%c%c%c%c%c%c%c%c", LV, ' ', ' ', ' ', ' ', ' ', ' ', ' ', LV);
   gotoxy(px, py + 3);
   if (valorcarta(indice) == 1)
   {
      printf("%c   %c   %c", LV, 'A', LV);
   }
   else
   {
      printf("%c   %c   %c", LV, (simbolocarta(indice) + SIMBOLO_ASCII_INICIAL), LV);
   }

   gotoxy(px, py + 4);
   printf("%c%c%c%c%c%c%c%c%c", LV, ' ', ' ', ' ', ' ', ' ', ' ', ' ', LV);
   gotoxy(px, py + 5);
   if (valorcarta(indice) == 1)
   {
      printf("%c     %2c%c", LV, 'A', LV);
   }
   else
   {
      printf("%c     %2d%c", LV, valcart(indice), LV);
   }

   gotoxy(px, py + 6);
   printf("%c%c%c%c%c%c%c%c%c", EII, LH, LH, LH, LH, LH, LH, LH, EID);
}


/*
   Función: valcart
   Argumentos: int indice. Indica la posición de la carta en el mazo.
   Objetivo: Convertir el valor original de la carta al valor usado en Blackjack.
   Retorno: Valor entero de la carta para el juego.
*/
int valcart(int indice)
{
   int val = valorcarta(indice);

   if (val >= 10)
   {
      return 10;
   }
   else
      return val;
}


/*
   Función: sumacarta
   Argumentos: int cartas[], int n. Arreglo de cartas y cantidad a sumar.
   Objetivo: Calcular el total óptimo de una mano sin pasarse de 21 si es posible.
   Retorno: Suma entera de los puntos de la mano.
*/
int sumacarta(int cartas[ ], int n)
{
   int total = 0, cantA = 0, valorcart;
   for (int ind = 0; ind < n; ind++)
   {
      valorcart = valcart(cartas[ind]);
      if (valorcart == 1)
      {
         cantA++;
         total += valorcart + 10;
      }
      else
         total += valorcart;
   }

   while (total > BLACKJACK && (cantA > 0))
   {
      total -= 10, cantA--;
   }

   return total;
}


/*
   Función: showpoints_xy
   Argumentos: int points, int px, int py. Puntos y posición en pantalla.
   Objetivo: Mostrar los puntos del jugador o la computadora.
   Retorno: Ninguno.
*/
void showpoints_xy(int points, int px, int py)
{
   if (py == 9)
   {
      gotoxy(px, py);
      printf("Puntos Computadora: %d\n", points);
   }
   else
   {
      gotoxy(px, py);
      printf("Puntos jugador: %d\n", points);
   }

   return;
}


/*
   Función: showbuttons_xy
   Argumentos: Ninguno.
   Objetivo: Mostrar las opciones disponibles para el jugador.
   Retorno: Ninguno.
*/
void showbuttons_xy(void)
{
   gotoxy(1, 14);

   printf("Pedir carta: E");

   gotoxy(20, 14);

   printf("Plantarse: R");

   gotoxy(40, 14);

   printf("Salir del juego: Q");
}
