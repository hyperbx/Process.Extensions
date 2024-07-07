#include <iostream>
#include <Windows.h>

BOOL WINAPI DllMain(_In_ HINSTANCE hInstance, _In_ DWORD reason, _In_ LPVOID reserved)
{
	switch (reason)
	{
		case DLL_PROCESS_ATTACH:
			printf("Client module attached.\n");
			break;

		case DLL_PROCESS_DETACH:
			printf("Client module detached.\n");
			break;

		case DLL_THREAD_ATTACH:
		case DLL_THREAD_DETACH:
			break;
	}

	return TRUE;
}

extern "C" int __declspec(dllexport) dllexportTestSumOfArguments(int in_a1, int in_a2, int in_a3)
{
	return in_a1 + in_a2 + in_a3;
}