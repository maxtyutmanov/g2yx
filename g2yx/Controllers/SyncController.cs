using g2yx.Services;
using Google.Apis.Auth.AspNetCore3;
using Google.Apis.Auth.OAuth2;
using Google.Apis.PhotosLibrary.v1;
using Google.Apis.Services;
using Hangfire;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace g2yx.Controllers
{
    [GoogleScopedAuthorize("https://www.googleapis.com/auth/photoslibrary.readonly")]
    public class SyncController : Controller
    {
        private readonly IGoogleAuthProvider _googleAuth;

        public SyncController(IGoogleAuthProvider googleAuth)
        {
            _googleAuth = googleAuth;
        }

        [HttpPost("Index/{albumId}")]
        public async Task<IActionResult> Index([FromRoute] string albumId, CancellationToken ct)
        {
            await SyncAlbum(albumId, ct);
            return Ok();
        }

        private async Task SyncAlbum(string albumId, CancellationToken ct)
        {
            var creds = await _googleAuth.GetCredentialAsync(cancellationToken: ct);
            var gAccessToken = await creds.UnderlyingCredential.GetAccessTokenForRequestAsync(cancellationToken: ct);
            var yAccessToken = await HttpContext.GetTokenAsync("yandex_cookie", "access_token");

            BackgroundJob.Enqueue(() => SyncAlbum(albumId, gAccessToken, yAccessToken, default));
        }

        public async Task SyncAlbum(string albumId, string gAccessToken, string yAccessToken, CancellationToken ct = default)
        {
            var creds = GoogleCredential.FromAccessToken(gAccessToken);
            var googleApi = new GooglePhotosApi(creds);
            
            using var plSvc = new PhotosLibraryService(new BaseClientService.Initializer
            {
                HttpClientInitializer = creds
            });

            var album = await plSvc.Albums.Get(albumId).ExecuteAsync(ct);

            var sanitizedAlbumTitle = album.Title.Replace("/", "\\/");

            using var http = new HttpClient();
            http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("OAuth", yAccessToken);

            var encodedPath = WebUtility.UrlEncode($"/Albums/{sanitizedAlbumTitle}");
            var yResp = await http.PutAsync($"https://cloud-api.yandex.net/v1/disk/resources?path={encodedPath}", null);

            var yRespContent = await yResp.Content?.ReadAsStringAsync();

            if (yResp.StatusCode != HttpStatusCode.OK)
            {
                // folder already exists, just skip
            }

            await foreach (var photo in googleApi.ReadAlbumPhotos(albumId, ct).WithCancellation(ct))
            {
                var sanitizedName = photo.Name.Replace("/", "\\/");
                var photoEncodedPath = WebUtility.UrlEncode($"/Albums/{sanitizedAlbumTitle}/{sanitizedName}");
                yRespContent = await http.GetStringAsync($"https://cloud-api.yandex.net/v1/disk/resources/upload?path={photoEncodedPath}&overwrite=true");

                var uploadUrl = JsonConvert.DeserializeObject<JObject>(yRespContent)["href"].Value<string>();

                var result = await http.PutAsync(uploadUrl, new ByteArrayContent(photo.Content));

                result.EnsureSuccessStatusCode();
            }
        }
    }
}
