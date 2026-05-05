#include <windows.h>
#include <conio.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

static void prepare_console(void)
{
    HANDLE console = GetStdHandle(STD_OUTPUT_HANDLE);
    CONSOLE_FONT_INFOEX font_info;

    SetConsoleCP(437);
    SetConsoleOutputCP(437);

    if (console == NULL || console == INVALID_HANDLE_VALUE) {
        return;
    }

    ZeroMemory(&font_info, sizeof(font_info));
    font_info.cbSize = sizeof(font_info);

    if (!GetCurrentConsoleFontEx(console, FALSE, &font_info)) {
        return;
    }

    font_info.FontFamily = FF_DONTCARE;
    font_info.FontWeight = FW_NORMAL;
    font_info.dwFontSize.X = 0;
    font_info.dwFontSize.Y = 16;
    lstrcpyW(font_info.FaceName, L"Terminal");
    SetCurrentConsoleFontEx(console, FALSE, &font_info);
}

static int run_process_and_wait(const char *exe_path)
{
    STARTUPINFOA startup_info;
    PROCESS_INFORMATION process_info;
    char *command_line;
    size_t command_length;
    DWORD exit_code = 1;

    ZeroMemory(&startup_info, sizeof(startup_info));
    ZeroMemory(&process_info, sizeof(process_info));
    startup_info.cb = sizeof(startup_info);

    command_length = strlen(exe_path) + 3;
    command_line = (char *)malloc(command_length);
    if (!command_line) {
        return 1;
    }

    snprintf(command_line, command_length, "\"%s\"", exe_path);

    if (!CreateProcessA(NULL, command_line, NULL, NULL, TRUE, 0, NULL, NULL, &startup_info, &process_info)) {
        free(command_line);
        return 1;
    }

    WaitForSingleObject(process_info.hProcess, INFINITE);
    GetExitCodeProcess(process_info.hProcess, &exit_code);

    CloseHandle(process_info.hThread);
    CloseHandle(process_info.hProcess);
    free(command_line);
    return (int)exit_code;
}

static void dump_console_if_possible(const char *dump_exe, const char *log_path)
{
    STARTUPINFOA startup_info;
    PROCESS_INFORMATION process_info;
    char *command_line;
    size_t command_length;

    if (!dump_exe || !log_path) {
        printf(" [AVISO] No se pudo registrar el volcado de consola en el log.\n");
        return;
    }

    if (GetFileAttributesA(dump_exe) == INVALID_FILE_ATTRIBUTES) {
        printf(" [AVISO] No se pudo registrar el volcado de consola en el log.\n");
        return;
    }

    ZeroMemory(&startup_info, sizeof(startup_info));
    ZeroMemory(&process_info, sizeof(process_info));
    startup_info.cb = sizeof(startup_info);

    command_length = strlen(dump_exe) + strlen(log_path) + 6;
    command_line = (char *)malloc(command_length);
    if (!command_line) {
        printf(" [AVISO] No se pudo registrar el volcado de consola en el log.\n");
        return;
    }

    snprintf(command_line, command_length, "\"%s\" \"%s\"", dump_exe, log_path);

    if (CreateProcessA(NULL, command_line, NULL, NULL, TRUE, 0, NULL, NULL, &startup_info, &process_info)) {
        WaitForSingleObject(process_info.hProcess, INFINITE);
        CloseHandle(process_info.hThread);
        CloseHandle(process_info.hProcess);
    } else {
        printf(" [AVISO] No se pudo registrar el volcado de consola en el log.\n");
    }

    free(command_line);
}

int main(int argc, char *argv[])
{
    int exit_code;

    if (argc < 2) {
        return 1;
    }

    prepare_console();
    exit_code = run_process_and_wait(argv[1]);

    printf("\n================================\n");
    printf(" Programa finalizado.\n");
    dump_console_if_possible(argc > 3 ? argv[3] : NULL, argc > 2 ? argv[2] : NULL);
    printf(" Presiona cualquier tecla para cerrar esta ventana.\n");
    printf("================================\n");
    FlushConsoleInputBuffer(GetStdHandle(STD_INPUT_HANDLE));
    _getch();

    return exit_code;
}