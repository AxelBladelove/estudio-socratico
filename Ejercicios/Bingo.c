/* Realizar un programa interactivo en C que simule un juego de BINGO con un cartón. Este debe presentarse
gráficamente, identificando con un color diferente los recuadros de las bolas que han salido. El juego
generará el cartón de forma aleatoria. Asegúrese que una combinación no se repita para cada columna
del cartón. Tome en cuenta que cada una de las letras de Bingo corresponde a un cierto rango de
números, como se indica a continuación.
B: 1-15
I: 16-30
N: 31-45
G: 46-60
O: 61-75
El cartón tendrá cinco columnas, rotuladas B-I-N-G-O. Cada columna contendrá cinco números, dentro
de los rangos indicados arriba. La posición central de cada cartón se cubre antes de comenzar el juego
(jugada gratis). Se gana cuando se tenga una línea de números sacados (en vertical, horizontal o en
diagonal). Luego de esto se preguntará si se desea jugar a cartón lleno y en caso de ser afirmativo se debe
continuar sacando bolos. Cada vez que termine una mano se debe preguntar si se desea continuar
jugando. Se debe presentar las estadísticas de ganados vs perdidos en el momento que el usuario desee.
Una vez iniciado el juego se presenta una bola que se genera de manera aleatoria, sin repetición. El
usuario presionará [ENTER] sobre la bola en su cartón y en caso de que no esté presionará [ESC]. Si el
usuario presiona [ESC] y la bola está en el cartón se le descuenta el 10% de lo apostado. Se debe presentar
la bola generada, en un área de la pantalla conjuntamente con la información del cartón. El usuario puede
cambiar de cartón cuantas veces lo desee antes de comenzar el juego. */



#include <stdio.h>
#include <stdlib.h>
#include <conio.h>
#include <time.h>

#define dim 		5
#define ENTER     13
#define ESC       27
#define ARRIBA    72
#define ABAJO     80
#define DERECHA   77
#define IZQUIERDA 75
#define ESP       32
#define BOLASTOTAL 75




void showcarton (int carton[dim][dim],int posx, int posy);
int bolas (int bolillero[BOLASTOTAL]);
int sacarbolas🥚🥚(int indice, bolillero[BOLASTOTAL])


int main(){

	int jugando = 1;
	int carton[dim][dim];
	int bolillero[BOLASTOTAL] = {0};
	srand(time(NULL));
	int B1, B2, I1, I2, N1, N2, G1, G2, O1, O2;
	B1 = 1;
	B2 = 15;
	I1 = 16;
	I2 = 30;
	N1 = 31;
	N2 = 45;
	G1 = 46;
	G2 = 60;
	O1 = 61;
	O2 = 75;
	

while (jugando == 1)
{
	bolas(bolillero);
	showcarton(carton, 15, 15);
}



	return;
}

void showcarton (int carton[dim][dim],int posx, int posy)
{
	int fil;
	int col;

	for (col = 0; col < dim; col++){
		for (fil = 0; fil < dim; fil++){
			bol
			
			if (condition)
			{
					
			}
			


		}
	}
}





int bolas (int bolillero[BOLASTOTAL])
{
	int indice;
	do
	{
		indice =  randrange(1, BOLASTOTAL - 1);
	} while (bolillero[indice] == 1);
	bolillero[indice] = 1;
	
	return indice;  
}




int randrange(int limif,int limsp)
{
	return (rand() % (limsp - limif + 1) + limif);
}

int sacarbolas🥚🥚 (int indice, int bolillero[BOLASTOTAL])
{
	bolas(bolillero)


}