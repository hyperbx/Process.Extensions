﻿using ProcessExtensions.Logger;
using System.Diagnostics;

namespace ProcessExtensions.Tests
{
    internal class TestBase(Process in_process)
    {
        protected Process Process = in_process;

        public virtual Func<bool>[] GetTests()
        {
            throw new NotImplementedException();
        }

        public virtual bool RunTests()
        {
            var result = true;

            foreach (var test in GetTests())
            {
                LoggerService.Log($"Running test: {test.Method.Name}...");

#if !DEBUG
                try
                {
#endif
                    if (test())
                    {
                        LoggerService.Utility("PASS");
                    }
                    else
                    {
                        result = false;
                        LoggerService.Error("FAIL");
                    }
#if !DEBUG
                }
                catch (Exception out_ex)
                {
                    result = false;
                    LoggerService.Error(out_ex.Message);
                    LoggerService.Error("FAIL");
                }
#endif

                LoggerService.WriteLine();

                if (!result)
                    break;
            }

            return result;
        }
    }
}
