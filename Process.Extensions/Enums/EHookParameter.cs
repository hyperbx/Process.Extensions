namespace ProcessExtensions.Enums
{
    /// <summary>
    /// An enum representing how mid-ASM hooks will jump to and from their respective code.
    /// </summary>
    public enum EHookParameter
    {
        /// <summary>
        /// Execute code via a <b>jmp</b> instruction, using another to jump back to the original code.
        /// </summary>
        Jump,

        /// <summary>
        /// Execute code via a <b>call</b> instruction, using <b>ret</b> to return back to the original code.
        /// </summary>
        Call
    }
}
