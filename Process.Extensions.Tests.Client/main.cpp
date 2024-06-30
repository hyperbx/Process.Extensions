#include <iostream>
#include <Windows.h>

#include "tests\cdeclTests.h"
#include "tests\fastcallTests.h"
#include "tests\stdcallTests.h"
#include "tests\thiscallTests.h"

bool m_isRunning = true;

static void signalExit()
{
    m_isRunning = false;
}

static void linkAll()
{
    if (!m_isRunning)
        return;

    signalExit();

    cdeclLinkTests();
    fastcallLinkTests();
    stdcallLinkTests();
    thiscallLinkTests();
}

int main()
{
    while (m_isRunning)
        Sleep(20);

    linkAll();
}