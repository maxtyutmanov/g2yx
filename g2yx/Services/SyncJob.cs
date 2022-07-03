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

        public async Task Execute(string gAccessToken, string yAccessToken, CancellationToken ct)
        {
            var gCreds = GoogleCredential.FromAccessToken(gAccessToken);
            var gApi = new GooglePhotosApi(gCreds);
            var yApi = new YandexDiskApi(yAccessToken);

            if (await yApi.IsLocked(ct))
            {
                return;
            }

            await yApi.EnsureLocked(ct);
            var lockLastUpdatedAt = DateTime.UtcNow;

            var lastSyncedDate = await yApi.GetLastSyncedDate(ct);

            var counter = 0;

            var notYetSyncedItems = gApi.GetAllItems(lastSyncedDate ?? DateTime.MinValue, ct);
            await foreach (var photo in gApi.ReadPhotos(notYetSyncedItems, ct).WithCancellation(ct))
            {
                await yApi.UploadPhoto(photo, ct);
                if (++counter % ProgressUpdatePeriod == 0)
                    await yApi.SetLastSyncedDate(photo.CreationDateTime, ct);

                if ((DateTime.UtcNow - lockLastUpdatedAt).TotalSeconds > YandexDiskApi.LockExpiration.TotalSeconds / 2)
                {
                    // refresh lock if we're halfway to expiration
                    await yApi.EnsureLocked(ct);
                }
            }
        }
    }
}
