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
        public async Task<IActionResult> CreateShare(int id, string expiry, int? maxDownloads, string? permission)
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

            var access = string.Equals(permission, "editor", StringComparison.OrdinalIgnoreCase)
                ? SharePermission.Editor
                : SharePermission.Viewer;

            var share = await _fileShareService.CreateShareAsync(id, userId, lifetime, limit, access);
            if (share == null)
            {
                TempData["Error"] = "Could not create a share link for that file.";
                return RedirectToAction("Index");
            }

            TempData["Message"] = "Share link created.";
            return RedirectToAction("Share", new { id });
        }

        [HttpPost("files/{id:int}/delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = _userManager.GetUserId(User)!;
            var ok = await _fileStorageService.DeleteFileAsync(id, userId);
            TempData[ok ? "Message" : "Error"] = ok ? "File deleted." : "Could not delete that file.";
            return RedirectToAction("Index");
        }

        [HttpPost("files/{id:int}/rename")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Rename(int id, string newName)
        {
            var userId = _userManager.GetUserId(User)!;
            var ok = await _fileStorageService.RenameFileAsync(id, userId, newName);
            TempData[ok ? "Message" : "Error"] = ok ? "File renamed." : "Could not rename that file.";
            return RedirectToAction("Index");
        }

        [HttpPost("files/{id:int}/replace")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Replace(int id, IFormFile file)
        {
            var userId = _userManager.GetUserId(User)!;
            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Please choose a file to upload.";
                return RedirectToAction("Index");
            }

            var ok = await _fileStorageService.ReplaceFileAsync(id, userId, file);
            TempData[ok ? "Message" : "Error"] = ok ? "File replaced." : "Could not replace that file.";
            return RedirectToAction("Index");
        }

        [HttpGet("files/share/{id:int}/status")]
        public async Task<IActionResult> ShareStatus(int id)
        {
            var userId = _userManager.GetUserId(User)!;
            var shares = await _fileShareService.GetSharesForFileAsync(id, userId);
            var now = DateTime.UtcNow;

            var data = shares.Select(s => new
            {
                id = s.Id,
                active = s.IsActive(now),
                statusText = s.StatusText(now),
                downloadCount = s.DownloadCount,
                maxDownloads = s.MaxDownloads
            });

            return Json(data);
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

        // Share links now require the recipient to be logged in. Anonymous visitors
        // are redirected to the login page (and back) by the cookie middleware.
        [HttpGet("s/{token}")]
        public async Task<IActionResult> Shared(string token)
        {
            var share = await _fileShareService.GetShareInfoAsync(token);
            if (share?.File == null)
                return View("ShareUnavailable");

            return View(new SharedFileViewModel
            {
                Token = token,
                FileName = share.File.FileName,
                FileSize = share.File.FileSize,
                FileType = share.File.FileType,
                ExpiresAt = share.ExpiresAt,
                DownloadCount = share.DownloadCount,
                MaxDownloads = share.MaxDownloads,
                Permission = share.Permission
            });
        }

        [HttpPost("s/{token}/download")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DownloadShared(string token)
        {
            var result = await _fileShareService.GetSharedFileAsync(token);
            if (result == null)
                return View("ShareUnavailable");

            var (stream, meta) = result.Value;
            return File(stream, "application/octet-stream", meta.FileName);
        }

        [HttpPost("s/{token}/rename")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SharedRename(string token, string newName)
        {
            var ok = await _fileShareService.RenameViaShareAsync(token, newName);
            TempData[ok ? "Message" : "Error"] = ok ? "File renamed." : "You don't have permission to rename this file.";
            return RedirectToAction("Shared", new { token });
        }

        [HttpPost("s/{token}/replace")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SharedReplace(string token, IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Please choose a file to upload.";
                return RedirectToAction("Shared", new { token });
            }

            var ok = await _fileShareService.ReplaceViaShareAsync(token, file);
            TempData[ok ? "Message" : "Error"] = ok ? "File replaced." : "You don't have permission to replace this file.";
            return RedirectToAction("Shared", new { token });
        }

        [HttpPost("s/{token}/revoke")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SharedRevoke(string token)
        {
            var ok = await _fileShareService.RevokeViaShareAsync(token);
            if (ok)
            {
                TempData["Message"] = "Share link revoked.";
                return View("ShareUnavailable");
            }

            TempData["Error"] = "You don't have permission to revoke this link.";
            return RedirectToAction("Shared", new { token });
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
