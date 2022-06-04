using g2yx.Models;
using Google.Apis.Auth.AspNetCore3;
using Google.Apis.PhotosLibrary.v1;
using Google.Apis.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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

        public async Task<IActionResult> Index(CancellationToken ct)
        {
            var creds = await _googleAuth.GetCredentialAsync(cancellationToken: ct);
            var plSvc = new PhotosLibraryService(new BaseClientService.Initializer
            {
                HttpClientInitializer = creds
            });

            var albumsResource = await plSvc.Albums.List().ExecuteAsync();
            var albumTitles = albumsResource.Albums.Select(x => x.Title).ToList();

            ViewData["AlbumTitles"] = albumTitles;

            return View();
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
