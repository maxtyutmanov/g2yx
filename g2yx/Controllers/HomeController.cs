using g2yx.Models;
using g2yx.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
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
                YandexAccessToken = yxToken,
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
