#include "testContext.h"

testContext g_fastcallTestStruct;

static bool __fastcall fastcallTestNoArguments()
{
    return true;
}

static int __fastcall fastcallTestSumOfArguments(int in_a1, int in_a2, int in_a3)
{
    return in_a1 + in_a2 + in_a3;
}

static testContext __fastcall fastcallTestReturnStruct()
{
    testContext ctx{ 1, 2, 3 };

    return ctx;
}

static testContext* __fastcall fastcallTestReturnStructPtr()
{
    g_fastcallTestStruct = testContext(1, 2, 3);

    return &g_fastcallTestStruct;
}

static int __fastcall fastcallTestStructAsArgument(testContext in_ctx)
{
    return in_ctx.a + in_ctx.b + in_ctx.c;
}

static int __fastcall fastcallTestStructsAsArguments(testContext in_ctx1, testContext in_ctx2, testContext in_ctx3, testContext in_ctx4, testContext in_ctx5, testContext in_ctx6)
{
    return (in_ctx1.a + in_ctx1.b + in_ctx1.c) + (in_ctx2.a + in_ctx2.b + in_ctx2.c) + (in_ctx3.a + in_ctx3.b + in_ctx3.c) + (in_ctx4.a + in_ctx4.b + in_ctx4.c) + (in_ctx5.a + in_ctx5.b + in_ctx5.c) + (in_ctx6.a + in_ctx6.b + in_ctx6.c);
}

static int __fastcall fastcallTestStructPtrAsArgument(testContext* in_pCtx)
{
    return in_pCtx->a + in_pCtx->b + in_pCtx->c;
}

static int __fastcall fastcallTestStructPtrsAsArguments(testContext* in_pCtx1, testContext* in_pCtx2, testContext* in_pCtx3)
{
    return (in_pCtx1->a + in_pCtx1->b + in_pCtx1->c) + (in_pCtx2->a + in_pCtx2->b + in_pCtx2->c) + (in_pCtx3->a + in_pCtx3->b + in_pCtx3->c);
}

void fastcallLinkTests()
{
    fastcallTestNoArguments();
    fastcallTestSumOfArguments(1, 2, 3);
    fastcallTestStructAsArgument(fastcallTestReturnStruct());
    fastcallTestStructsAsArguments(fastcallTestReturnStruct(), fastcallTestReturnStruct(), fastcallTestReturnStruct(), fastcallTestReturnStruct(), fastcallTestReturnStruct(), fastcallTestReturnStruct());
    fastcallTestStructPtrAsArgument(fastcallTestReturnStructPtr());
    fastcallTestStructPtrsAsArguments(fastcallTestReturnStructPtr(), fastcallTestReturnStructPtr(), fastcallTestReturnStructPtr());
}