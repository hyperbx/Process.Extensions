#pragma once

#include "testContext.h"

static bool __cdecl cdeclTestNoArguments();
static int __cdecl cdeclTestSumOfArguments(int in_a1, int in_a2, int in_a3);
static testContext __cdecl cdeclTestReturnStruct();
static testContext* __cdecl cdeclTestReturnStructPtr();
static int __cdecl cdeclTestStructAsArgument(testContext in_ctx);
static int __cdecl cdeclTestStructPtrAsArgument(testContext* in_pCtx);
void cdeclLinkTests();