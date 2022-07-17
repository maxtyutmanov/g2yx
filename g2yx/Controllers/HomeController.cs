using g2yx.Models;
using g2yx.Services;
using Google.Apis.Auth.AspNetCore3;
using Google.Apis.Drive.v3;
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
    public class HomeController : Controller
    {
        public async Task<ViewResult> Index(CancellationToken ct)
        {
            var yxToken = await HttpContext.GetTokenAsync("yandex_cookie", "access_token");
            var model = new IndexModel()
            {
                LoggedInYandex = !string.IsNullOrEmpty(yxToken)
            };

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
