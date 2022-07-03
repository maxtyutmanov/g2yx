using g2yx.Models;
using Google.Apis.Auth.OAuth2;
using Google.Apis.PhotosLibrary.v1;
using Google.Apis.PhotosLibrary.v1.Data;
using Google.Apis.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace g2yx.Services
{
    public class GooglePhotosApi
    {
        private readonly GoogleCredential _creds;

        public GooglePhotosApi(GoogleCredential creds)
        {
            _creds = creds;
        }

        public async Task<IReadOnlyCollection<string>> GetAllMediaItemIdsOrderedByCreationDate(CancellationToken ct)
        {
            var plSvc = new PhotosLibraryService(new BaseClientService.Initializer
            {
                HttpClientInitializer = _creds
            });

            var allItemIds = new List<string>();
            string nextPageToken = null;

            do
            {
                var itemsRequest = plSvc.MediaItems.Search(new SearchMediaItemsRequest
                {
                    PageSize = 10000,
                    PageToken = nextPageToken,
                });

                var itemsResponse = await itemsRequest.ExecuteAsync(ct);
                nextPageToken = itemsResponse.NextPageToken;

                allItemIds.AddRange(itemsResponse.MediaItems.Select(x => x.Id));
            } while (!string.IsNullOrEmpty(nextPageToken));

            return allItemIds;
        }

        public async IAsyncEnumerable<AlbumPhoto> ReadAlbumPhotos(string albumId, CancellationToken ct)
        {
            var plSvc = new PhotosLibraryService(new BaseClientService.Initializer
            {
                HttpClientInitializer = _creds
            });

            var allItemIds = new List<string>();
            string nextPageToken = null;

            do
            {
                var itemsRequest = plSvc.MediaItems.Search(new SearchMediaItemsRequest
                {
                    AlbumId = albumId,
                    PageSize = 100,
                    PageToken = nextPageToken
                });

                var itemsResponse = await itemsRequest.ExecuteAsync(ct);
                nextPageToken = itemsResponse.NextPageToken;

                allItemIds.AddRange(itemsResponse.MediaItems.Select(x => x.Id));
            } while (!string.IsNullOrEmpty(nextPageToken));

            using var http = new HttpClient();

            foreach (var photoId in allItemIds)
            {
                var photo = await plSvc.MediaItems.Get(photoId).ExecuteAsync(ct);

                if (photo.MediaMetadata.Photo == null)
                    continue;

                var width = photo.MediaMetadata.Width;
                var height = photo.MediaMetadata.Height;

                var photoStream = await http.GetStreamAsync(photo.BaseUrl + $"=w{width}-h{height}");
                using var ms = new MemoryStream();
                await photoStream.CopyToAsync(ms);

                yield return new AlbumPhoto
                {
                    Name = photo.Filename,
                    Content = ms.ToArray()
                };
            }
        }
    }
}
