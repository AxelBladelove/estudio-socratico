/* A conio implementation for Mingw/Dev-C++.
 *
 * Written by:
 * Hongli Lai <hongli@telekabel.nl>
 * tkorrovi <tkorrovi@altavista.net> on 2002/02/26.
 * Andrew Westcott <ajwestco@users.sourceforge.net>
 *
 * Offered for use in the public domain without any warranty.
 */

#ifndef _CONIO_C_
#define _CONIO_C_

#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <windows.h>
#include "conio.h"

#ifdef __cplusplus
extern "C" {
#endif

static int __BACKGROUND = BLACK;
static int __FOREGROUND = LIGHTGRAY;
static int __UNGETCH = -1;

static HANDLE conio_input(void)
{
    return GetStdHandle(STD_INPUT_HANDLE);
}

static HANDLE conio_output(void)
{
    return GetStdHandle(STD_OUTPUT_HANDLE);
}

static WORD conio_attr(void)
{
    return (WORD)(__FOREGROUND + (__BACKGROUND << 4));
}

static int conio_is_modifier_key(WORD virtual_key)
{
    switch (virtual_key) {
    case VK_SHIFT:
    case VK_CONTROL:
    case VK_MENU:
    case VK_LSHIFT:
    case VK_RSHIFT:
    case VK_LCONTROL:
    case VK_RCONTROL:
    case VK_LMENU:
    case VK_RMENU:
        return 1;
    default:
        return 0;
    }
}

void clrscr(void)
{
    HANDLE out = conio_output();
    CONSOLE_SCREEN_BUFFER_INFO info;
    DWORD written;
    DWORD cells;
    COORD origin = {0, 0};

    if (out == INVALID_HANDLE_VALUE || !GetConsoleScreenBufferInfo(out, &info)) {
        return;
    }

    cells = (DWORD)info.dwSize.X * (DWORD)info.dwSize.Y;
    FillConsoleOutputAttribute(out, conio_attr(), cells, origin, &written);
    FillConsoleOutputCharacter(out, ' ', cells, origin, &written);
    gotoxy(1, 1);
}

void clreol(void)
{
    HANDLE out = conio_output();
    CONSOLE_SCREEN_BUFFER_INFO info;
    DWORD written;
    DWORD cells;
    COORD coord;

    if (out == INVALID_HANDLE_VALUE || !GetConsoleScreenBufferInfo(out, &info)) {
        return;
    }

    coord = info.dwCursorPosition;
    cells = (DWORD)(info.dwSize.X - info.dwCursorPosition.X);
    FillConsoleOutputAttribute(out, conio_attr(), cells, coord, &written);
    FillConsoleOutputCharacter(out, ' ', cells, coord, &written);
    gotoxy(coord.X + 1, coord.Y + 1);
}

void delline(void)
{
    HANDLE out = conio_output();
    CONSOLE_SCREEN_BUFFER_INFO info;
    SMALL_RECT scroll;
    CHAR_INFO fill;
    COORD dest;

    if (out == INVALID_HANDLE_VALUE || !GetConsoleScreenBufferInfo(out, &info)) {
        return;
    }

    scroll.Left = 0;
    scroll.Right = info.dwSize.X - 1;
    scroll.Top = info.dwCursorPosition.Y + 1;
    scroll.Bottom = info.dwSize.Y - 1;
    dest.X = 0;
    dest.Y = info.dwCursorPosition.Y;
    fill.Char.UnicodeChar = L' ';
    fill.Attributes = conio_attr();

    ScrollConsoleScreenBuffer(out, &scroll, NULL, dest, &fill);
    gotoxy(1, info.dwCursorPosition.Y + 1);
}

int _conio_gettext(int left, int top, int right, int bottom, char *str)
{
    HANDLE out = conio_output();
    SMALL_RECT rect;
    COORD size;
    COORD origin = {0, 0};
    CHAR_INFO *buffer;
    int width;
    int height;
    int x;
    int y;
    int n = 0;

    if (str == NULL || left > right || top > bottom) {
        return 0;
    }

    width = right - left + 1;
    height = bottom - top + 1;
    buffer = (CHAR_INFO *)calloc((size_t)width * (size_t)height, sizeof(CHAR_INFO));
    if (buffer == NULL) {
        return 0;
    }

    rect = (SMALL_RECT){left - 1, top - 1, right - 1, bottom - 1};
    size = (COORD){(SHORT)width, (SHORT)height};

    if (!ReadConsoleOutput(out, buffer, size, origin, &rect)) {
        free(buffer);
        return 0;
    }

    for (y = 0; y < height; y++) {
        for (x = 0; x < width; x++) {
            WCHAR ch = buffer[(y * width) + x].Char.UnicodeChar;
            str[n++] = (ch <= 255) ? (char)ch : '?';
        }
    }
    str[n] = '\0';

    free(buffer);
    return 1;
}

void gotoxy(int x, int y)
{
    COORD coord;

    if (x < 1) {
        x = 1;
    }
    if (y < 1) {
        y = 1;
    }

    coord.X = (SHORT)(x - 1);
    coord.Y = (SHORT)(y - 1);
    SetConsoleCursorPosition(conio_output(), coord);
}

void puttext(int left, int top, int right, int bottom, char *str)
{
    HANDLE out = conio_output();
    SMALL_RECT rect;
    COORD size;
    COORD origin = {0, 0};
    CHAR_INFO *buffer;
    int width;
    int height;
    int x;
    int y;
    int n = 0;

    if (str == NULL || left > right || top > bottom) {
        return;
    }

    width = right - left + 1;
    height = bottom - top + 1;
    buffer = (CHAR_INFO *)calloc((size_t)width * (size_t)height, sizeof(CHAR_INFO));
    if (buffer == NULL) {
        return;
    }

    for (y = 0; y < height; y++) {
        for (x = 0; x < width; x++) {
            unsigned char byte = str[n] ? (unsigned char)str[n++] : ' ';
            buffer[(y * width) + x].Char.UnicodeChar = estudio_conio_map_cp437_byte(byte);
            buffer[(y * width) + x].Attributes = conio_attr();
        }
    }

    rect = (SMALL_RECT){left - 1, top - 1, right - 1, bottom - 1};
    size = (COORD){(SHORT)width, (SHORT)height};
    WriteConsoleOutput(out, buffer, size, origin, &rect);
    free(buffer);
}

void _setcursortype(int type)
{
    CONSOLE_CURSOR_INFO info;

    if (type <= 0) {
        info.bVisible = FALSE;
        info.dwSize = 100;
    } else {
        info.bVisible = TRUE;
        info.dwSize = (DWORD)type;
    }

    SetConsoleCursorInfo(conio_output(), &info);
}

void textattr(int attr)
{
    __FOREGROUND = attr & 0x0F;
    __BACKGROUND = (attr >> 4) & 0x0F;
    SetConsoleTextAttribute(conio_output(), (WORD)attr);
}

void textbackground(int color)
{
    __BACKGROUND = color & 0x0F;
    SetConsoleTextAttribute(conio_output(), conio_attr());
}

void textcolor(int color)
{
    __FOREGROUND = color & 0x0F;
    SetConsoleTextAttribute(conio_output(), conio_attr());
}

int wherex(void)
{
    CONSOLE_SCREEN_BUFFER_INFO info;

    if (!GetConsoleScreenBufferInfo(conio_output(), &info)) {
        return 1;
    }

    return info.dwCursorPosition.X + 1;
}

int wherey(void)
{
    CONSOLE_SCREEN_BUFFER_INFO info;

    if (!GetConsoleScreenBufferInfo(conio_output(), &info)) {
        return 1;
    }

    return info.dwCursorPosition.Y + 1;
}

int _putch(int character)
{
    unsigned char byte = (unsigned char)character;
    return estudio_conio_write_cp437((const char *)&byte, 1) == 1 ? character : EOF;
}

int putch(int character)
{
    return _putch(character);
}

int _cputs(const char *text)
{
    if (text == NULL) {
        return EOF;
    }

    return estudio_conio_write_cp437(text, strlen(text)) >= 0 ? 0 : EOF;
}

int _cprintf(const char *format, ...)
{
    va_list args;
    int written;

    va_start(args, format);
    written = estudio_vprintf(format, args);
    va_end(args);

    return written;
}

int _cscanf(char *format, ...)
{
    va_list args;
    int result;

    va_start(args, format);
    result = vscanf(format, args);
    va_end(args);

    return result;
}

int _ungetch(int character)
{
    if (__UNGETCH != -1 || character == EOF) {
        return EOF;
    }

    __UNGETCH = character & 0xFF;
    return character;
}

int ungetch(int character)
{
    return _ungetch(character);
}

int _kbhit(void)
{
    HANDLE input = conio_input();
    INPUT_RECORD record;
    DWORD events;
    DWORD read;

    if (__UNGETCH != -1) {
        return 1;
    }

    if (input == INVALID_HANDLE_VALUE || !GetNumberOfConsoleInputEvents(input, &events)) {
        return 0;
    }

    while (events > 0) {
        if (!PeekConsoleInput(input, &record, 1, &read) || read == 0) {
            return 0;
        }

        if (record.EventType == KEY_EVENT &&
            record.Event.KeyEvent.bKeyDown &&
            !conio_is_modifier_key(record.Event.KeyEvent.wVirtualKeyCode)) {
            return 1;
        }

        ReadConsoleInput(input, &record, 1, &read);
        events--;
    }

    return 0;
}

int kbhit(void)
{
    return _kbhit();
}

int _getch(void)
{
    HANDLE input = conio_input();
    INPUT_RECORD record;
    DWORD read;

    if (__UNGETCH != -1) {
        int character = __UNGETCH;
        __UNGETCH = -1;
        return character;
    }

    if (input == INVALID_HANDLE_VALUE) {
        return fgetc(stdin);
    }

    for (;;) {
        if (!ReadConsoleInput(input, &record, 1, &read)) {
            return EOF;
        }

        if (record.EventType == KEY_EVENT &&
            record.Event.KeyEvent.bKeyDown &&
            !conio_is_modifier_key(record.Event.KeyEvent.wVirtualKeyCode)) {
            CHAR ascii = record.Event.KeyEvent.uChar.AsciiChar;
            return ascii ? (unsigned char)ascii : 0;
        }
    }
}

int getch(void)
{
    return _getch();
}

int _getche(void)
{
    int character = _getch();

    if (character != EOF) {
        _putch(character);
    }

    return character;
}

int getche(void)
{
    return _getche();
}

char *_cgets(char *buffer)
{
    int max_count;
    int count = 0;

    if (buffer == NULL) {
        return NULL;
    }

    max_count = (unsigned char)buffer[0];
    while (count < max_count) {
        int character = _getch();
        if (character == EOF) {
            break;
        }
        if (character == '\r' || character == '\n') {
            break;
        }
        if (character == '\b') {
            if (count > 0) {
                count--;
                _putch('\b');
                _putch(' ');
                _putch('\b');
            }
            continue;
        }

        buffer[2 + count] = (char)character;
        count++;
        _putch(character);
    }

    buffer[1] = (char)count;
    buffer[2 + count] = '\0';
    _putch('\r');
    _putch('\n');

    return buffer;
}

#ifdef __cplusplus
}
#endif

#endif /* _CONIO_C_ */
