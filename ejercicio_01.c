/* Ejercicio: Leer N numeros enteros usando memoria dinamica,
   e imprimir la suma de los que sean numeros primos. */

#include <stdio.h>
#include <stdlib.h>

int esPrimo(int n) {
    if (n < 2) return 0;
    for (int i = 2; i * i <= n; i++) {
        if (n % i == 0) return 0;
    }
    return 1;
}

int main() {
    int n;
    printf("Cuantos numeros vas a ingresar? ");
    scanf("%d", &n);

    int *numeros = (int *)malloc(n * sizeof(int));
    if (numeros == NULL) {
        printf("Error: no se pudo reservar memoria.\n");
        return 1;
    }

    printf("Ingresa %d numeros:\n", n);
    for (int i = 0; i < n; i++) {
        scanf("%d", &numeros[i]);
    }

    int suma = 0;
    for (int i = 0; i < n; i++) {
        if (esPrimo(numeros[i])) {
            suma += numeros[i];
        }
    }

    printf("Suma de los primos: %d\n", suma);

    free(numeros);
    return 0;
}
