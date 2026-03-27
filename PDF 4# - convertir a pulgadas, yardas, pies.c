/*Haga un programa al cual se le digite una longitud expresada en 
pulgadas e imprima la misma longitud en yardas, pies y 
pulgadas.  Por ejemplo, una longitud de 65 pulgadas sería 
expresada como 1 yarda, 2 pies y 5 pulgadas. */ 

int main(){

// lo primero es crear las variables donde vamos a almacenar los datos
// luego pedirle al usurario cada dato y almacenarlos en sus respesctivas variables

int pulgadas = 0;
int yardas = 0;
int pies = 0;

printf("Digite la cantidad de pulgadas que quieres convertir: ");
scanf("%d", &pulgadas);

//hay que imprimir la cantidad final
printf("%d Yardas, %d pies y %d pulgadas\n", yardas, pies, pulgadas);


}