using ProcessExtensions.Enums;
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

        public static bool IsMatchingTypeCode(this Type in_type, ETypeCode in_typeCode)
        {
            // Handle custom type codes.
            if (in_typeCode == ETypeCode.WString && in_type.Equals(typeof(string)))
                return true;

            return Type.GetTypeCode(in_type) == (TypeCode)in_typeCode;
        }
    }
}
