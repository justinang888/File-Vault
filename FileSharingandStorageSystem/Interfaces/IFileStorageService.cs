using FileSharingandStorageSystem;
using Microsoft.EntityFrameworkCore;

namespace FileSharingandStorageSystem.Interfaces
{
    public interface IFileStorageService
    {
        Task StoreFileAsync(IFormFile file, string ownerId);
        Task<(FileStream Stream, FileMetaData Meta)?> GetFileAsync(int id, string ownerId);
        FileMetaData? GetUserFile(int id, string ownerId);
        IEnumerable<FileMetaData> GetUserFiles(string ownerId);
    }

    public class FileStorageService : IFileStorageService
    {
        private readonly AppDBContext _db;
        private readonly string _storagePath;

        public FileStorageService(AppDBContext db, IWebHostEnvironment env)
        {
            _db = db;

            // Store outside wwwroot so files are only reachable through the
            // authenticated Download action, never as static content.
            _storagePath = Path.Combine(env.ContentRootPath, "Storage");
            if (!Directory.Exists(_storagePath))
                Directory.CreateDirectory(_storagePath);
        }

        public async Task StoreFileAsync(IFormFile file, string ownerId)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("No file provided.", nameof(file));

            var originalName = Path.GetFileName(file.FileName);
            if (string.IsNullOrWhiteSpace(originalName))
                throw new ArgumentException("Invalid file name.", nameof(file));

            var storedName = $"{Guid.NewGuid():N}{Path.GetExtension(originalName)}";
            var filePath = Path.Combine(_storagePath, storedName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var meta = new FileMetaData
            {
                FileName = originalName,
                StoredFileName = storedName,
                FileType = string.IsNullOrWhiteSpace(file.ContentType)
                    ? "application/octet-stream"
                    : file.ContentType,
                FileSize = file.Length,
                UploadedAt = DateTime.UtcNow,
                OwnerId = ownerId
            };

            _db.FileMetaData.Add(meta);
            await _db.SaveChangesAsync();
        }

        public async Task<(FileStream Stream, FileMetaData Meta)?> GetFileAsync(int id, string ownerId)
        {
            var meta = await _db.FileMetaData
                .FirstOrDefaultAsync(f => f.Id == id && f.OwnerId == ownerId);

            if (meta == null)
                return null;

            var filePath = Path.Combine(_storagePath, meta.StoredFileName);
            if (!File.Exists(filePath))
                return null;

            var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
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
