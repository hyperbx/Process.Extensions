#pragma once

class testContext
{
public:
	int a{};
	int b{};
	int c{};

	testContext() {}
	testContext(int in_a, int in_b, int in_c) : a(in_a), b(in_b), c(in_c) {}

	bool __thiscall thiscallTestNoArguments()
	{
		return true;
	}

	int __thiscall thiscallTestSumOfArguments(int in_a1, int in_a2, int in_a3)
	{
		return in_a1 + in_a2 + in_a3;
	}

	int __thiscall thiscallTestSumOfFields()
	{
		return a + b + c;
	}

	int __thiscall thiscallTestSumOfFieldsAndArguments(int in_a1, int in_a2, int in_a3)
	{
		return (a + b + c) + (in_a1 + in_a2 + in_a3);
	}

	int __thiscall thiscallTestSumOfFieldsAndArgumentsNested(int in_a1, int in_a2, int in_a3)
	{
		return (a + b + c) + thiscallTestSumOfArguments(in_a1, in_a2, in_a3);
	}

	testContext __thiscall thiscallTestReturnStruct()
	{
		testContext ctx{ 1, 2, 3 };

		return ctx;
	}

	int __thiscall thiscallTestStructAsArgument(testContext in_ctx)
	{
		return in_ctx.a + in_ctx.b + in_ctx.c;
	}

	int __thiscall thiscallTestStructPtrAsArgument(testContext* in_pCtx)
	{
		return in_pCtx->a + in_pCtx->b + in_pCtx->c;
	}
};