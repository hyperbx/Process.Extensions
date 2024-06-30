#include "testContext.h"

testContext m_fastcallTestStruct;

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
    m_fastcallTestStruct = testContext(1, 2, 3);

    return &m_fastcallTestStruct;
}

static int __fastcall fastcallTestStructAsArgument(testContext in_ctx)
{
    return in_ctx.a + in_ctx.b + in_ctx.c;
}

static int __fastcall fastcallTestStructPtrAsArgument(testContext* in_pCtx)
{
    return in_pCtx->a + in_pCtx->b + in_pCtx->c;
}

void fastcallLinkTests()
{
    fastcallTestNoArguments();
    fastcallTestSumOfArguments(1, 2, 3);
    fastcallTestStructAsArgument(fastcallTestReturnStruct());
    fastcallTestStructPtrAsArgument(fastcallTestReturnStructPtr());
}