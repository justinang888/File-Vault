using System.Security.Cryptography;
using FileSharingandStorageSystem;
using FileSharingandStorageSystem.Services;
using Microsoft.EntityFrameworkCore;

namespace FileSharingandStorageSystem.Interfaces
{
    public interface IFileShareService
    {
        Task<FileShare?> CreateShareAsync(int fileId, string ownerId, TimeSpan? lifetime, int? maxDownloads, SharePermission permission);
        Task<IEnumerable<FileShare>> GetSharesForFileAsync(int fileId, string ownerId);
        Task<bool> RevokeShareAsync(int shareId, string ownerId);
        Task<FileShare?> GetShareInfoAsync(string token);
        Task<(Stream Stream, FileMetaData Meta)?> GetSharedFileAsync(string token);

        // Editor-link actions, authorized by the link's Editor permission rather
        // than file ownership. Return false if the link is missing, inactive, or
        // not an Editor link.
        Task<bool> RenameViaShareAsync(string token, string newName);
        Task<bool> ReplaceViaShareAsync(string token, IFormFile file);
        Task<bool> RevokeViaShareAsync(string token);
    }

    public class FileShareService : IFileShareService
    {
        private readonly AppDBContext _db;
        private readonly IObjectStorage _storage;

        public FileShareService(AppDBContext db, IObjectStorage storage)
        {
            _db = db;
            _storage = storage;
        }

        public async Task<FileShare?> CreateShareAsync(int fileId, string ownerId, TimeSpan? lifetime, int? maxDownloads, SharePermission permission)
        {
            // Only the file's owner may create a share for it.
            var file = await _db.FileMetaData
                .FirstOrDefaultAsync(f => f.Id == fileId && f.OwnerId == ownerId);
            if (file == null)
                return null;

            var share = new FileShare
            {
                Token = GenerateToken(),
                Permission = permission,
                FileMetaDataId = file.Id,
                CreatedByUserId = ownerId,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = lifetime.HasValue ? DateTime.UtcNow.Add(lifetime.Value) : null,
                MaxDownloads = maxDownloads,
                DownloadCount = 0,
                IsRevoked = false
            };

            _db.FileShares.Add(share);
            await _db.SaveChangesAsync();
            return share;
        }

        public async Task<IEnumerable<FileShare>> GetSharesForFileAsync(int fileId, string ownerId)
        {
            // Ownership check keeps one user from listing another user's shares.
            var owns = await _db.FileMetaData
                .AnyAsync(f => f.Id == fileId && f.OwnerId == ownerId);
            if (!owns)
                return Enumerable.Empty<FileShare>();

            return await _db.FileShares
                .Where(s => s.FileMetaDataId == fileId)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();
        }

        public async Task<bool> RevokeShareAsync(int shareId, string ownerId)
        {
            var share = await _db.FileShares
                .FirstOrDefaultAsync(s => s.Id == shareId && s.CreatedByUserId == ownerId);
            if (share == null)
                return false;

            share.IsRevoked = true;
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<FileShare?> GetShareInfoAsync(string token)
        {
            // Read-only lookup for the confirmation page: does not open a stream or
            // increment the download count, so viewing the page doesn't consume a download.
            var share = await _db.FileShares
                .Include(s => s.File)
                .FirstOrDefaultAsync(s => s.Token == token);

            if (share == null || share.File == null || !share.IsActive(DateTime.UtcNow))
                return null;

            return share;
        }

        public async Task<(Stream Stream, FileMetaData Meta)?> GetSharedFileAsync(string token)
        {
            var share = await _db.FileShares
                .Include(s => s.File)
                .FirstOrDefaultAsync(s => s.Token == token);

            if (share == null || share.File == null || !share.IsActive(DateTime.UtcNow))
                return null;

            var stream = await _storage.OpenReadAsync(share.File.StoredFileName);
            if (stream == null)
                return null;

            share.DownloadCount++;
            await _db.SaveChangesAsync();

            return (stream, share.File);
        }

        public async Task<bool> RenameViaShareAsync(string token, string newName)
        {
            var share = await GetActiveEditableShareAsync(token);
            if (share?.File == null)
                return false;

            var sanitized = FileStorageService.SanitizeFileName(newName);
            if (sanitized == null)
                return false;

            share.File.FileName = sanitized;
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ReplaceViaShareAsync(string token, IFormFile file)
        {
            var share = await GetActiveEditableShareAsync(token);
            if (share?.File == null)
                return false;

            if (file == null || file.Length == 0)
                return false;

            var newStoredName = $"{Guid.NewGuid():N}{Path.GetExtension(share.File.FileName)}";
            var contentType = string.IsNullOrWhiteSpace(file.ContentType)
                ? "application/octet-stream"
                : file.ContentType;

            await using (var stream = file.OpenReadStream())
            {
                await _storage.UploadAsync(newStoredName, stream, contentType);
            }

            var oldStoredName = share.File.StoredFileName;
            share.File.StoredFileName = newStoredName;
            share.File.FileType = contentType;
            share.File.FileSize = file.Length;
            share.File.UploadedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            await _storage.DeleteAsync(oldStoredName);
            return true;
        }

        public async Task<bool> RevokeViaShareAsync(string token)
        {
            var share = await GetActiveEditableShareAsync(token);
            if (share == null)
                return false;

            share.IsRevoked = true;
            await _db.SaveChangesAsync();
            return true;
        }

        // Loads a share only if it is an active Editor link; otherwise null.
        private async Task<FileShare?> GetActiveEditableShareAsync(string token)
        {
            var share = await _db.FileShares
                .Include(s => s.File)
                .FirstOrDefaultAsync(s => s.Token == token);

            if (share == null || share.File == null || !share.CanEdit(DateTime.UtcNow))
                return null;

            return share;
        }

        private static string GenerateToken()
        {
            // 32 random bytes -> URL-safe base64 (no padding), ~43 chars.
            var bytes = RandomNumberGenerator.GetBytes(32);
            return Convert.ToBase64String(bytes)
                .Replace("+", "-")
                .Replace("/", "_")
                .TrimEnd('=');
        }
    }
}
