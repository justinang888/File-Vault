using System.Security.Cryptography;
using FileSharingandStorageSystem;
using Microsoft.EntityFrameworkCore;

namespace FileSharingandStorageSystem.Interfaces
{
    public interface IFileShareService
    {
        Task<FileShare?> CreateShareAsync(int fileId, string ownerId, TimeSpan? lifetime, int? maxDownloads);
        Task<IEnumerable<FileShare>> GetSharesForFileAsync(int fileId, string ownerId);
        Task<bool> RevokeShareAsync(int shareId, string ownerId);
        Task<(FileStream Stream, FileMetaData Meta)?> GetSharedFileAsync(string token);
    }

    public class FileShareService : IFileShareService
    {
        private readonly AppDBContext _db;
        private readonly string _storagePath;

        public FileShareService(AppDBContext db, IWebHostEnvironment env)
        {
            _db = db;
            _storagePath = Path.Combine(env.ContentRootPath, "Storage");
        }

        public async Task<FileShare?> CreateShareAsync(int fileId, string ownerId, TimeSpan? lifetime, int? maxDownloads)
        {
            // Only the file's owner may create a share for it.
            var file = await _db.FileMetaData
                .FirstOrDefaultAsync(f => f.Id == fileId && f.OwnerId == ownerId);
            if (file == null)
                return null;

            var share = new FileShare
            {
                Token = GenerateToken(),
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

        public async Task<(FileStream Stream, FileMetaData Meta)?> GetSharedFileAsync(string token)
        {
            var share = await _db.FileShares
                .Include(s => s.File)
                .FirstOrDefaultAsync(s => s.Token == token);

            if (share == null || share.File == null || !share.IsActive(DateTime.UtcNow))
                return null;

            var filePath = Path.Combine(_storagePath, share.File.StoredFileName);
            if (!File.Exists(filePath))
                return null;

            share.DownloadCount++;
            await _db.SaveChangesAsync();

            var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            return (stream, share.File);
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
