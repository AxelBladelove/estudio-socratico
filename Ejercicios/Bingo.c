#include <stdio.h>
#include <stdlib.h>
#include <conio.h>
#include <time.h>

#define dim 5
#define ENTER 13
#define ESC 27
#define ARRIBA 72
#define ABAJO 80
#define DERECHA 77
#define IZQUIERDA 75
#define ESP 32
#define CT LIGHTBLUE
#define CF WHITE
#define colortext RED
#define CMT CYAN
#define CMF DARKGRAY
#define CCSF YELLOW
#define ANCHO_CASILLA 4
#define ALTO_CASILLA 1

#define BOLASTOTAL 75
#define B 1
#define I 2
#define N 3
#define G 4
#define O 5
#define bingoposY 7
#define POS_B 27
#define POS_I 31
#define POS_N 35
#define POS_G 39
#define POS_O 43
#define POS_CARTONX 25
#define POS_CARTONY 8
#define POS_BOLAX 65
#define POS_BOLAY 7
#define BOLA_TEXTO_DX 1
#define BOLA_TEXTO_DY 2
#define POS_MENSAJEX 3
#define POS_MENSAJESUPY 2
#define POS_MENSAJEINFY 15

int buscarbola (int carton[dim][dim], int bola);
void setcolor(int ct, int cf);
void colordefault(void);
void llenar_carton (int carton[dim][dim], int num_carton[BOLASTOTAL]);
void LetrasBingo (void);
void showcasilla (int carton[dim][dim], int marcadas[dim][dim], int col, int fil, int col_actual, int fil_actual, int posx, int posy);
void showcarton (int carton[dim][dim], int marcadas[dim][dim], int col_actual, int fil_actual, int posx, int posy);
int bolas (int bolillero[BOLASTOTAL]);
int randrange (int limif, int limsp);
int numerar_carton (int Grupo, int num_carton[BOLASTOTAL]);
void iniciar_carton (int marcadas[dim][dim]);
void mostrarbola (int bola);
int verificarlinea (int marcadas[dim][dim]);
int verificarlleno (int marcadas[dim][dim]);
void clearxy(int posx,int posy,int ancho,int largo);
int pedir_apuesta(void);
void mostrar_apuesta(int apuesta, int descuento_total);

int main()
{
   int jugando = 1;
   int carton[dim][dim];
   int bolillero[BOLASTOTAL + 1] = {0};
   int num_carton[BOLASTOTAL + 1] = {0};
   int marcadas[dim][dim] = {0};
   int bola;
   int fil_actual = -1;
   int col_actual = -1;
   int prejuego = 1;
   int tecla;
   int ganadas = 0;
   int perdidas = 0;
   int apuesta_mano;
   int descuento_total = 0;
   int descuento;
   int seguir_mano;
   int linea_hecha;
   int ir_lleno;
   int bolas_sacadas;
   int col_anterior;
   int fil_anterior;
   int ind;


   srand(time(NULL));
   _setcursortype(0);

   while (jugando == 1)
   {
      prejuego = 1;
      linea_hecha = 0;
      ir_lleno = 0;
      bolas_sacadas = 0;
      col_actual = -1;
      fil_actual = -1;
      apuesta_mano = 0;
      descuento = 0;

      for (ind = 0; ind <= BOLASTOTAL; ind++)
      {
         bolillero[ind] = 0;
         num_carton[ind] = 0;
      }

      llenar_carton(carton, num_carton);
      iniciar_carton(marcadas);
      showcarton(carton, marcadas, col_actual, fil_actual, POS_CARTONX, POS_CARTONY);
      clearxy(POS_BOLAX - 2, POS_BOLAY, 12, 5);
      clearxy(POS_MENSAJEX, POS_MENSAJESUPY, 40, 4);
      clearxy(POS_MENSAJEX, POS_MENSAJEINFY, 40, 5);
      colordefault();
      apuesta_mano = pedir_apuesta();
      gotoxy(POS_MENSAJEX, POS_MENSAJESUPY);
      printf("ESP: cambiar de carton");
      gotoxy(POS_MENSAJEX, POS_MENSAJESUPY + 1);
      printf("ENTER: comenzar");
      gotoxy(POS_MENSAJEX, POS_MENSAJESUPY + 2);
      printf("X: salir");
      gotoxy(POS_MENSAJEX, POS_MENSAJESUPY + 3);
      printf("Apuesta: %d", apuesta_mano);
      gotoxy(POS_MENSAJEX, POS_MENSAJEINFY);
      printf("Ganadas: %d", ganadas);
      gotoxy(POS_MENSAJEX, POS_MENSAJEINFY + 1);
      printf("Perdidas: %d", perdidas);
      gotoxy(POS_MENSAJEX, POS_MENSAJEINFY + 2);
      printf("Centro: Free");

      while (prejuego == 1)
      {
         tecla = getch();

         if (tecla == 'x' || tecla == 'X')
         {
            prejuego = 0;
            jugando = 0;
         }

         if (tecla == ESP)
         {
            for (ind = 0; ind <= BOLASTOTAL; ind++)
            {
               num_carton[ind] = 0;
            }
            llenar_carton(carton, num_carton);
            iniciar_carton(marcadas);
            col_actual = -1;
            fil_actual = -1;
            showcarton(carton, marcadas, col_actual, fil_actual, POS_CARTONX, POS_CARTONY);
         }

         if (tecla == ENTER)
         {
            prejuego = 0;
         }
      }

      if (jugando == 0)
      {
         break;
      }

      clearxy(POS_MENSAJEX, POS_MENSAJESUPY, 40, 4);
      clearxy(POS_MENSAJEX, POS_MENSAJEINFY, 40, 5);
      gotoxy(POS_MENSAJEX, POS_MENSAJESUPY);
      printf("Flechas: mover");
      gotoxy(POS_MENSAJEX, POS_MENSAJESUPY + 1);
      printf("ENTER: marcar");
      gotoxy(POS_MENSAJEX, POS_MENSAJESUPY + 2);
      printf("ESC: no esta");
      gotoxy(POS_MENSAJEX, POS_MENSAJEINFY);
      printf("Ganadas: %d", ganadas);
      gotoxy(POS_MENSAJEX, POS_MENSAJEINFY + 1);
      printf("Perdidas: %d", perdidas);
      gotoxy(POS_MENSAJEX, POS_MENSAJEINFY + 2);
      printf("X: salir");
      mostrar_apuesta(apuesta_mano, descuento_total);

      seguir_mano = 1;
      while (seguir_mano == 1)
      {
         if (bolas_sacadas == BOLASTOTAL)
         {
            if (linea_hecha == 0)
            {
               perdidas++;
            }
            seguir_mano = 0;
         }
         else
         {
            bola = bolas(bolillero);
            bolas_sacadas++;
            mostrarbola(bola);
            gotoxy(POS_MENSAJEX, POS_MENSAJEINFY);
            printf("Ganadas: %d", ganadas);
            gotoxy(POS_MENSAJEX, POS_MENSAJEINFY + 1);
            printf("Perdidas: %d", perdidas);
            gotoxy(POS_MENSAJEX, POS_MENSAJEINFY + 2);
            printf("X: salir");
            mostrar_apuesta(apuesta_mano, descuento_total);

            prejuego = 1;
            while (prejuego == 1 && seguir_mano == 1)
            {
               tecla = getch();
               col_anterior = col_actual;
               fil_anterior = fil_actual;

               if (tecla == 'x' || tecla == 'X')
               {
                  prejuego = 0;
                  seguir_mano = 0;
                  jugando = 0;
               }

               if (tecla == ARRIBA || tecla == ABAJO || tecla == IZQUIERDA || tecla == DERECHA)
               {
                  if (col_actual == -1 || fil_actual == -1)
                  {
                     col_actual = 0;
                     fil_actual = 0;
                     if (tecla == DERECHA && col_actual < dim - 1) col_actual++;
                     if (tecla == ABAJO && fil_actual < dim - 1) fil_actual++;
                     showcasilla(carton, marcadas, col_actual, fil_actual, col_actual, fil_actual, POS_CARTONX, POS_CARTONY);
                  }
                  else
                  {
                     if (tecla == ARRIBA && fil_actual > 0) fil_actual--;
                     if (tecla == ABAJO && fil_actual < dim - 1) fil_actual++;
                     if (tecla == IZQUIERDA && col_actual > 0) col_actual--;
                     if (tecla == DERECHA && col_actual < dim - 1) col_actual++;

                     if (col_anterior != col_actual || fil_anterior != fil_actual)
                     {
                        showcasilla(carton, marcadas, col_anterior, fil_anterior, col_actual, fil_actual, POS_CARTONX, POS_CARTONY);
                        showcasilla(carton, marcadas, col_actual, fil_actual, col_actual, fil_actual, POS_CARTONX, POS_CARTONY);
                     }
                  }
               }

               if (tecla == ENTER && col_actual != -1 && fil_actual != -1)
               {
                  prejuego = 0;
                  if (carton[col_actual][fil_actual] == bola)
                  {
                     marcadas[col_actual][fil_actual] = 1;
                     showcasilla(carton, marcadas, col_actual, fil_actual, col_actual, fil_actual, POS_CARTONX, POS_CARTONY);

                     if (linea_hecha == 0 && verificarlinea(marcadas))
                     {
                        ganadas++;
                        linea_hecha = 1;
                        gotoxy(POS_MENSAJEX, POS_MENSAJEINFY + 2);
                        printf("ENTER: carton lleno  ESC: cerrar");

                        do
                        {
                           gotoxy(POS_MENSAJEX, POS_MENSAJEINFY);
                           printf("Ganadas: %d", ganadas);
                           gotoxy(POS_MENSAJEX, POS_MENSAJEINFY + 1);
                           printf("Perdidas: %d", perdidas);
                           tecla = getch();
                        } while (tecla != ENTER && tecla != ESC && tecla != 'x' && tecla != 'X');

                        gotoxy(POS_MENSAJEX, POS_MENSAJEINFY + 2);
                        printf("X: salir                         ");

                        if (tecla == ENTER)
                        {
                           ir_lleno = 1;
                        }
                        else if (tecla == 'x' || tecla == 'X')
                        {
                           seguir_mano = 0;
                           jugando = 0;
                        }
                        else
                        {
                           seguir_mano = 0;
                        }
                     }

                     if (ir_lleno == 1 && verificarlleno(marcadas))
                     {
                        seguir_mano = 0;
                     }
                  }
               }

               if (tecla == ESC)
               {
                  prejuego = 0;
                  if (buscarbola(carton, bola) == 1)
                  {
                     descuento = apuesta_mano / 10;
                     apuesta_mano -= descuento;
                     if (apuesta_mano < 0)
                     {
                        apuesta_mano = 0;
                     }
                     descuento_total += descuento;
                     mostrar_apuesta(apuesta_mano, descuento_total);
                     perdidas++;
                  }
               }
            }
         }
      }

      if (jugando == 0)
      {
         break;
      }

      clearxy(POS_MENSAJEX, POS_MENSAJEINFY, 40, 5);
      gotoxy(POS_MENSAJEX, POS_MENSAJEINFY);
      printf("ENTER: jugar otra mano");
      gotoxy(POS_MENSAJEX, POS_MENSAJEINFY + 1);
      printf("Ganadas: %d", ganadas);
      gotoxy(POS_MENSAJEX, POS_MENSAJEINFY + 2);
      printf("X: salir");
      mostrar_apuesta(apuesta_mano, descuento_total);

      do
      {
         tecla = getch();
      } while (tecla != ENTER && tecla != 'x' && tecla != 'X');

      if (tecla == 'x' || tecla == 'X')
      {
         jugando = 0;
      }
   }

   _setcursortype(100);
   colordefault();
   return 0;
}

/*
   Funcion: showcasilla
   Argumentos: int carton[dim][dim], int marcadas[dim][dim], int col,
               int fil, int col_actual, int fil_actual, int posx,
               int posy. Recibe el carton, el estado de marcadas,
               la casilla a dibujar, la casilla seleccionada y la
               posicion base del carton.
   Objetivo: Dibujar una casilla individual del carton con el color
             que le corresponda segun su estado actual.
   Retorno: Ninguno.
*/
void showcasilla (int carton[dim][dim], int marcadas[dim][dim], int col, int fil, int col_actual, int fil_actual, int posx, int posy)
{
   int dx;
   int dy;

   if (marcadas[col][fil] == 1 && col == col_actual && fil == fil_actual && col_actual != -1 && fil_actual != -1)
   {
      setcolor(CCSF, CF);
   }
   else if (marcadas[col][fil] == 1)
   {
      setcolor(CMT, CMF);
   }
   else if (col == col_actual && fil == fil_actual && col_actual != -1 && fil_actual != -1)
   {
      setcolor(CT, CCSF);
   }
   else
   {
      setcolor(CT, CF);
   }

   for (dy = 0; dy < ALTO_CASILLA; dy++)
   {
      gotoxy(posx + col * ANCHO_CASILLA, posy + fil * ALTO_CASILLA + dy);
      for (dx = 0; dx < ANCHO_CASILLA; dx++)
      {
         printf(" ");
      }
   }

   if (col == 2 && fil == 2)
   {
      gotoxy(posx + col * ANCHO_CASILLA, posy + fil * ALTO_CASILLA);
      printf("Free");
   }
   else
   {
      gotoxy(posx + col * ANCHO_CASILLA + 1, posy + fil * ALTO_CASILLA);
      printf("%2d", carton[col][fil]);
   }
}

/*
   Funcion: showcarton
   Argumentos: int carton[dim][dim], int marcadas[dim][dim],
               int col_actual, int fil_actual, int posx, int posy.
               Recibe el contenido del carton, sus casillas marcadas,
               la seleccion actual y la posicion base del dibujo.
   Objetivo: Mostrar el carton completo junto con sus rotulos.
   Retorno: Ninguno.
*/
void showcarton (int carton[dim][dim], int marcadas[dim][dim], int col_actual, int fil_actual, int posx, int posy)
{
   int fil;
   int col;

   LetrasBingo();

   for (fil = 0; fil < dim; fil++)
   {
      for (col = 0; col < dim; col++)
      {
         showcasilla(carton, marcadas, col, fil, col_actual, fil_actual, posx, posy);
      }
   }
   colordefault();
}

/*
   Funcion: bolas
   Argumentos: int bolillero[BOLASTOTAL]. Arreglo que registra las
               bolas que ya han salido.
   Objetivo: Sacar una bola aleatoria que aun no haya salido y
             marcarla como usada.
   Retorno: Indice entero de la bola sacada.
*/
int bolas (int bolillero[BOLASTOTAL])
{
   int indice;

   do
   {
      indice = randrange(1, BOLASTOTAL);
   } while (bolillero[indice] == 1);

   bolillero[indice] = 1;
   return indice;
}

/*
   Funcion: randrange
   Argumentos: int limif, int limsp. Limites inferior y superior del
               rango.
   Objetivo: Generar un entero aleatorio dentro del rango indicado.
   Retorno: Valor entero entre limif y limsp.
*/
int randrange(int limif,int limsp)
{
   return (rand() % (limsp - limif + 1) + limif);
}

/*
   Funcion: numerar_carton
   Argumentos: int Grupo, int num_carton[BOLASTOTAL]. Recibe el grupo
               de bingo buscado y el arreglo que controla repetidos.
   Objetivo: Generar un numero aleatorio valido para la columna indicada
             sin repetirlo dentro del carton.
   Retorno: Numero entero asignado a la casilla.
*/
int numerar_carton (int Grupo, int num_carton[BOLASTOTAL])
{
   int indice;
   int indice_temp;

   do
   {
      indice_temp = randrange(1, BOLASTOTAL);
      indice = indice_temp;
      indice_temp = (((indice_temp - 1) / 15) + 1);
      if (indice_temp == Grupo && num_carton[indice] == 0)
      {
         num_carton[indice] = 1;
         return indice;
      }
   } while (num_carton[indice] == 1 || indice_temp != Grupo);

   return indice;
}

/*
   Funcion: setcolor
   Argumentos: int ct, int cf. Colores de texto y fondo.
   Objetivo: Configurar los colores actuales de impresion en consola.
   Retorno: Ninguno.
*/
void setcolor(int ct, int cf)
{
   textbackground(cf);
   textcolor(ct);
}

/*
   Funcion: colordefault
   Argumentos: Ninguno.
   Objetivo: Restaurar el color por defecto usado por el programa.
   Retorno: Ninguno.
*/
void colordefault(void)
{
   setcolor(LIGHTGRAY,BLACK);
}

/*
   Funcion: pedir_apuesta
   Argumentos: Ninguno.
   Objetivo: Solicitar al usuario una apuesta valida para la mano
             actual.
   Retorno: Valor entero positivo correspondiente a la apuesta.
*/
int pedir_apuesta(void)
{
   int apuesta;
   int leidos;
   int c; 

   do
   {
      clearxy(POS_MENSAJEX, POS_MENSAJESUPY, 40, 4);
      gotoxy(POS_MENSAJEX, POS_MENSAJESUPY);
      _setcursortype(1);
      printf("Ingrese la apuesta de la mano: ");

      
      leidos = scanf("%d", &apuesta);

      do 
      {
         c = getchar();
      } while (c != '\n' && c != EOF);

   } while (leidos != 1 || apuesta <= 0);

   clearxy(POS_MENSAJEX, POS_MENSAJESUPY, 40, 4);
   _setcursortype(0);

   return apuesta;
}

/*
   Funcion: mostrar_apuesta
   Argumentos: int apuesta, int descuento_total. Recibe la apuesta
               restante de la mano y el descuento acumulado.
   Objetivo: Mostrar en pantalla la informacion actual de la apuesta.
   Retorno: Ninguno.
*/
void mostrar_apuesta(int apuesta, int descuento_total)
{
   gotoxy(POS_MENSAJEX, POS_MENSAJEINFY + 3);
   printf("Apuesta: %d                ", apuesta);
   gotoxy(POS_MENSAJEX, POS_MENSAJEINFY + 4);
   printf("Descuento acum.: %d        ", descuento_total);
}

/*
   Funcion: llenar_carton
   Argumentos: int carton[dim][dim], int num_carton[BOLASTOTAL].
               Recibe la matriz del carton y el arreglo de control
               para numeros repetidos.
   Objetivo: Llenar el carton con numeros validos por columna y dejar
             libre la casilla central.
   Retorno: Ninguno.
*/
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

   carton[2][2] = 0;
}

/*
   Funcion: LetrasBingo
   Argumentos: Ninguno.
   Objetivo: Dibujar los rotulos B I N G O sobre el carton.
   Retorno: Ninguno.
*/
void LetrasBingo (void)
{
   int dx;

   setcolor(CF, colortext);

   gotoxy(POS_B - 2, bingoposY);
   for (dx = 0; dx < (ANCHO_CASILLA * 5); dx++) printf(" ");

   gotoxy(POS_B, bingoposY);
   printf("B");
   gotoxy(POS_I, bingoposY);
   printf("I");
   gotoxy(POS_N, bingoposY);
   printf("N");
   gotoxy(POS_G, bingoposY);
   printf("G");
   gotoxy(POS_O, bingoposY);
   printf("O");
   colordefault();
}

/*
   Funcion: buscarbola
   Argumentos: int carton[dim][dim], int bola. Recibe el carton y la
               bola a buscar.
   Objetivo: Determinar si la bola indicada se encuentra dentro del
             carton actual.
   Retorno: 1 si la bola existe en el carton, 0 en caso contrario.
*/
int buscarbola (int carton[dim][dim], int bola)
{
   int fil;
   int col;

   for (col = 0; col < dim; col++)
   {
      for (fil = 0; fil < dim; fil++)
      {
         if (carton[col][fil] == bola)
         {
            return 1;
         }
      }
   }

   return 0;
}

/*
   Funcion: iniciar_carton
   Argumentos: int marcadas[dim][dim]. Matriz que indica las casillas
               marcadas.
   Objetivo: Reiniciar el estado de marcas del carton y activar la
             casilla central libre.
   Retorno: Ninguno.
*/
void iniciar_carton(int marcadas[dim][dim])
{
   int col;
   int fil;

   for (col = 0; col < dim; col++)
   {
      for (fil = 0; fil < dim; fil++)
      {
         marcadas[col][fil] = 0;

         if (col == 2 && fil == 2)
         {
            marcadas[col][fil] = 1;
         }
      }
   }
}

/*
   Funcion: mostrarbola
   Argumentos: int bola. Numero de la bola que se desea presentar.
   Objetivo: Dibujar en pantalla la bola actual con su letra y numero.
   Retorno: Ninguno.
*/
void mostrarbola (int bola)
{
   int grupo;
   int color;

   grupo = (((bola - 1) / 15) + 1);
   color = randrange(1,4);

   if (color == 1) setcolor(CT, CCSF);
   if (color == 2) setcolor(CMT, CMF);
   if (color == 3) setcolor(CF, colortext);
   if (color == 4) setcolor(colortext, CF);

   gotoxy(POS_BOLAX, POS_BOLAY);
   printf("      ");
   gotoxy(POS_BOLAX - 1, POS_BOLAY + 1);
   printf("        ");
   gotoxy(POS_BOLAX - 2, POS_BOLAY + 2);
   printf("          ");
   gotoxy(POS_BOLAX - 1, POS_BOLAY + 3);
   printf("        ");
   gotoxy(POS_BOLAX, POS_BOLAY + 4);
   printf("      ");

   gotoxy(POS_BOLAX + BOLA_TEXTO_DX, POS_BOLAY + BOLA_TEXTO_DY);
   if (grupo == B) printf("B-%2d", bola);
   if (grupo == I) printf("I-%2d", bola);
   if (grupo == N) printf("N-%2d", bola);
   if (grupo == G) printf("G-%2d", bola);
   if (grupo == O) printf("O-%2d", bola);
   colordefault();
}

/*
   Funcion: verificarlinea
   Argumentos: int marcadas[dim][dim]. Matriz con las casillas ya
               marcadas.
   Objetivo: Comprobar si existe una linea completa horizontal,
             vertical o diagonal.
   Retorno: 1 si existe una linea ganadora, 0 en caso contrario.
*/
int verificarlinea (int marcadas[dim][dim])
{
   int fil;
   int col;
   int si;

   for (fil = 0; fil < dim; fil++)
   {
      si = 1;
      for (col = 0; col < dim; col++)
      {
         if (marcadas[col][fil] == 0)
         {
            si = 0;
         }
      }
      if (si == 1) return 1;
   }

   for (col = 0; col < dim; col++)
   {
      si = 1;
      for (fil = 0; fil < dim; fil++)
      {
         if (marcadas[col][fil] == 0)
         {
            si = 0;
         }
      }
      if (si == 1) return 1;
   }

   si = 1;
   for (col = 0; col < dim; col++)
   {
      if (marcadas[col][col] == 0)
      {
         si = 0;
      }
   }
   if (si == 1) return 1;

   si = 1;
   for (col = 0; col < dim; col++)
   {
      if (marcadas[col][dim - 1 - col] == 0)
      {
         si = 0;
      }
   }
   if (si == 1) return 1;

   return 0;
}

/*
   Funcion: verificarlleno
   Argumentos: int marcadas[dim][dim]. Matriz con el estado de las
               casillas del carton.
   Objetivo: Verificar si todo el carton se encuentra marcado.
   Retorno: 1 si el carton esta lleno, 0 en caso contrario.
*/
int verificarlleno (int marcadas[dim][dim])
{
   int fil;
   int col;

   for (col = 0; col < dim; col++)
   {
      for (fil = 0; fil < dim; fil++)
      {
         if (marcadas[col][fil] == 0)
         {
            return 0;
         }
      }
   }

   return 1;
}

/*
   Funcion: clearxy
   Argumentos: int posx, int posy, int ancho, int largo. Definen la
               posicion y el tamano del area a limpiar.
   Objetivo: Borrar un rectangulo de la consola pintandolo de negro.
   Retorno: Ninguno.
*/
void clearxy(int posx,int posy,int ancho,int largo)
{
   int indfil;
   int indcol;

   setcolor(BLACK,BLACK);
   for (indfil = 0; indfil < largo; indfil++)
   {
      for (indcol = 0; indcol < ancho; indcol++)
      {
         gotoxy(posx + indcol, posy + indfil);
         printf(" ");
      }
   }
   colordefault();
}