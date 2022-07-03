using g2yx.Models;
using g2yx.Services;
using Google.Apis.Auth.AspNetCore3;
using Google.Apis.PhotosLibrary.v1;
using Google.Apis.PhotosLibrary.v1.Data;
using Google.Apis.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace g2yx.Controllers
{
    [GoogleScopedAuthorize("https://www.googleapis.com/auth/photoslibrary.readonly")]
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IGoogleAuthProvider _googleAuth;

        public HomeController(ILogger<HomeController> logger, IGoogleAuthProvider googleAuth)
        {
            _logger = logger;
            _googleAuth = googleAuth;
        }

        public async Task<ViewResult> Index(CancellationToken ct)
        {
            var creds = await _googleAuth.GetCredentialAsync(cancellationToken: ct);
            var plSvc = new PhotosLibraryService(new BaseClientService.Initializer
            {
                HttpClientInitializer = creds
            });

            var allAlbums = new List<AlbumMeta>();
            string nextPageToken = null;

            do
            {
                var albumsRequest = plSvc.Albums.List();
                albumsRequest.PageToken = nextPageToken;
                var albumsResource = await albumsRequest.ExecuteAsync(ct);
                allAlbums.AddRange(albumsResource.Albums.Select(x => new AlbumMeta(x.Id, x.Title)));
                nextPageToken = albumsResource.NextPageToken;
            } while (!string.IsNullOrEmpty(nextPageToken));

            var model = new IndexModel
            {
                Albums = allAlbums
            };

            var yxToken = await HttpContext.GetTokenAsync("yandex_cookie", "access_token");
            model.LoggedInYandex = !string.IsNullOrEmpty(yxToken);
            if (model.LoggedInYandex)
            {
                model.Progress = await new YandexDiskApi(yxToken).GetSyncProgress(ct);
            }
            return View(model);
        }

        [HttpGet("Login/Yandex")]
        public IActionResult LoginWithYandex()
        {
            return Challenge(
                new AuthenticationProperties()
                {
                    RedirectUri = Url.Action("Index")
                },
                "yandex_cookie");
        }

        [HttpGet("Albums/{albumId}")]
        public async Task<IActionResult> Album(string albumId, CancellationToken ct)
        {
            var creds = await _googleAuth.GetCredentialAsync(cancellationToken: ct);
            var plSvc = new PhotosLibraryService(new BaseClientService.Initializer
            {
                HttpClientInitializer = creds
            });

            var album = await plSvc.Albums.Get(albumId).ExecuteAsync(ct);

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

            return View(new Models.Album(album.Title, allItemIds));
        }

        [HttpGet("Photos/{photoId}")]
        public async Task<IActionResult> Photo(string photoId, CancellationToken ct)
        {
            var creds = await _googleAuth.GetCredentialAsync(cancellationToken: ct);
            var plSvc = new PhotosLibraryService(new BaseClientService.Initializer
            {
                HttpClientInitializer = creds
            });

            var photo = await plSvc.MediaItems.Get(photoId).ExecuteAsync(ct);

            if (photo.MediaMetadata.Photo == null)
                return NotFound();

            var width = photo.MediaMetadata.Width;
            var height = photo.MediaMetadata.Height;

            using var http = new HttpClient();
            
            var photoStream = await http.GetStreamAsync(photo.BaseUrl + $"=w{width}-h{height}");
            return new FileStreamResult(photoStream, photo.MimeType);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
