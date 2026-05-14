#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <windows.h>

#define ESTUDIO_MAX_PATH (MAX_PATH * 4)

static int build_latest_path(char *buffer, size_t buffer_size)
{
    DWORD length = GetModuleFileNameA(NULL, buffer, (DWORD)buffer_size);
    char *last_separator;

    if (length == 0 || length >= buffer_size) {
        return 1;
    }

    last_separator = strrchr(buffer, '\\');
    if (last_separator == NULL) {
        return 1;
    }

    strcpy(last_separator + 1, "latest_exe.txt");
    return 0;
}

static int load_latest_target(char *buffer, size_t buffer_size)
{
    char latest_file[ESTUDIO_MAX_PATH];
    FILE *fp;
    size_t length;

    if (build_latest_path(latest_file, sizeof(latest_file)) != 0) {
        fprintf(stderr, "[ERROR] No se pudo resolver la ruta de latest_exe.txt.\n");
        return 1;
    }

    fp = fopen(latest_file, "r");
    if (!fp) {
        fprintf(stderr, "[ERROR] No hay un binario compilado reciente. Compila primero.\n");
        return 1;
    }

    if (!fgets(buffer, (int)buffer_size, fp)) {
        fclose(fp);
        fprintf(stderr, "[ERROR] No se pudo leer la ruta del ultimo binario.\n");
        return 1;
    }

    fclose(fp);

    length = strlen(buffer);
    while (length > 0 && (buffer[length - 1] == '\n' || buffer[length - 1] == '\r')) {
        buffer[length - 1] = '\0';
        length--;
    }

    if (length == 0) {
        fprintf(stderr, "[ERROR] La ruta del ultimo binario esta vacia.\n");
        return 1;
    }

    return 0;
}

static void append_quoted_arg(char *buffer, size_t buffer_size, const char *arg)
{
    strncat(buffer, "\"", buffer_size - strlen(buffer) - 1);
    while (*arg != '\0' && strlen(buffer) + 2 < buffer_size) {
        if (*arg == '"') {
            strncat(buffer, "\\\"", buffer_size - strlen(buffer) - 1);
        } else {
            size_t length = strlen(buffer);
            buffer[length] = *arg;
            buffer[length + 1] = '\0';
        }
        arg++;
    }
    strncat(buffer, "\"", buffer_size - strlen(buffer) - 1);
}

static int build_command_line(char *buffer, size_t buffer_size, const char *exe_path,
                              int argc, char **argv, int first_program_arg)
{
    int index;

    buffer[0] = '\0';
    append_quoted_arg(buffer, buffer_size, exe_path);

    for (index = first_program_arg; index < argc; index++) {
        if (strlen(buffer) + strlen(argv[index]) + 4 >= buffer_size) {
            fprintf(stderr, "[ERROR] La linea de ejecucion es demasiado larga.\n");
            return 1;
        }
        strncat(buffer, " ", buffer_size - strlen(buffer) - 1);
        append_quoted_arg(buffer, buffer_size, argv[index]);
    }

    return 0;
}

static void write_utf8_line(FILE *fp, const WCHAR *line, int length)
{
    char stack_buffer[4096];
    char *utf8 = stack_buffer;
    int needed;

    while (length > 0 && line[length - 1] == L' ') {
        length--;
    }

    needed = WideCharToMultiByte(CP_UTF8, 0, line, length, NULL, 0, NULL, NULL);
    if (needed <= 0) {
        fputc('\n', fp);
        return;
    }

    if (needed >= (int)sizeof(stack_buffer)) {
        utf8 = (char *)malloc((size_t)needed + 1);
        if (utf8 == NULL) {
            fputc('\n', fp);
            return;
        }
    }

    WideCharToMultiByte(CP_UTF8, 0, line, length, utf8, needed, NULL, NULL);
    fwrite(utf8, 1, (size_t)needed, fp);
    fputc('\n', fp);

    if (utf8 != stack_buffer) {
        free(utf8);
    }
}

static void dump_console_to_log(const char *log_path)
{
    HANDLE console;
    CONSOLE_SCREEN_BUFFER_INFO info;
    FILE *fp;
    WCHAR *line;
    DWORD read;
    COORD coord;
    int y;

    if (log_path == NULL || log_path[0] == '\0') {
        return;
    }

    console = GetStdHandle(STD_OUTPUT_HANDLE);
    if (console == NULL || console == INVALID_HANDLE_VALUE) {
        return;
    }

    if (!GetConsoleScreenBufferInfo(console, &info)) {
        return;
    }

    fp = fopen(log_path, "ab");
    if (!fp) {
        return;
    }

    line = (WCHAR *)malloc(sizeof(WCHAR) * (size_t)info.dwSize.X);
    if (line == NULL) {
        fclose(fp);
        return;
    }

    fprintf(fp, "\n[EJECUCION DEL USUARIO]\n");
    coord.X = 0;
    for (y = 0; y <= info.dwCursorPosition.Y; y++) {
        coord.Y = (SHORT)y;
        if (ReadConsoleOutputCharacterW(console, line, info.dwSize.X, coord, &read)) {
            write_utf8_line(fp, line, (int)read);
        }
    }
    fprintf(fp, "\n------------------------------------------------------------\n");

    free(line);
    fclose(fp);
}

static int is_modifier_key(WORD virtual_key)
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

static void wait_for_key(void)
{
    HANDLE input = GetStdHandle(STD_INPUT_HANDLE);
    INPUT_RECORD record;
    DWORD read = 0;

    if (input == NULL || input == INVALID_HANDLE_VALUE) {
        return;
    }

    FlushConsoleInputBuffer(input);
    for (;;) {
        if (!ReadConsoleInputW(input, &record, 1, &read)) {
            return;
        }

        if (record.EventType == KEY_EVENT && record.Event.KeyEvent.bKeyDown &&
            !is_modifier_key(record.Event.KeyEvent.wVirtualKeyCode)) {
            return;
        }
    }
}

static double elapsed_seconds(LARGE_INTEGER start, LARGE_INTEGER end, LARGE_INTEGER frequency)
{
    if (frequency.QuadPart == 0) {
        return 0.0;
    }

    return (double)(end.QuadPart - start.QuadPart) / (double)frequency.QuadPart;
}

static int line_has_content(const WCHAR *line, int length)
{
    int index;

    for (index = 0; index < length; index++) {
        if (line[index] != L' ' && line[index] != L'\0') {
            return 1;
        }
    }

    return 0;
}

static int find_last_visible_content_row(HANDLE console, const CONSOLE_SCREEN_BUFFER_INFO *info)
{
    WCHAR *line;
    DWORD read;
    COORD coord;
    int y;
    int last_row = -1;

    line = (WCHAR *)malloc(sizeof(WCHAR) * (size_t)info->dwSize.X);
    if (line == NULL) {
        return -1;
    }

    coord.X = 0;
    for (y = info->srWindow.Top; y <= info->srWindow.Bottom; y++) {
        coord.Y = (SHORT)y;
        if (ReadConsoleOutputCharacterW(console, line, info->dwSize.X, coord, &read) &&
            line_has_content(line, (int)read)) {
            last_row = y;
        }
    }

    free(line);
    return last_row;
}

static int place_runner_message_cursor(void)
{
    HANDLE console = GetStdHandle(STD_OUTPUT_HANDLE);
    CONSOLE_SCREEN_BUFFER_INFO info;
    COORD coord;
    int last_content_row;
    int target_row;

    if (console == NULL || console == INVALID_HANDLE_VALUE) {
        return 1;
    }

    if (!GetConsoleScreenBufferInfo(console, &info)) {
        return 1;
    }

    last_content_row = find_last_visible_content_row(console, &info);
    target_row = info.dwCursorPosition.Y + 1;
    if (last_content_row >= 0 && target_row < last_content_row + 2) {
        target_row = last_content_row + 2;
    }

    coord.X = 0;
    if (target_row >= info.dwSize.Y) {
        coord.Y = (SHORT)(info.dwSize.Y - 1);
        if (!SetConsoleCursorPosition(console, coord)) {
            return 1;
        }
        printf("\n");
        fflush(stdout);
        return 0;
    }

    coord.Y = (SHORT)target_row;
    return SetConsoleCursorPosition(console, coord) ? 0 : 1;
}

int main(int argc, char **argv)
{
    char target_exe[ESTUDIO_MAX_PATH];
    char command_line[ESTUDIO_MAX_PATH * 2];
    const char *log_path = NULL;
    int first_program_arg = 1;
    STARTUPINFOA startup_info;
    PROCESS_INFORMATION process_info;
    LARGE_INTEGER start_time;
    LARGE_INTEGER end_time;
    LARGE_INTEGER frequency;
    DWORD exit_code = 1;

    SetConsoleOutputCP(437);
    SetConsoleCP(437);

    if (argc >= 3 && strcmp(argv[1], "--run") == 0) {
        strncpy(target_exe, argv[2], sizeof(target_exe) - 1);
        target_exe[sizeof(target_exe) - 1] = '\0';
        first_program_arg = 3;
    } else if (argc >= 2) {
        strncpy(target_exe, argv[1], sizeof(target_exe) - 1);
        target_exe[sizeof(target_exe) - 1] = '\0';
        first_program_arg = 2;
    } else if (load_latest_target(target_exe, sizeof(target_exe)) != 0) {
        return 1;
    }

    if (first_program_arg + 1 < argc && strcmp(argv[first_program_arg], "--log") == 0) {
        log_path = argv[first_program_arg + 1];
        first_program_arg += 2;
    }

    if (GetFileAttributesA(target_exe) == INVALID_FILE_ATTRIBUTES) {
        fprintf(stderr, "[ERROR] El binario no existe: %s\n", target_exe);
        return 1;
    }

    if (build_command_line(command_line, sizeof(command_line), target_exe, argc, argv, first_program_arg) != 0) {
        return 1;
    }

    ZeroMemory(&startup_info, sizeof(startup_info));
    ZeroMemory(&process_info, sizeof(process_info));
    startup_info.cb = sizeof(startup_info);

    QueryPerformanceFrequency(&frequency);
    QueryPerformanceCounter(&start_time);

    if (!CreateProcessA(NULL, command_line, NULL, NULL, TRUE, 0, NULL, NULL, &startup_info, &process_info)) {
        fprintf(stderr, "[ERROR] No se pudo ejecutar el binario: %s\n", target_exe);
        return 1;
    }

    WaitForSingleObject(process_info.hProcess, INFINITE);
    QueryPerformanceCounter(&end_time);
    GetExitCodeProcess(process_info.hProcess, &exit_code);
    CloseHandle(process_info.hThread);
    CloseHandle(process_info.hProcess);

    if (place_runner_message_cursor() != 0) {
        printf("\n");
    }
    printf("Process returned %lu (0x%lX)   execution time : %.3f s\n",
           exit_code, exit_code, elapsed_seconds(start_time, end_time, frequency));
    printf("Press any key to continue.");

    dump_console_to_log(log_path);
    wait_for_key();

    return (int)exit_code;
}
