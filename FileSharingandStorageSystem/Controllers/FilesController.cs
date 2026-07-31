using System.Diagnostics;
using FileSharingandStorageSystem.Interfaces;
using FileSharingandStorageSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace FileSharingandStorageSystem.Controllers
{
    [Authorize]
    public class FilesController : Controller
    {
        private readonly IFileStorageService _fileStorageService;
        private readonly IFileShareService _fileShareService;
        private readonly UserManager<ApplicationUser> _userManager;

        public FilesController(
            IFileStorageService fileStorageService,
            IFileShareService fileShareService,
            UserManager<ApplicationUser> userManager)
        {
            _fileStorageService = fileStorageService;
            _fileShareService = fileShareService;
            _userManager = userManager;
        }

        public IActionResult Index()
        {
            var userId = _userManager.GetUserId(User)!;
            var files = _fileStorageService.GetUserFiles(userId);
            return View(files);
        }

        [AllowAnonymous]
        public IActionResult Privacy()
        {
            return View();
        }

        [HttpPost("upload")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upload(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Please select a file to upload.";
                return RedirectToAction("Index");
            }

            var userId = _userManager.GetUserId(User)!;
            await _fileStorageService.StoreFileAsync(file, userId);
            TempData["Message"] = "File uploaded successfully.";
            return RedirectToAction("Index");
        }

        [HttpGet("download/{id:int}")]
        public async Task<IActionResult> Download(int id)
        {
            var userId = _userManager.GetUserId(User)!;
            var result = await _fileStorageService.GetFileAsync(id, userId);
            if (result == null) return NotFound();

            var (stream, meta) = result.Value;
            return File(stream, "application/octet-stream", meta.FileName);
        }

        [HttpGet("files/share/{id:int}")]
        public async Task<IActionResult> Share(int id)
        {
            var userId = _userManager.GetUserId(User)!;
            var file = _fileStorageService.GetUserFile(id, userId);
            if (file == null) return NotFound();

            var shares = await _fileShareService.GetSharesForFileAsync(id, userId);
            return View(new ManageSharesViewModel { File = file, Shares = shares });
        }

        [HttpPost("files/share/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateShare(int id, string expiry, int? maxDownloads)
        {
            var userId = _userManager.GetUserId(User)!;

            var lifetime = expiry switch
            {
                "1h" => TimeSpan.FromHours(1),
                "1d" => TimeSpan.FromDays(1),
                "7d" => TimeSpan.FromDays(7),
                "30d" => TimeSpan.FromDays(30),
                _ => (TimeSpan?)null
            };

            var limit = maxDownloads.HasValue && maxDownloads.Value > 0 ? maxDownloads : null;

            var share = await _fileShareService.CreateShareAsync(id, userId, lifetime, limit);
            if (share == null)
            {
                TempData["Error"] = "Could not create a share link for that file.";
                return RedirectToAction("Index");
            }

            TempData["Message"] = "Share link created.";
            return RedirectToAction("Share", new { id });
        }

        [HttpPost("files/share/{id:int}/revoke")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RevokeShare(int id, int shareId)
        {
            var userId = _userManager.GetUserId(User)!;
            await _fileShareService.RevokeShareAsync(shareId, userId);
            TempData["Message"] = "Share link revoked.";
            return RedirectToAction("Share", new { id });
        }

        [AllowAnonymous]
        [HttpGet("s/{token}")]
        public async Task<IActionResult> Shared(string token)
        {
            var result = await _fileShareService.GetSharedFileAsync(token);
            if (result == null)
                return View("ShareUnavailable");

            var (stream, meta) = result.Value;
            return File(stream, "application/octet-stream", meta.FileName);
        }

        [AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            var requestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
            return View(new ErrorViewModel { RequestId = requestId });
        }
    }
}
