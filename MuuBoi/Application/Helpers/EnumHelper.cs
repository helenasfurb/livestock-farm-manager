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

        public static EnumValueDto ToEnumValue(this Enum value) =>
            new EnumValueDto { Value = Convert.ToInt32(value), Label = value.GetDescription() };

        public static IEnumerable<LookupDto> ToLookup<TEnum>() where TEnum : struct, Enum =>
            Enum.GetValues<TEnum>().Select(e => new LookupDto
            {
                Value = Convert.ToInt32(e),
                Label = e.GetDescription()
            });

        /// <summary>Decomposes a [Flags] enum value into the individual set members as EnumValueDto.</summary>
        public static IEnumerable<EnumValueDto> ToFlagValues<TEnum>(TEnum value) where TEnum : struct, Enum
        {
            var bits = Convert.ToInt32(value);
            return Enum.GetValues<TEnum>()
                .Where(f =>
                {
                    var fb = Convert.ToInt32(f);
                    return fb != 0 && (bits & fb) == fb;
                })
                .Select(f => new EnumValueDto { Value = Convert.ToInt32(f), Label = f.GetDescription() });
        }
    }
}
