using MuuBoi.Domain.Models;

namespace MuuBoi.Application.Helpers
{
    public static class AnimalPhotoPaths
    {
        public static string? PhotoUrl(int animalId, string? photoHash) =>
            photoHash == null ? null : $"/api/animals/{animalId}/photo?v={photoHash}";

        public static string? ThumbnailUrl(int animalId, string? photoHash) =>
            photoHash == null ? null : $"/api/animals/{animalId}/photo/thumbnail?v={photoHash}";

        public static string PropertyPrefix(Guid propertyId) => $"{propertyId}/";

        public static string PhotoKey(Animal animal, string photoHash) =>
            $"{PropertyPrefix(animal.PropertyId)}animals/{animal.SyncId}/{photoHash}/photo";

        public static string ThumbnailKey(Animal animal, string photoHash) =>
            $"{PropertyPrefix(animal.PropertyId)}animals/{animal.SyncId}/{photoHash}/thumbnail";
    }
}
