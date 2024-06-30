#include "testContext.h"

testContext m_cdeclTestStruct;

static bool __cdecl cdeclTestNoArguments()
{
    return true;
}

static int __cdecl cdeclTestSumOfArguments(int in_a1, int in_a2, int in_a3)
{
    return in_a1 + in_a2 + in_a3;
}

static testContext __cdecl cdeclTestReturnStruct()
{
    testContext ctx{1, 2, 3};

    return ctx;
}

static testContext* __cdecl cdeclTestReturnStructPtr()
{
    m_cdeclTestStruct = testContext(1, 2, 3);

    return &m_cdeclTestStruct;
}

static int __cdecl cdeclTestStructAsArgument(testContext in_ctx)
{
    return in_ctx.a + in_ctx.b + in_ctx.c;
}

static int __cdecl cdeclTestStructPtrAsArgument(testContext* in_pCtx)
{
    return in_pCtx->a + in_pCtx->b + in_pCtx->c;
}

void cdeclLinkTests()
{
    cdeclTestNoArguments();
    cdeclTestSumOfArguments(1, 2, 3);
    cdeclTestStructAsArgument(cdeclTestReturnStruct());
    cdeclTestStructPtrAsArgument(cdeclTestReturnStructPtr());
}