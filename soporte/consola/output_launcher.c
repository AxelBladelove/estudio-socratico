#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <windows.h>

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
    char latest_file[MAX_PATH * 4];
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

int main(void)
{
    char latest_exe[MAX_PATH * 4];
    STARTUPINFOA startup_info;
    PROCESS_INFORMATION process_info;
    char *command_line;
    size_t command_length;
    DWORD exit_code = 1;

    if (load_latest_target(latest_exe, sizeof(latest_exe)) != 0) {
        return 1;
    }

    if (GetFileAttributesA(latest_exe) == INVALID_FILE_ATTRIBUTES) {
        fprintf(stderr, "[ERROR] El ultimo binario ya no existe: %s\n", latest_exe);
        return 1;
    }

    ZeroMemory(&startup_info, sizeof(startup_info));
    ZeroMemory(&process_info, sizeof(process_info));
    startup_info.cb = sizeof(startup_info);

    command_length = strlen(latest_exe) + 3;
    command_line = (char *)malloc(command_length);
    if (!command_line) {
        fprintf(stderr, "[ERROR] Memoria insuficiente para lanzar el binario.\n");
        return 1;
    }

    snprintf(command_line, command_length, "\"%s\"", latest_exe);

    if (!CreateProcessA(NULL, command_line, NULL, NULL, TRUE, 0, NULL, NULL, &startup_info, &process_info)) {
        fprintf(stderr, "[ERROR] No se pudo ejecutar el binario: %s\n", latest_exe);
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