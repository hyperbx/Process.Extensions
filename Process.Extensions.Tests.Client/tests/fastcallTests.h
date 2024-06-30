#pragma once

#include "testContext.h"

static bool __fastcall fastcallTestNoArguments();
static int __fastcall fastcallTestSumOfArguments(int in_a1, int in_a2, int in_a3);
static testContext __fastcall fastcallTestReturnStruct();
static testContext* __fastcall fastcallTestReturnStructPtr();
static int __fastcall fastcallTestStructAsArgument(testContext in_ctx);
static int __fastcall fastcallTestStructPtrAsArgument(testContext* in_pCtx);
void fastcallLinkTests();