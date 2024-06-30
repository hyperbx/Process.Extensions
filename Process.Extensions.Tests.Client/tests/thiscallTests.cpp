#include "testContext.h"

void thiscallLinkTests()
{
	testContext ctx{1, 2, 3};

	ctx.thiscallTestNoArguments();
	ctx.thiscallTestSumOfArguments(1, 2, 3);
	ctx.thiscallTestSumOfFields();
	ctx.thiscallTestSumOfFieldsAndArguments(1, 2, 3);
	ctx.thiscallTestSumOfFieldsAndArgumentsNested(1, 2, 3);
	
	auto _ctx = ctx.thiscallTestReturnStruct();

	ctx.thiscallTestStructAsArgument(_ctx);
	ctx.thiscallTestStructPtrAsArgument(&_ctx);
}