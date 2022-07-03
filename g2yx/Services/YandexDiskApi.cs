using g2yx.Models;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace g2yx.Services
{
    public class YandexDiskApi : IDisposable
    {
        private const string GPhotoFolderPath = "/Google Photo All";
        private const string GPhotoLockFilePath = "/Google Photo All/lock";
        public static readonly TimeSpan LockExpiration = TimeSpan.FromMinutes(5);
        
        private readonly HttpClient _http;

        public YandexDiskApi(string accessToken)
        {
            _http = new HttpClient();
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("OAuth", accessToken);
        }

        public async Task<bool> IsLocked(CancellationToken ct)
        {
            var lockFileBytes = await DownloadFile(GPhotoLockFilePath, ct);
            if (lockFileBytes != null)
            {
                var ticksStr = Encoding.UTF8.GetString(lockFileBytes);
                var ticks = long.Parse(ticksStr);
                var lockUpdateStamp = new DateTime(ticks, DateTimeKind.Utc);
                return (DateTime.UtcNow - lockUpdateStamp) < LockExpiration;
            }
            return false;
        }

        public async Task EnsureLocked(CancellationToken ct)
        {
            var lockFileBytes = Encoding.UTF8.GetBytes(DateTime.UtcNow.Ticks.ToString());
            await UploadFile(GPhotoLockFilePath, lockFileBytes, ct);
        }

        public async Task<SyncProgress> GetSyncProgress(CancellationToken ct)
        {
            var lastSyncedDate = await GetLastSyncedDate(ct);
            var isRunning = await IsLocked(ct);

            return new SyncProgress
            {
                IsRunning = isRunning,
                LastSyncedDate = lastSyncedDate
            };
        }

        public async Task<DateTime?> GetLastSyncedDate(CancellationToken ct)
        {
            var customProps = await GetCustomProps(GPhotoFolderPath, ct);
            if (customProps.TryGetValue("last_synced_date", out var lastSyncedDateToken))
            {
                var ticks = lastSyncedDateToken.Value<long>();
                return new DateTime(ticks, DateTimeKind.Utc);
            }

            return null;
        }

        public async Task SetLastSyncedDate(DateTime lastSyncedDate, CancellationToken ct)
        {
            var content = new
            {
                custom_properties = new
                {
                    last_synced_date = lastSyncedDate.Ticks
                }
            };
            var contentStr = JsonConvert.SerializeObject(content);

            var response = await _http.PatchAsync(
                $"https://cloud-api.yandex.net/v1/disk/resources?path={Encode(GPhotoFolderPath)}",
                new StringContent(contentStr, Encoding.UTF8, "application/json"),
                ct);

            response.EnsureSuccessStatusCode();
        }

        public async Task UploadPhoto(AlbumPhoto photo, CancellationToken ct)
        {
            var photoEncodedPath = Encode($"{GPhotoFolderPath}/{Sanitize(photo.Name)}");
            await UploadFile(photoEncodedPath, photo.Content, ct);
        }

        public void Dispose()
        {
            _http.Dispose();
        }

        private async Task<byte[]> DownloadFile(string path, CancellationToken ct)
        {
            var yResp = await GetJObject($"https://cloud-api.yandex.net/v1/disk/resources/download?path={path}", ct);
            if (yResp == null)
                return null;

            var downloadUrl = yResp["href"].Value<string>();
            var content = await _http.GetByteArrayAsync(downloadUrl, ct);
            return content;
        }

        private async Task UploadFile(string path, byte[] bytes, CancellationToken ct)
        {
            var yResp = await GetJObject($"https://cloud-api.yandex.net/v1/disk/resources/upload?path={path}&overwrite=true", ct);
            var uploadUrl = yResp["href"].Value<string>();
            var result = await _http.PutAsync(uploadUrl, new ByteArrayContent(bytes));
            result.EnsureSuccessStatusCode();
        }

        private async Task<JObject> GetCustomProps(string path, CancellationToken ct)
        {
            var response = await GetJObject(
                $"https://cloud-api.yandex.net/v1/disk/resources?path={Encode(path)}&fields=custom_properties",
                ct);

            var customProps = (JObject)response["custom_properties"];
            return customProps ?? new JObject();
        }

        private async Task<JObject> GetJObject(string url, CancellationToken ct)
        {
            var response = await _http.GetAsync(url, ct);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return null;

            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync(ct);

            return JsonConvert.DeserializeObject<JObject>(responseContent);
        }

        private static string Sanitize(string pathElement)
        {
            return pathElement.Replace("/", "\\/");
        }

        private static string Encode(string path)
        {
            return WebUtility.UrlEncode(path);
        }
    }
}
