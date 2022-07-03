using g2yx.Models;
using Google.Apis.Auth.OAuth2;
using Google.Apis.PhotosLibrary.v1;
using Google.Apis.PhotosLibrary.v1.Data;
using Google.Apis.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace g2yx.Services
{
    public class GooglePhotosApi : IDisposable
    {
        private readonly PhotosLibraryService _plSvc;

        public GooglePhotosApi(GoogleCredential creds)
        {
            _plSvc = new PhotosLibraryService(new BaseClientService.Initializer
            {
                HttpClientInitializer = creds
            });
        }

        public async IAsyncEnumerable<MediaItem> GetAllItems(DateTime startDate, string startEtag, CancellationToken ct)
        {
            bool navigatedToStartEtag = (startEtag == null);
            string nextPageToken = null;

            do
            {
                var searchParams = new SearchMediaItemsRequest
                {
                    PageSize = 100,
                    PageToken = nextPageToken,
                };

                searchParams.Filters = new Filters()
                {
                    DateFilter = new DateFilter
                    {
                        Ranges = new List<DateRange>()
                        {
                            new DateRange() { StartDate = DateTimeToGoogleDate(startDate.Date), EndDate = DateTimeToGoogleDate(DateTime.UtcNow.Date) }
                        }
                    }
                };

                var itemsRequest = _plSvc.MediaItems.Search(searchParams);
                await AddOrderByDateToSearchRequest(itemsRequest);

                var itemsResponse = await itemsRequest.ExecuteAsync(ct);
                nextPageToken = itemsResponse.NextPageToken;

                foreach (var item in itemsResponse.MediaItems)
                {
                    if (navigatedToStartEtag)
                    {
                        yield return item;
                    }
                    else if (item.ETag == startEtag)
                    {
                        navigatedToStartEtag = true;
                    }
                }

            } while (!string.IsNullOrEmpty(nextPageToken));
        }

        private async Task AddOrderByDateToSearchRequest(MediaItemsResource.SearchRequest request)
        {
            var httpReq = request.CreateRequest(overrideGZipEnabled: false);
            var contentStr = await httpReq.Content.ReadAsStringAsync();

            var content = JsonConvert.DeserializeObject<JObject>(contentStr);
            content["orderBy"] = "MediaMetadata.creation_time";

            request.ModifyRequest = req =>
            {
                req.Content = new StringContent(JsonConvert.SerializeObject(content), Encoding.UTF8, "application/json");
            };
        }

        public async IAsyncEnumerable<AlbumPhoto> ReadPhotos(IAsyncEnumerable<MediaItem> mediaItems, [EnumeratorCancellation] CancellationToken ct)
        {
            using var http = new HttpClient();

            await foreach (var item in mediaItems.WithCancellation(ct))
            {
                if (item.MediaMetadata.Photo == null)
                    continue;

                var width = item.MediaMetadata.Width;
                var height = item.MediaMetadata.Height;

                var photoStream = await http.GetStreamAsync(item.BaseUrl + $"=w{width}-h{height}", ct);
                using var ms = new MemoryStream();
                await photoStream.CopyToAsync(ms, ct);

                yield return new AlbumPhoto
                {
                    Name = item.Filename,
                    Content = ms.ToArray(),
                    CreationDateTime = (DateTime)item.MediaMetadata.CreationTime,
                    Etag = item.ETag
                };
            }
        }

        public async IAsyncEnumerable<AlbumPhoto> ReadAlbumPhotos(string albumId, [EnumeratorCancellation] CancellationToken ct)
        {
            var allItemIds = new List<string>();
            string nextPageToken = null;

            do
            {
                var itemsRequest = _plSvc.MediaItems.Search(new SearchMediaItemsRequest
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
                var photo = await _plSvc.MediaItems.Get(photoId).ExecuteAsync(ct);

                if (photo.MediaMetadata.Photo == null)
                    continue;

                var width = photo.MediaMetadata.Width;
                var height = photo.MediaMetadata.Height;

                var photoStream = await http.GetStreamAsync(photo.BaseUrl + $"=w{width}-h{height}", ct);
                using var ms = new MemoryStream();
                await photoStream.CopyToAsync(ms, ct);

                yield return new AlbumPhoto
                {
                    Name = photo.Filename,
                    Content = ms.ToArray(),
                    CreationDateTime = (DateTime)photo.MediaMetadata.CreationTime,
                    Etag = photo.ETag
                };
            }
        }

        public void Dispose()
        {
            _plSvc.Dispose();
        }

        private Date DateTimeToGoogleDate(DateTime dateTime)
        {
            return new Date() { Day = dateTime.Day, Month = dateTime.Month, Year = dateTime.Year };
        }
    }
}
