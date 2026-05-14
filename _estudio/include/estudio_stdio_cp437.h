#ifndef ESTUDIO_STDIO_CP437_H
#define ESTUDIO_STDIO_CP437_H

#include <stdarg.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <windows.h>

/* Windows Terminal treats low ASCII bytes as controls. Code::Blocks-era
   console programs expect CP437 glyphs for card suits and box drawing. */
static inline HANDLE estudio_stdout_handle(void)
{
    return GetStdHandle(STD_OUTPUT_HANDLE);
}

static inline int estudio_stdout_is_console(void)
{
    DWORD mode;
    HANDLE out = estudio_stdout_handle();

    return out != NULL && out != INVALID_HANDLE_VALUE && GetConsoleMode(out, &mode);
}

static inline WCHAR estudio_cp437_to_wchar(unsigned char byte)
{
    static const WCHAR cp437[256] = {
        0x0000, 0x263A, 0x263B, 0x2665, 0x2666, 0x2663, 0x2660, 0x2022,
        0x25D8, 0x25CB, 0x25D9, 0x2642, 0x2640, 0x266A, 0x266B, 0x263C,
        0x25BA, 0x25C4, 0x2195, 0x203C, 0x00B6, 0x00A7, 0x25AC, 0x21A8,
        0x2191, 0x2193, 0x2192, 0x2190, 0x221F, 0x2194, 0x25B2, 0x25BC,
        0x0020, 0x0021, 0x0022, 0x0023, 0x0024, 0x0025, 0x0026, 0x0027,
        0x0028, 0x0029, 0x002A, 0x002B, 0x002C, 0x002D, 0x002E, 0x002F,
        0x0030, 0x0031, 0x0032, 0x0033, 0x0034, 0x0035, 0x0036, 0x0037,
        0x0038, 0x0039, 0x003A, 0x003B, 0x003C, 0x003D, 0x003E, 0x003F,
        0x0040, 0x0041, 0x0042, 0x0043, 0x0044, 0x0045, 0x0046, 0x0047,
        0x0048, 0x0049, 0x004A, 0x004B, 0x004C, 0x004D, 0x004E, 0x004F,
        0x0050, 0x0051, 0x0052, 0x0053, 0x0054, 0x0055, 0x0056, 0x0057,
        0x0058, 0x0059, 0x005A, 0x005B, 0x005C, 0x005D, 0x005E, 0x005F,
        0x0060, 0x0061, 0x0062, 0x0063, 0x0064, 0x0065, 0x0066, 0x0067,
        0x0068, 0x0069, 0x006A, 0x006B, 0x006C, 0x006D, 0x006E, 0x006F,
        0x0070, 0x0071, 0x0072, 0x0073, 0x0074, 0x0075, 0x0076, 0x0077,
        0x0078, 0x0079, 0x007A, 0x007B, 0x007C, 0x007D, 0x007E, 0x2302,
        0x00C7, 0x00FC, 0x00E9, 0x00E2, 0x00E4, 0x00E0, 0x00E5, 0x00E7,
        0x00EA, 0x00EB, 0x00E8, 0x00EF, 0x00EE, 0x00EC, 0x00C4, 0x00C5,
        0x00C9, 0x00E6, 0x00C6, 0x00F4, 0x00F6, 0x00F2, 0x00FB, 0x00F9,
        0x00FF, 0x00D6, 0x00DC, 0x00A2, 0x00A3, 0x00A5, 0x20A7, 0x0192,
        0x00E1, 0x00ED, 0x00F3, 0x00FA, 0x00F1, 0x00D1, 0x00AA, 0x00BA,
        0x00BF, 0x2310, 0x00AC, 0x00BD, 0x00BC, 0x00A1, 0x00AB, 0x00BB,
        0x2591, 0x2592, 0x2593, 0x2502, 0x2524, 0x2561, 0x2562, 0x2556,
        0x2555, 0x2563, 0x2551, 0x2557, 0x255D, 0x255C, 0x255B, 0x2510,
        0x2514, 0x2534, 0x252C, 0x251C, 0x2500, 0x253C, 0x255E, 0x255F,
        0x255A, 0x2554, 0x2569, 0x2566, 0x2560, 0x2550, 0x256C, 0x2567,
        0x2568, 0x2564, 0x2565, 0x2559, 0x2558, 0x2552, 0x2553, 0x256B,
        0x256A, 0x2518, 0x250C, 0x2588, 0x2584, 0x258C, 0x2590, 0x2580,
        0x03B1, 0x00DF, 0x0393, 0x03C0, 0x03A3, 0x03C3, 0x00B5, 0x03C4,
        0x03A6, 0x0398, 0x03A9, 0x03B4, 0x221E, 0x03C6, 0x03B5, 0x2229,
        0x2261, 0x00B1, 0x2265, 0x2264, 0x2320, 0x2321, 0x00F7, 0x2248,
        0x00B0, 0x2219, 0x00B7, 0x221A, 0x207F, 0x00B2, 0x25A0, 0x00A0
    };

    return cp437[byte];
}

static inline WCHAR estudio_console_wchar(unsigned char byte)
{
    switch (byte) {
    case '\a':
    case '\b':
    case '\t':
    case '\n':
    case '\r':
        return (WCHAR)byte;
    default:
        return estudio_cp437_to_wchar(byte);
    }
}

static inline int estudio_write_cp437(const char *text, size_t length)
{
    WCHAR *wide;
    DWORD written = 0;
    HANDLE out = estudio_stdout_handle();
    size_t source = 0;
    size_t target = 0;

    if (!estudio_stdout_is_console()) {
        return (int)fwrite(text, 1, length, stdout);
    }

    wide = (WCHAR *)malloc(sizeof(WCHAR) * ((length * 2) + 1));
    if (wide == NULL) {
        return (int)fwrite(text, 1, length, stdout);
    }

    while (source < length) {
        unsigned char byte = (unsigned char)text[source++];
        if (byte == '\n' && (source < 2 || text[source - 2] != '\r')) {
            wide[target++] = L'\r';
        }
        wide[target++] = estudio_console_wchar(byte);
    }

    if (!WriteConsoleW(out, wide, (DWORD)target, &written, NULL)) {
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

    written = estudio_write_cp437(buffer, (size_t)length);
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

    return estudio_write_cp437((const char *)&byte, 1) >= 1 ? character : EOF;
}

static inline int estudio_puts(const char *text)
{
    if (text == NULL) {
        return EOF;
    }

    if (estudio_write_cp437(text, strlen(text)) < 0) {
        return EOF;
    }

    return estudio_write_cp437("\n", 1) >= 1 ? 0 : EOF;
}

#endif
