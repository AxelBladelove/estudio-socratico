/* A conio implementation for Mingw/Dev-C++.
 *
 * Written by:
 * Hongli Lai <hongli@telekabel.nl>
 * tkorrovi <tkorrovi@altavista.net> on 2002/02/26.
 * Andrew Westcott <ajwestco@users.sourceforge.net>
 *
 * Offered for use in the public domain without any warranty.
 */

#ifndef _CONIO_H_
#define _CONIO_H_

#include <stdio.h>
#include <windows.h>

#ifdef __cplusplus
extern "C" {
#endif

#define BLINK 0

typedef enum
{
    BLACK,
    BLUE,
    GREEN,
    CYAN,
    RED,
    MAGENTA,
    BROWN,
    LIGHTGRAY,
    DARKGRAY,
    LIGHTBLUE,
    LIGHTGREEN,
    LIGHTCYAN,
    LIGHTRED,
    LIGHTMAGENTA,
    YELLOW,
    WHITE
} COLORS;


#define cgets _cgets
#define cprintf _cprintf
#define cputs _cputs
#define cscanf _cscanf
#define ScreenClear clrscr

void clreol(void);
void clrscr(void);

int _conio_gettext(int left, int top, int right, int bottom,
                   char *str);

void delline(void);

void gotoxy(int x, int y);
void gotoxy(int x, int y);

void puttext(int left, int top, int right, int bottom, char *str);

void _setcursortype(int type);

void textattr(int _attr);

void textbackground(int color);

void textcolor(int color);

int wherex(void);

int wherey(void);

char *_cgets(char *);
int _cprintf(const char *, ...);
int _cputs(const char *);
int _cscanf(char *, ...);

int _getch(void);
int _getche(void);
int _kbhit(void);
int _putch(int);
int _ungetch(int);


int getch(void);
int getche(void);
int kbhit(void);
int putch(int);
int ungetch(int);


#ifdef __cplusplus
}
#endif

static inline HANDLE estudio_conio_stdout(void)
{
    return GetStdHandle(STD_OUTPUT_HANDLE);
}

#include "../soporte/consola/console_cp437.h"

#define printf estudio_printf

#endif /* _CONIO_H_ */
