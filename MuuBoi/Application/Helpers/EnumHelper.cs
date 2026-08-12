using MuuBoi.Application.DTOs;
using System.ComponentModel;
using System.Reflection;

namespace MuuBoi.Application.Helpers
{
    public static class EnumHelper
    {
        public static string GetDescription(this Enum value)
        {
            var field = value.GetType().GetField(value.ToString());
            var attr = field?.GetCustomAttribute<DescriptionAttribute>();
            return attr?.Description ?? value.ToString();
        }

        public static IEnumerable<LookupDto> ToLookup<TEnum>() where TEnum : struct, Enum =>
            Enum.GetValues<TEnum>().Select(e => new LookupDto
            {
                Value = Convert.ToInt32(e),
                Label = e.GetDescription()
            });
    }
}
