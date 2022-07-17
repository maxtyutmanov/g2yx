using g2yx.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace g2yx.Services
{
    public interface IPhotosReader
    {
        IAsyncEnumerable<AlbumPhoto> ReadPhotos(string syncPointer, [EnumeratorCancellation] CancellationToken ct);
    }
}
