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
    public class GooglePhotosApiReader : IPhotosReader, IDisposable
    {
        private readonly PhotosLibraryService _plSvc;

        public GooglePhotosApiReader(GoogleCredential creds)
        {
            _plSvc = new PhotosLibraryService(new BaseClientService.Initializer
            {
                HttpClientInitializer = creds
            });
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

        public async IAsyncEnumerable<AlbumPhoto> ReadPhotos(string syncPointer, [EnumeratorCancellation] CancellationToken ct)
        {
            DateTime? startDate = null;

            if (!string.IsNullOrEmpty(syncPointer))
            {
                if (!long.TryParse(syncPointer, out var startTicks))
                {
                    throw new FormatException($"Sync pointer value {syncPointer} is not recognized");
                }

                startDate = new DateTime(startTicks, DateTimeKind.Utc);
            }

            using var http = new HttpClient();

            await foreach (var item in GetAllItems(startDate ?? new DateTime(2000, 1, 1), ct).WithCancellation(ct))
            {
                if (item.MediaMetadata.Photo == null)
                    continue;

                var width = item.MediaMetadata.Width;
                var height = item.MediaMetadata.Height;

                var photoStream = await http.GetStreamAsync(item.BaseUrl + $"=w{width}-h{height}=d");
                using var ms = new MemoryStream();
                await photoStream.CopyToAsync(ms, ct);

                syncPointer = ((DateTime)item.MediaMetadata.CreationTime).Ticks.ToString();

                yield return new AlbumPhoto(ms.ToArray(), item.Filename, syncPointer);
            }
        }

        private async IAsyncEnumerable<MediaItem> GetAllItems(DateTime startDate, [EnumeratorCancellation] CancellationToken ct)
        {
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
                    yield return item;
                }

            } while (!string.IsNullOrEmpty(nextPageToken));
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
