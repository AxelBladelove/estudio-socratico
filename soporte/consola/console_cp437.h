#ifndef ESTUDIO_SOCRATICO_CONSOLE_CP437_H
#define ESTUDIO_SOCRATICO_CONSOLE_CP437_H

#include <stdarg.h>
#include <stdlib.h>
#include <string.h>

static inline int estudio_conio_stdout_is_console(void)
{
    DWORD mode;
    HANDLE out = estudio_conio_stdout();

    return out != NULL && out != INVALID_HANDLE_VALUE && GetConsoleMode(out, &mode);
}

static inline WCHAR estudio_conio_map_cp437_byte(unsigned char byte)
{
    static const WCHAR cp437_low[32] = {
        0x0000, 0x263A, 0x263B, 0x2665, 0x2666, 0x2663, 0x2660, 0x2022,
        0x25D8, 0x25CB, 0x25D9, 0x2642, 0x2640, 0x266A, 0x266B, 0x263C,
        0x25BA, 0x25C4, 0x2195, 0x203C, 0x00B6, 0x00A7, 0x25AC, 0x21A8,
        0x2191, 0x2193, 0x2192, 0x2190, 0x221F, 0x2194, 0x25B2, 0x25BC
    };
    char single[2];
    WCHAR wide = L'?';

    switch (byte) {
    case '\b':
    case '\n':
    case '\r':
    case '\t':
        return (WCHAR)byte;
    default:
        break;
    }

    if (byte > 0 && byte < 32) {
        return cp437_low[byte];
    }

    if (byte < 128) {
        return (WCHAR)byte;
    }

    single[0] = (char)byte;
    single[1] = '\0';
    if (MultiByteToWideChar(437, 0, single, 1, &wide, 1) == 1) {
        return wide;
    }

    return L'?';
}

static inline int estudio_conio_write_cp437(const char *text, size_t length)
{
    WCHAR *wide;
    DWORD written = 0;
    HANDLE out = estudio_conio_stdout();
    size_t index;

    if (!estudio_conio_stdout_is_console()) {
        return (int)fwrite(text, 1, length, stdout);
    }

    wide = (WCHAR *)malloc(sizeof(WCHAR) * length);
    if (wide == NULL) {
        return (int)fwrite(text, 1, length, stdout);
    }

    for (index = 0; index < length; index++) {
        wide[index] = estudio_conio_map_cp437_byte((unsigned char)text[index]);
    }

    if (!WriteConsoleW(out, wide, (DWORD)length, &written, NULL)) {
        free(wide);
        return (int)fwrite(text, 1, length, stdout);
    }

    free(wide);
    return (int)written;
}

static inline int estudio_vprintf(const char *format, va_list args)
{
    va_list copy;
    char *buffer;
    int length;
    int written;

    va_copy(copy, args);
    length = vsnprintf(NULL, 0, format, copy);
    va_end(copy);
    if (length < 0) {
        return vfprintf(stdout, format, args);
    }

    buffer = (char *)malloc((size_t)length + 1);
    if (buffer == NULL) {
        return vfprintf(stdout, format, args);
    }

    if (vsnprintf(buffer, (size_t)length + 1, format, args) < 0) {
        free(buffer);
        return -1;
    }

    written = estudio_conio_write_cp437(buffer, (size_t)length);
    free(buffer);
    return written;
}

static inline int estudio_printf(const char *format, ...)
{
    va_list args;
    int written;

    va_start(args, format);
    written = estudio_vprintf(format, args);
    va_end(args);
    return written;
}

static inline int estudio_putchar(int character)
{
    unsigned char byte = (unsigned char)character;
    return estudio_conio_write_cp437((const char *)&byte, 1) == 1 ? character : EOF;
}

static inline int estudio_puts(const char *text)
{
    if (text == NULL) {
        return EOF;
    }

    if (estudio_conio_write_cp437(text, strlen(text)) < 0) {
        return EOF;
    }

    return estudio_conio_write_cp437("\n", 1) == 1 ? 0 : EOF;
}

#endif
