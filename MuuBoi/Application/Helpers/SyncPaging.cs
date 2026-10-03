using System.Buffers.Binary;
using System.Globalization;
using MuuBoi.Application.DTOs;
using MuuBoi.Domain.Models;

namespace MuuBoi.Application.Helpers
{
    public static class SyncPaging
    {
        public const int DefaultLimit = 500;
        public const int MaxLimit = 500;

        public static int ResolveLimit(int? requested)
        {
            if (!requested.HasValue || requested.Value < 1)
                return DefaultLimit;

            return Math.Min(requested.Value, MaxLimit);
        }

        public static bool TryDecodeCursor(string? cursor, out ulong value)
        {
            if (string.IsNullOrWhiteSpace(cursor))
            {
                value = 0;
                return true;
            }

            return ulong.TryParse(cursor, NumberStyles.None, CultureInfo.InvariantCulture, out value);
        }

        public static byte[] ToRowVersionBytes(ulong value)
        {
            var bytes = new byte[8];
            BinaryPrimitives.WriteUInt64BigEndian(bytes, value);
            return bytes;
        }

        public static ulong FromRowVersionBytes(byte[] rowVersion)
        {
            return BinaryPrimitives.ReadUInt64BigEndian(rowVersion);
        }

        public static SyncPageDto<TDto> BuildPage<TEntity, TDto>(
            IReadOnlyList<TEntity> fetched, int limit, ulong since, Func<TEntity, TDto> map)
            where TEntity : ISyncable
        {
            var page = fetched.Take(limit).ToList();
            var next = page.Count > 0 ? FromRowVersionBytes(page[^1].RowVersion) : since;

            return new SyncPageDto<TDto>
            {
                Items = page.Select(map).ToList(),
                NextCursor = next.ToString(CultureInfo.InvariantCulture),
                HasMore = fetched.Count > limit
            };
        }
    }
}
