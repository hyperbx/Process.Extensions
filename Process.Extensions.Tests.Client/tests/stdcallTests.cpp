#include "testContext.h"

testContext g_stdcallTestStruct;

static bool __stdcall stdcallTestNoArguments()
{
    return true;
}

static int __stdcall stdcallTestSumOfArguments(int in_a1, int in_a2, int in_a3)
{
    return in_a1 + in_a2 + in_a3;
}

static testContext __stdcall stdcallTestReturnStruct()
{
    testContext ctx{ 1, 2, 3 };

    return ctx;
}

static testContext* __stdcall stdcallTestReturnStructPtr()
{
    g_stdcallTestStruct = testContext(1, 2, 3);

    return &g_stdcallTestStruct;
}

static int __stdcall stdcallTestStructAsArgument(testContext in_ctx)
{
    return in_ctx.a + in_ctx.b + in_ctx.c;
}

static int __stdcall stdcallTestStructsAsArguments(testContext in_ctx1, testContext in_ctx2, testContext in_ctx3)
{
    return (in_ctx1.a + in_ctx1.b + in_ctx1.c) + (in_ctx2.a + in_ctx2.b + in_ctx2.c) + (in_ctx3.a + in_ctx3.b + in_ctx3.c);
}

static int __stdcall stdcallTestStructPtrAsArgument(testContext* in_pCtx)
{
    return in_pCtx->a + in_pCtx->b + in_pCtx->c;
}

static int __stdcall stdcallTestStructPtrsAsArguments(testContext* in_pCtx1, testContext* in_pCtx2, testContext* in_pCtx3)
{
    return (in_pCtx1->a + in_pCtx1->b + in_pCtx1->c) + (in_pCtx2->a + in_pCtx2->b + in_pCtx2->c) + (in_pCtx3->a + in_pCtx3->b + in_pCtx3->c);
}

void stdcallLinkTests()
{
    stdcallTestNoArguments();
    stdcallTestSumOfArguments(1, 2, 3);
    stdcallTestStructAsArgument(stdcallTestReturnStruct());
    stdcallTestStructsAsArguments(stdcallTestReturnStruct(), stdcallTestReturnStruct(), stdcallTestReturnStruct());
    stdcallTestStructPtrAsArgument(stdcallTestReturnStructPtr());
    stdcallTestStructPtrsAsArguments(stdcallTestReturnStructPtr(), stdcallTestReturnStructPtr(), stdcallTestReturnStructPtr());
}