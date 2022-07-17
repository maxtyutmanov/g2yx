using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace g2yx.Services
{
    public class SyncJob
    {
        private const int ProgressUpdatePeriod = 25;
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
            if (await writer.IsLocked(ct))
            {
                _logger.LogWarning("Writer is locked. Sync will not be performed");
                return;
            }

            await writer.EnsureLocked(ct);
            var lockLastUpdatedAt = DateTime.UtcNow;

            var syncPointer = await writer.GetSyncPointer(ct);

            var counter = 0;

            await foreach (var photo in reader.ReadPhotos(syncPointer, ct).WithCancellation(ct))
            {
                await writer.UploadPhoto(photo, ct);
                if (++counter % ProgressUpdatePeriod == 0)
                {
                    _logger.LogInformation("Synchronized {SyncedPhotosCount} photos", counter);
                    await writer.SetSyncPointer(photo.SyncPointer, ct);
                }

                if ((DateTime.UtcNow - lockLastUpdatedAt).TotalSeconds > YandexDiskApi.LockExpiration.TotalSeconds / 2)
                {
                    _logger.LogInformation("Refreshing the lock for yandex disk");
                    // refresh lock if we're halfway to expiration
                    await writer.EnsureLocked(ct);
                }
            }
        }
    }
}
