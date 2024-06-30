using ProcessExtensions.Interop.Attributes;
using System.Reflection;

namespace ProcessExtensions.Extensions.Internal
{
    internal static class TypeExtensions
    {
        public static bool IsStruct(this Type in_type)
        {
            var isMarshalAsRegister = in_type.GetCustomAttribute<MarshalAsRegister>() != null;

            if (isMarshalAsRegister)
                return false;

            return in_type.IsValueType && !in_type.IsPrimitive && !in_type.IsEnum;
        }
    }
}
