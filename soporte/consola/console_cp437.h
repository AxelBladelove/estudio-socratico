#ifndef ESTUDIO_SOCRATICO_CONSOLE_CP437_H
#define ESTUDIO_SOCRATICO_CONSOLE_CP437_H

#include <stdarg.h>
#include <stdlib.h>

static inline int estudio_conio_stdout_is_console(void)
{
    DWORD mode;
    HANDLE out = estudio_conio_stdout();

    return out != NULL && out != INVALID_HANDLE_VALUE && GetConsoleMode(out, &mode);
}

static inline WCHAR estudio_conio_map_cp437_byte(unsigned char byte)
{
    char single[2];
    WCHAR wide = L'?';

    switch (byte) {
    case '\a':
    case '\b':
    case '\f':
    case '\n':
    case '\r':
    case '\t':
    case '\v':
        return (WCHAR)byte;
    case 3:
        return 0x2665;
    case 4:
        return 0x2666;
    case 5:
        return 0x2663;
    case 6:
        return 0x2660;
    default:
        break;
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

#endif