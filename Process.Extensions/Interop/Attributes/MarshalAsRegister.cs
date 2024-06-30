namespace ProcessExtensions.Interop.Attributes
{
    /// <summary>
    /// Indicates that a struct can be marshalled to fit into a register.
    /// </summary>
    [AttributeUsage(AttributeTargets.Struct)]
    public class MarshalAsRegister : Attribute { }
}
