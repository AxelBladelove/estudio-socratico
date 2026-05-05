#include <windows.h>

static int estudio_es_tecla_modificadora(WORD virtual_key)
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

int main(void)
{
    HANDLE input = GetStdHandle(STD_INPUT_HANDLE);
    INPUT_RECORD record;
    DWORD read = 0;
    DWORD attempt;

    if (input == NULL || input == INVALID_HANDLE_VALUE) {
        return 1;
    }

    for (attempt = 0; attempt < 10; attempt++) {
        FlushConsoleInputBuffer(input);
        Sleep(100);
    }

    FlushConsoleInputBuffer(input);

    for (;;) {
        if (!ReadConsoleInputW(input, &record, 1, &read)) {
            return 1;
        }

        if (record.EventType == KEY_EVENT && record.Event.KeyEvent.bKeyDown &&
            !estudio_es_tecla_modificadora(record.Event.KeyEvent.wVirtualKeyCode)) {
            return 0;
        }
    }
}