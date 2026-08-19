using FileSharingandStorageSystem;
using FileSharingandStorageSystem.Services;
using Microsoft.EntityFrameworkCore;

namespace FileSharingandStorageSystem.Interfaces
{
    public interface IFileStorageService
    {
        Task StoreFileAsync(IFormFile file, string ownerId);
        Task<(Stream Stream, FileMetaData Meta)?> GetFileAsync(int id, string ownerId);
        FileMetaData? GetUserFile(int id, string ownerId);
        IEnumerable<FileMetaData> GetUserFiles(string ownerId);
        Task<bool> DeleteFileAsync(int id, string ownerId);
        Task<bool> RenameFileAsync(int id, string ownerId, string newName);
        Task<bool> ReplaceFileAsync(int id, string ownerId, IFormFile file);
    }

    public class FileStorageService : IFileStorageService
    {
        private readonly AppDBContext _db;
        private readonly IObjectStorage _storage;

        public FileStorageService(AppDBContext db, IObjectStorage storage)
        {
            _db = db;
            _storage = storage;
        }

        public async Task StoreFileAsync(IFormFile file, string ownerId)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("No file provided.", nameof(file));

            var originalName = Path.GetFileName(file.FileName);
            if (string.IsNullOrWhiteSpace(originalName))
                throw new ArgumentException("Invalid file name.", nameof(file));

            var storedName = $"{Guid.NewGuid():N}{Path.GetExtension(originalName)}";
            var contentType = string.IsNullOrWhiteSpace(file.ContentType)
                ? "application/octet-stream"
                : file.ContentType;

            await using (var stream = file.OpenReadStream())
            {
                await _storage.UploadAsync(storedName, stream, contentType);
            }

            var meta = new FileMetaData
            {
                FileName = originalName,
                StoredFileName = storedName,
                FileType = contentType,
                FileSize = file.Length,
                UploadedAt = DateTime.UtcNow,
                OwnerId = ownerId
            };

            _db.FileMetaData.Add(meta);
            await _db.SaveChangesAsync();
        }

        public async Task<(Stream Stream, FileMetaData Meta)?> GetFileAsync(int id, string ownerId)
        {
            var meta = await _db.FileMetaData
                .FirstOrDefaultAsync(f => f.Id == id && f.OwnerId == ownerId);

            if (meta == null)
                return null;

            var stream = await _storage.OpenReadAsync(meta.StoredFileName);
            if (stream == null)
                return null;

            return (stream, meta);
        }

        public FileMetaData? GetUserFile(int id, string ownerId)
        {
            return _db.FileMetaData
                .FirstOrDefault(f => f.Id == id && f.OwnerId == ownerId);
        }

        public IEnumerable<FileMetaData> GetUserFiles(string ownerId)
        {
            return _db.FileMetaData
                .Where(f => f.OwnerId == ownerId)
                .OrderByDescending(f => f.UploadedAt)
                .ToList();
        }

        public async Task<bool> DeleteFileAsync(int id, string ownerId)
        {
            var meta = await _db.FileMetaData
                .FirstOrDefaultAsync(f => f.Id == id && f.OwnerId == ownerId);
            if (meta == null)
                return false;

            // Remove any share links pointing at this file, then the metadata,
            // then the stored bytes.
            var shares = _db.FileShares.Where(s => s.FileMetaDataId == id);
            _db.FileShares.RemoveRange(shares);
            _db.FileMetaData.Remove(meta);
            await _db.SaveChangesAsync();

            await _storage.DeleteAsync(meta.StoredFileName);
            return true;
        }

        public async Task<bool> RenameFileAsync(int id, string ownerId, string newName)
        {
            var meta = await _db.FileMetaData
                .FirstOrDefaultAsync(f => f.Id == id && f.OwnerId == ownerId);
            if (meta == null)
                return false;

            var sanitized = SanitizeFileName(newName);
            if (sanitized == null)
                return false;

            meta.FileName = sanitized;
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ReplaceFileAsync(int id, string ownerId, IFormFile file)
        {
            var meta = await _db.FileMetaData
                .FirstOrDefaultAsync(f => f.Id == id && f.OwnerId == ownerId);
            if (meta == null)
                return false;

            await ReplaceContentAsync(meta, file);
            return true;
        }

        // Uploads new bytes for an existing file under a fresh stored name, updates
        // the metadata, and removes the old object. Shared by the owner-scoped and
        // share-token-scoped replace paths.
        internal async Task ReplaceContentAsync(FileMetaData meta, IFormFile file)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("No file provided.", nameof(file));

            var newStoredName = $"{Guid.NewGuid():N}{Path.GetExtension(meta.FileName)}";
            var contentType = string.IsNullOrWhiteSpace(file.ContentType)
                ? "application/octet-stream"
                : file.ContentType;

            await using (var stream = file.OpenReadStream())
            {
                await _storage.UploadAsync(newStoredName, stream, contentType);
            }

            var oldStoredName = meta.StoredFileName;
            meta.StoredFileName = newStoredName;
            meta.FileType = contentType;
            meta.FileSize = file.Length;
            meta.UploadedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            await _storage.DeleteAsync(oldStoredName);
        }

        // Returns a safe, stored-safe file name, or null if the input is unusable.
        internal static string? SanitizeFileName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;

            var trimmed = Path.GetFileName(name.Trim());
            if (string.IsNullOrWhiteSpace(trimmed))
                return null;

            return trimmed.Length > 260 ? trimmed[..260] : trimmed;
        }
    }
}
