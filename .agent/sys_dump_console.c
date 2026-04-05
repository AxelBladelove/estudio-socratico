#include <windows.h>
#include <stdio.h>
#include <stdlib.h>

int main(int argc, char *argv[]) {
    if (argc < 2) {
        return 1;
    }

    FILE *fp = fopen(argv[1], "a");
    if (!fp) return 1;

    HANDLE hConsole = GetStdHandle(STD_OUTPUT_HANDLE);
    CONSOLE_SCREEN_BUFFER_INFO csbi;
    if (GetConsoleScreenBufferInfo(hConsole, &csbi)) {
        COORD coord = {0, 0};
        DWORD charsRead;
        char buffer[2048]; // Max console width
        
        fprintf(fp, "\n[EJECUCION DEL USUARIO]\n");

        for (int y = 0; y < csbi.dwCursorPosition.Y; y++) {
            coord.Y = y;
            if (ReadConsoleOutputCharacterA(hConsole, buffer, csbi.dwSize.X, coord, &charsRead)) {
                // Trim trailing spaces for cleaner log
                int lastChar = charsRead - 1;
                while (lastChar >= 0 && buffer[lastChar] == ' ') {
                    lastChar--;
                }
                buffer[lastChar + 1] = '\0';

                fprintf(fp, "%s\n", buffer);
            }
        }
        
        fprintf(fp, "\n------------------------------------------------------------\n");
    }
    
    fclose(fp);
    return 0;
}
