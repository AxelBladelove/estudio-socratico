#ifndef ESTUDIO_SOCRATICO_CONIO_H
#define ESTUDIO_SOCRATICO_CONIO_H

/*
   Compatibilidad minima de conio.h para ejercicios de consola en Windows.
   Permite usar gotoxy, clrscr, getch y algunas funciones comunes de Turbo C
   compilando con GCC/MinGW.
*/

#ifdef _WIN32

#include <stdio.h>
#include <windows.h>

#ifndef BLACK
#define BLACK 0
#define BLUE 1
#define GREEN 2
#define CYAN 3
#define RED 4
#define MAGENTA 5
#define BROWN 6
#define LIGHTGRAY 7
#define DARKGRAY 8
#define LIGHTBLUE 9
#define LIGHTGREEN 10
#define LIGHTCYAN 11
#define LIGHTRED 12
#define LIGHTMAGENTA 13
#define YELLOW 14
#define WHITE 15
#endif

int _getch(void);
int _getche(void);
int _kbhit(void);

static inline HANDLE estudio_conio_stdout(void)
{
    return GetStdHandle(STD_OUTPUT_HANDLE);
}

static inline WORD *estudio_conio_default_attr(void)
{
    static WORD attr = 7;
    return &attr;
}

static inline WORD *estudio_conio_current_attr(void)
{
    static WORD attr = 7;
    return &attr;
}

static inline void estudio_conio_init_attr(void)
{
    static int initialized = 0;
    CONSOLE_SCREEN_BUFFER_INFO info;

    if (!initialized) {
        if (GetConsoleScreenBufferInfo(estudio_conio_stdout(), &info)) {
            *estudio_conio_default_attr() = info.wAttributes;
            *estudio_conio_current_attr() = info.wAttributes;
        }
        initialized = 1;
    }
}

static inline void gotoxy(int x, int y)
{
    COORD position;

    if (x < 1) {
        x = 1;
    }
    if (y < 1) {
        y = 1;
    }

    position.X = (SHORT)(x - 1);
    position.Y = (SHORT)(y - 1);
    fflush(stdout);
    SetConsoleCursorPosition(estudio_conio_stdout(), position);
}

static inline int wherex(void)
{
    CONSOLE_SCREEN_BUFFER_INFO info;

    if (GetConsoleScreenBufferInfo(estudio_conio_stdout(), &info)) {
        return info.dwCursorPosition.X + 1;
    }
    return 1;
}

static inline int wherey(void)
{
    CONSOLE_SCREEN_BUFFER_INFO info;

    if (GetConsoleScreenBufferInfo(estudio_conio_stdout(), &info)) {
        return info.dwCursorPosition.Y + 1;
    }
    return 1;
}

static inline void clrscr(void)
{
    HANDLE out = estudio_conio_stdout();
    CONSOLE_SCREEN_BUFFER_INFO info;
    DWORD cells;
    DWORD written;
    COORD home = {0, 0};

    if (!GetConsoleScreenBufferInfo(out, &info)) {
        return;
    }

    cells = (DWORD)info.dwSize.X * (DWORD)info.dwSize.Y;
    FillConsoleOutputCharacterA(out, ' ', cells, home, &written);
    FillConsoleOutputAttribute(out, info.wAttributes, cells, home, &written);
    SetConsoleCursorPosition(out, home);
}

static inline void textattr(int attr)
{
    estudio_conio_init_attr();
    *estudio_conio_current_attr() = (WORD)attr;
    SetConsoleTextAttribute(estudio_conio_stdout(), *estudio_conio_current_attr());
}

static inline void textcolor(int color)
{
    estudio_conio_init_attr();
    *estudio_conio_current_attr() =
        (WORD)((*estudio_conio_current_attr() & 0xF0) | (color & 0x0F));
    SetConsoleTextAttribute(estudio_conio_stdout(), *estudio_conio_current_attr());
}

static inline void textbackground(int color)
{
    estudio_conio_init_attr();
    *estudio_conio_current_attr() =
        (WORD)((*estudio_conio_current_attr() & 0x0F) | ((color & 0x0F) << 4));
    SetConsoleTextAttribute(estudio_conio_stdout(), *estudio_conio_current_attr());
}

static inline void normvideo(void)
{
    estudio_conio_init_attr();
    *estudio_conio_current_attr() = *estudio_conio_default_attr();
    SetConsoleTextAttribute(estudio_conio_stdout(), *estudio_conio_default_attr());
}

static inline int getch(void)
{
    return _getch();
}

static inline int getche(void)
{
    return _getche();
}

static inline int kbhit(void)
{
    return _kbhit();
}

#else

#error "Este conio.h local esta pensado para compilar en Windows con GCC/MinGW."

#endif

#endif
