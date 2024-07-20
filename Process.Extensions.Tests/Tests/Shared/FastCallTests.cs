using ProcessExtensions.Enums;
using ProcessExtensions.Extensions;
using ProcessExtensions.Interop;
using ProcessExtensions.Interop.Generic;
using ProcessExtensions.Interop.Structures;
using ProcessExtensions.Logger;
using System.Diagnostics;

namespace ProcessExtensions.Tests.Shared
{
    internal class FastCallTests : TestBase
    {
        private UnmanagedProcessFunctionPointer<byte>? fastcallTestNoArguments;
        private UnmanagedProcessFunctionPointer<int, int, int, int>? fastcallTestSumOfArguments;
        private UnmanagedProcessFunctionPointer<UnmanagedPointer, UnmanagedPointer>? fastcallTestReturnStruct;
        private UnmanagedProcessFunctionPointer<UnmanagedPointer>? fastcallTestReturnStructPtr;
        private UnmanagedProcessFunctionPointer<int, TestContext>? fastcallTestStructAsArgument;
        private UnmanagedProcessFunctionPointer<int, TestContext, TestContext, TestContext, TestContext, TestContext, TestContext>? fastcallTestStructsAsArguments;
        private UnmanagedProcessFunctionPointer<int, UnmanagedPointer>? fastcallTestStructPtrAsArgument;
        private UnmanagedProcessFunctionPointer<int, UnmanagedPointer, UnmanagedPointer, UnmanagedPointer>? fastcallTestStructPtrsAsArguments;

        public FastCallTests(Process in_process, SymbolResolver in_sr) : base(in_process)
        {
            fastcallTestNoArguments = new(Process, Process.ToASLR(in_sr.GetProcedureAddress("fastcallTestNoArguments")), ECallingConvention.FastCall);
            fastcallTestSumOfArguments = new(Process, Process.ToASLR(in_sr.GetProcedureAddress("fastcallTestSumOfArguments")), ECallingConvention.FastCall);
            fastcallTestReturnStruct = new(Process, Process.ToASLR(in_sr.GetProcedureAddress("fastcallTestReturnStruct")), ECallingConvention.FastCall);
            fastcallTestReturnStructPtr = new(Process, Process.ToASLR(in_sr.GetProcedureAddress("fastcallTestReturnStructPtr")), ECallingConvention.FastCall);
            fastcallTestStructAsArgument = new(Process, Process.ToASLR(in_sr.GetProcedureAddress("fastcallTestStructAsArgument")), ECallingConvention.FastCall);
            fastcallTestStructsAsArguments = new(Process, Process.ToASLR(in_sr.GetProcedureAddress("fastcallTestStructsAsArguments")), Process.Is64Bit() ? ECallingConvention.UserCall : ECallingConvention.FastCall);
            fastcallTestStructPtrAsArgument = new(Process, Process.ToASLR(in_sr.GetProcedureAddress("fastcallTestStructPtrAsArgument")), ECallingConvention.FastCall);
            fastcallTestStructPtrsAsArguments = new(Process, Process.ToASLR(in_sr.GetProcedureAddress("fastcallTestStructPtrsAsArguments")), ECallingConvention.FastCall);
        }

        public bool fastcallTestNoArguments_ShouldReturnTrue()
        {
            return fastcallTestNoArguments!.Invoke() == 1;
        }

        public bool fastcallTestSumOfArguments_ShouldReturnCorrectSum()
        {
            return fastcallTestSumOfArguments!.Invoke(1, 2, 3) == 6;
        }

        public bool fastcallTestReturnStruct_ShouldReturnCorrectStruct()
        {
            var in_ctx = new UnmanagedPointer(Process, new TestContext(1, 2, 3));
            var out_ctx = fastcallTestReturnStruct!.Invoke(in_ctx);

            var result = new TestContext(1, 2, 3).Equals(out_ctx.Get<TestContext>(Process));

            in_ctx.Free(Process);

            return result;
        }

        public bool fastcallTestReturnStructPtr_ShouldReturnCorrectStruct()
        {
            var ctx = fastcallTestReturnStructPtr!.Invoke();

            return new TestContext(1, 2, 3).Equals(ctx.Get<TestContext>(Process));
        }

        public bool fastcallTestStructAsArgument_ShouldReturnCorrectSum()
        {
            return fastcallTestStructAsArgument!.Invoke(new TestContext(1, 2, 3)) == 6;
        }

        public bool fastcallTestStructsAsArguments_ShouldReturnCorrectSum()
        {
            var testCtx = new TestContext(1, 2, 3);

            // Set up context for 64-bit.
            fastcallTestStructsAsArguments!.Prefix += (sender, ctx) =>
            {
                for (int i = 5; i >= 0; i--)
                {
                    var ptr = ctx.StackWrite(testCtx);

                    switch (i)
                    {
                        case 2: ctx.SetGPR(EBaseRegister.R9, ptr);  break;
                        case 3: ctx.SetGPR(EBaseRegister.R8, ptr);  break;
                        case 4: ctx.SetGPR(EBaseRegister.RDX, ptr); break;
                        case 5: ctx.SetGPR(EBaseRegister.RCX, ptr); break;
                    }
                }

                // Reserve unknown 16 bytes before pointers to last structs.
                var rsp = ctx.SetStackPointer(-0x10);

                // Write pointers to remaining structs.
                ctx.StackWrite(rsp + 0x10);
                ctx.StackWrite(rsp + 0x20);

                // Reserve shadow space.
                ctx.SetStackPointer(-0x20);
            };

            /* Passing arguments is optional here if context is set up manually.
               They're used here for the x86 test, which is standard __fastcall. */
            return fastcallTestStructsAsArguments!.Invoke(testCtx, testCtx, testCtx, testCtx, testCtx, testCtx) == 36;
        }

        public bool fastcallTestStructPtrAsArgument_ShouldReturnCorrectSum()
        {
            var ctx = new UnmanagedPointer(Process, new TestContext(1, 2, 3));
            var result = fastcallTestStructPtrAsArgument!.Invoke(ctx) == 6;

            ctx.Free(Process);

            return result;
        }

        public bool fastcallTestStructPtrsAsArguments_ShouldReturnCorrectSum()
        {
            var ctx = new UnmanagedPointer(Process, new TestContext(1, 2, 3));
            var result = fastcallTestStructPtrsAsArguments!.Invoke(ctx, ctx, ctx) == 18;

            ctx.Free(Process);

            return result;
        }

        public override Func<bool>[] GetTests()
        {
            LoggerService.Warning($"__fastcall Tests ({Process.GetArchitectureName()}) --------------\n");

            return
            [
                fastcallTestNoArguments_ShouldReturnTrue,
                fastcallTestSumOfArguments_ShouldReturnCorrectSum,
                fastcallTestReturnStruct_ShouldReturnCorrectStruct,
                fastcallTestReturnStructPtr_ShouldReturnCorrectStruct,
                fastcallTestStructAsArgument_ShouldReturnCorrectSum,
                fastcallTestStructsAsArguments_ShouldReturnCorrectSum,
                fastcallTestStructPtrAsArgument_ShouldReturnCorrectSum,
                fastcallTestStructPtrsAsArguments_ShouldReturnCorrectSum
            ];
        }

        public override void Dispose()
        {
#if DEBUG
            LoggerService.Warning($"__fastcall Cleanup ({Process.GetArchitectureName()}) ------------\n");
#endif

            fastcallTestNoArguments?.Dispose();
            fastcallTestSumOfArguments?.Dispose();
            fastcallTestReturnStruct?.Dispose();
            fastcallTestReturnStructPtr?.Dispose();
            fastcallTestStructAsArgument?.Dispose();
            fastcallTestStructsAsArguments?.Dispose();
            fastcallTestStructPtrAsArgument?.Dispose();
            fastcallTestStructPtrsAsArguments?.Dispose();

#if DEBUG
            LoggerService.WriteLine();
#endif
        }
    }
}
