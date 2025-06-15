#define WIN32_LEAN_AND_MEAN

#include <iostream>
#include <Windows.h>

#include "tests\cdeclTests.h"
#include "tests\fastcallTests.h"
#include "tests\stdcallTests.h"
#include "tests\thiscallTests.h"

bool g_isRunning = true;

static void signalExit()
{
    g_isRunning = false;
}

static void runAllTests()
{
    cdeclLinkTests();
    stdcallLinkTests();
    fastcallLinkTests();
    thiscallLinkTests();
}

static void linkAll()
{
    if (!g_isRunning)
        return;

    signalExit();

    runAllTests();
}

int main()
{
    while (g_isRunning)
    {
        if (GetAsyncKeyState(VK_F1) & 0x8000)
            runAllTests();

        Sleep(20);
    }

    linkAll();

    return 0;
}