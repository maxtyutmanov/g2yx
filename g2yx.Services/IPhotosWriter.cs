using g2yx.Models;
using System.Threading;
using System.Threading.Tasks;

namespace g2yx.Services
{
    public interface IPhotosWriter
    {
        Task<bool> IsLocked(CancellationToken ct);
        Task EnsureLocked(CancellationToken ct);
        Task<SyncProgress> GetSyncProgress(CancellationToken ct);
        Task<string> GetSyncPointer(CancellationToken ct);
        Task SetSyncPointer(string syncPointer, CancellationToken ct);
        Task Write(AlbumPhoto photo, CancellationToken ct);
    }
}
