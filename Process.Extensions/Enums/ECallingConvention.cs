using ProcessExtensions.Interop;

namespace ProcessExtensions.Enums
{
    /// <summary>
    /// An enum representing the supported calling conventions for remote code execution.
    /// </summary>
    public enum ECallingConvention
    {
        /// <summary>
        /// The default calling convention for Windows API functions.
        /// <para>This will default to <see cref="StdCall"/> for 32-bit and <see cref="FastCall"/> for 64-bit.</para>
        /// </summary>
        Windows,

        /// <summary>
        /// The default calling convention for C functions on 32-bit platforms.
        /// <para>Under this convention, the caller cleans the stack after the call has returned.</para>
        /// <para>This convention supports variadic arguments.</para>
        /// </summary>
        Cdecl,

        /// <summary>
        /// The default calling convention for Windows API functions on 32-bit platforms.
        /// <para>Under this convention, the callee cleans the stack before the call returns.</para>
        /// </summary>
        StdCall,

        /// <summary>
        /// The default calling convention for C++ functions as part of a class on 32-bit platforms.
        /// <para>Under this convention, the first argument to the function is a pointer to the class that it originates from (<c>this</c>), stored in the <c>ECX</c> register.</para>
        /// <para>The rest of the arguments are pushed to the stack.</para>
        /// </summary>
        ThisCall,

        /// <summary>
        /// The default calling convention for 64-bit platforms, but is also supported on 32-bit platforms.
        /// <para>Under this convention for 32-bit, the first two arguments are stored in registers <c>ECX</c> and <c>EDX</c>.</para>
        /// <para>Under this convention for 64-bit, the first four arguments are stored in registers <c>RCX</c>, <c>RDX</c>, <c>R8</c> and <c>R9</c>.</para>
        /// <para>The rest of the arguments are pushed to the stack.</para>
        /// </summary>
        FastCall,

        /// <summary>
        /// The calling convention is explicitly set up by the user.
        /// <para>Subscribe to the <c>Prefix</c> event in <see cref="UnmanagedProcessFunctionPointer"/> to set the CPU context before the function call.</para>
        /// </summary>
        UserCall
    }
}
