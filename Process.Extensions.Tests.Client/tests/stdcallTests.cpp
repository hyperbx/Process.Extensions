#include "testContext.h"

testContext m_stdcallTestStruct;

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
    m_stdcallTestStruct = testContext(1, 2, 3);

    return &m_stdcallTestStruct;
}

static int __stdcall stdcallTestStructAsArgument(testContext in_ctx)
{
    return in_ctx.a + in_ctx.b + in_ctx.c;
}

static int __stdcall stdcallTestStructPtrAsArgument(testContext* in_pCtx)
{
    return in_pCtx->a + in_pCtx->b + in_pCtx->c;
}

void stdcallLinkTests()
{
    stdcallTestNoArguments();
    stdcallTestSumOfArguments(1, 2, 3);
    stdcallTestStructAsArgument(stdcallTestReturnStruct());
    stdcallTestStructPtrAsArgument(stdcallTestReturnStructPtr());
}