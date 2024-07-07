namespace ProcessExtensions.Enums
{
    /// <summary>
    /// An enum representing all possible jump lengths.
    /// </summary>
    public enum EJumpType : sbyte
    {
        Unknown = -1,
        ShortCond,
        NearJmp,
        NearCond,
        LongJmp
    }
}
