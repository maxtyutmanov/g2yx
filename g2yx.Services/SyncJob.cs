using g2yx.Models;
using g2yx.Services.Utils;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace g2yx.Services
{
    public class SyncJob
    {
        private static readonly TimeSpan ProgressUpdatePeriod = TimeSpan.FromMinutes(1);
        private static readonly int DegreeOfUploadParallelism = 10;

        private readonly ILogger<SyncJob> _logger;

        public SyncJob(ILogger<SyncJob> logger)
        {
            _logger = logger;
        }

        public async Task SyncFromGTakeout(string takeoutDir, string yAccessToken, CancellationToken ct)
        {
            var takeoutReader = new GoogleTakeoutReader(takeoutDir);
            using var yApi = new YandexDiskApi(yAccessToken);

            await Sync(takeoutReader, yApi, ct);
        }

        public async Task SyncFromGPhotoApi(string gAccessToken, string yAccessToken, CancellationToken ct)
        {
            var gCreds = GoogleCredential.FromAccessToken(gAccessToken);
            using var gApi = new GooglePhotosApiReader(gCreds);
            using var yApi = new YandexDiskApi(yAccessToken);

            await Sync(gApi, yApi, ct);
        }

        private async Task Sync(IPhotosReader reader, IPhotosWriter writer, CancellationToken ct)
        {
            var lockLastUpdatedAt = DateTime.UtcNow;
            var progressLastUpdatedAt = DateTime.UtcNow;

            if (await writer.IsLocked(ct))
            {
                _logger.LogWarning("Writer is locked. Sync will not be performed");
                return;
            }

            await writer.EnsureLocked(ct);
            
            var syncPointer = await writer.GetSyncPointer(ct);

            _logger.LogInformation("Starting sync. Previous sync pointer value: {SyncPointer}", syncPointer);

            var processedPhotosStream = reader.Read(syncPointer, ct)
                .ProcessInParallel(UploadOnePhoto, DegreeOfUploadParallelism, ct)
                .MakeOrdered(ct)
                .WithCancellation(ct);

            await foreach (var photo in processedPhotosStream)
            {
                if ((DateTime.UtcNow - progressLastUpdatedAt) > ProgressUpdatePeriod || photo.Content.Length > 20_000_000)
                {
                    _logger.LogInformation("Updating progress (setting current sync pointer value to {SyncPointer})", photo.SyncPointer);
                    await writer.SetSyncPointer(photo.SyncPointer.ToString(), ct);
                    progressLastUpdatedAt = DateTime.UtcNow;
                }
            }

            async Task UploadOnePhoto(AlbumPhoto photo)
            {
                if (photo.Content.Length > 20_000_000)
                {
                    _logger.LogWarning("Uploading a large file (~{SizeInMb} MB) {FileName}", photo.Content.Length / 1_000_000, photo.Name);
                }

                if (photo.Folder != null)
                    await writer.EnsureSubfolderCreated(photo.Folder, ct);

                await writer.Write(photo, ct);

                // refresh lock if we're halfway to expiration
                if ((DateTime.UtcNow - lockLastUpdatedAt).TotalSeconds > YandexDiskApi.LockExpiration.TotalSeconds / 2)
                {
                    _logger.LogInformation("Refreshing the lock for yandex disk");
                    await writer.EnsureLocked(ct);
                    lockLastUpdatedAt = DateTime.UtcNow;
                }
            }
        }
    }
}
