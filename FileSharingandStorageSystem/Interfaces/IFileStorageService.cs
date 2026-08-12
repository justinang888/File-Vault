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
    }
}
