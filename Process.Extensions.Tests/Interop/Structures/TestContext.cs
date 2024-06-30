using System.Diagnostics.CodeAnalysis;

namespace ProcessExtensions.Interop.Structures
{
    internal struct TestContext
    {
        public int A;
        public int B;
        public int C;

        public TestContext() { }

        public TestContext(int in_a, int in_b, int in_c)
        {
            A = in_a;
            B = in_b;
            C = in_c;
        }

        public override bool Equals([NotNullWhen(true)] object? obj)
        {
            if (obj is TestContext ctx)
            {
                if (A == ctx.A && B == ctx.B && C == ctx.C)
                    return true;

                return false;
            }

            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            throw new NotImplementedException();
        }
    }
}
