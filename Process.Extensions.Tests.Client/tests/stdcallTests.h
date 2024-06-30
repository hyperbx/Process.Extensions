#pragma once

static bool __stdcall stdcallTestNoArguments();
static int __stdcall stdcallTestSumOfArguments(int in_a1, int in_a2, int in_a3);
static testContext __stdcall stdcallTestReturnStruct();
static testContext* __stdcall stdcallTestReturnStructPtr();
static int __stdcall stdcallTestStructAsArgument(testContext in_ctx);
static int __stdcall stdcallTestStructPtrAsArgument(testContext* in_pCtx);
void stdcallLinkTests();