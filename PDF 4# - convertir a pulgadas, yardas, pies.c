/*Haga un programa al cual se le digite una longitud expresada en 
pulgadas e imprima la misma longitud en yardas, pies y 
pulgadas.  Por ejemplo, una longitud de 65 pulgadas sería 
expresada como 1 yarda, 2 pies y 5 pulgadas. */ 
#include <stdio.h>
int main(){

// lo primero es crear las variables donde vamos a almacenar los datos
// luego pedirle al usurario cada dato y almacenarlos en sus respesctivas variables

int pulgadas = 0;
int yardas = 0;
int pies = 0;

printf("Digite la cantidad de pulgadas que quieres convertir: ");
scanf("%d", &pulgadas);

// tengo que convertir ahora los datosd introducidos en pulgadas, a yardas y a pies. hagamoslo en orden
// al dividir la cantidad de pulgadas que nos dieron, entre lo que vale 1 pie, obtenemos la cantidad de pies 
// que representa esa cantidad de pulgadas
pies = pulgadas / 12;

yardas = pies / 3; 
// lo mismo pasa si lo hacemos con las yardas y los pies

//hay que imprimir la cantidad final
printf("%d Yardas, %d pies y %d pulgadas\n", yardas, pies, pulgadas);


}