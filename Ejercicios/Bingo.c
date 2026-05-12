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
#define CT      	LIGHTBLUE
#define CF 			WHITE
#define ANCHO_CASILLA 4
#define ALTO_CASILLA  1

#define BOLASTOTAL 75
#define B 1
#define I 2
#define N 3
#define G 4
#define O 5
#define POS_B_x
#define POS_B_y
#define POS_I_x
#define POS_I_y
#define POS_N_x
#define POS_N_y
#define POS_G_x
#define POS_G_y
#define POS_O_x
#define POS_O_y



void setcolor(int ct, int cf);
void colordefault(void);
void llenar_carton (int carton[dim][dim], int num_carton[BOLASTOTAL]);
void 

void showcarton (int carton[dim][dim], int posx, int posy);
int bolas (int bolillero[BOLASTOTAL]);
int sacarbolas🥚🥚(int Grupo, int bolillero[BOLASTOTAL]);
int randrange (int limif, int limsp);
int numerar_carton (int Grupo, int num_carton[BOLASTOTAL]);


int main(){

	int jugando = 1;
	int carton[dim][dim];
	int bolillero[BOLASTOTAL + 1] = {0};
	int num_carton[BOLASTOTAL + 1] = {0};
	srand(time(NULL));
	

	//while (jugando == 1)
//	{
		llenar_carton(carton, num_carton);
		showcarton(carton, 45, 8);

		jugando = 0;
		printf("\n\nprueba");
//	}



	return 0;
}

void showcarton (int carton[dim][dim], int posx, int posy)
{
	int fil;
	int col;
	int dx;
	int dy;


	for (fil = 0; fil < dim; fil++)
	{
	
		for (col = 0; col < dim; col++)
		{
			setcolor(CT, CF);

         for (dy = 0; dy < ALTO_CASILLA; dy++)
			{
				gotoxy(posx + col * ANCHO_CASILLA, posy + fil * ALTO_CASILLA + dy);
				for (dx = 0; dx < ANCHO_CASILLA; dx++)
				{
					printf(" ");
				}
			}
			int textoy = posy + fil * ALTO_CASILLA + (ALTO_CASILLA - 1) / 2;
			int textox = posx + col * ANCHO_CASILLA + 1;
			gotoxy(textox, textoy);
			printf("%2d", carton[col][fil]);
		}
		//printf("\n");
	}	
}




/*Recibe el arreglo del total de volas y me retorna de manera aleatoria el indice de una posicion que no ha salido antes*/

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

/**/
int sacarbolas🥚🥚 (int GRUPO, int bolillero[BOLASTOTAL])
{
	int indice;
	int indice_temp;

	do
	{
		indice_temp = bolas(bolillero);
		indice = indice_temp;
		indice_temp = (((indice_temp - 1) / 15) + 1);
	} while (indice_temp != GRUPO);	

	return indice;
}

int numerar_carton (int Grupo, int num_carton[BOLASTOTAL])
{
	int indice;
	int indice_temp;
	do
	{
		indice_temp =  randrange(1, BOLASTOTAL);
		indice = indice_temp;
		indice_temp = (((indice_temp - 1) / 15) + 1);
		if (indice_temp == Grupo && num_carton[indice] == 0)
		{
			num_carton[indice] = 1;
			return indice;
		}
		
	} while (num_carton[indice] == 1 || indice_temp != Grupo);
}


void setcolor(int ct, int cf)
{
   textbackground(cf);
   textcolor(ct);
}


void colordefault(void)
{
   setcolor(LIGHTGRAY,BLACK);
}




void llenar_carton (int carton[dim][dim], int num_carton[BOLASTOTAL])
{
	int col;
	int fil;

	for (col = 0; col < dim; col++)
	{
		for (fil = 0; fil < dim; fil++)
		{	
			if (col == 0) carton[col][fil] = numerar_carton(B, num_carton);
			if (col == 1) carton[col][fil] = numerar_carton(I, num_carton);
			if (col == 2) carton[col][fil] = numerar_carton(N, num_carton);
			if (col == 3) carton[col][fil] = numerar_carton(G, num_carton);
			if (col == 4) carton[col][fil] = numerar_carton(O, num_carton);
		}		
	}
}