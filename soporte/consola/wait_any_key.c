#include <windows.h>

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

        if (record.EventType == KEY_EVENT && record.Event.KeyEvent.bKeyDown) {
            return 0;
        }
    }
}