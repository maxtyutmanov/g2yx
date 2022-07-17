using g2yx.Services;
using Google.Apis.Auth.AspNetCore3;
using Hangfire;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;

namespace g2yx.Controllers
{
    public class SyncController : Controller
    {
        [HttpGet("Index")]
        public async Task<IActionResult> Index(string takeoutDirPath, CancellationToken ct)
        {
            var yAccessToken = await HttpContext.GetTokenAsync("yandex_cookie", "access_token");

            BackgroundJob.Enqueue<SyncJob>(job => job.SyncFromGTakeout(takeoutDirPath, yAccessToken, ct));

            return RedirectToAction("Index", "Home");
        }
    }
}
