using FileSharingandStorageSystem.Interfaces;
using FileSharingandStorageSystem;
using Microsoft.AspNetCore.StaticFiles;

namespace FileSharingandStorageSystem.Interfaces
{
    public interface IFileStorageService
    {
        Task StoreFileAsync(IFormFile file);
        FileStream? GetFileStream(string fileName);
        IEnumerable<FileMetaData> GetAllFiles();
    }

    public class FileStorageService : IFileStorageService
    {
        private readonly string _storagePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads");
        private readonly FileExtensionContentTypeProvider _contentTypeProvider;

        public FileStorageService()
        {
            if (!Directory.Exists(_storagePath))
                Directory.CreateDirectory(_storagePath);

            _contentTypeProvider = new FileExtensionContentTypeProvider();
        }

        public async Task StoreFileAsync(IFormFile file)
        {
            var safeFileName = Path.GetFileName(file.FileName);
            if (string.IsNullOrWhiteSpace(safeFileName))
                throw new ArgumentException("Invalid file name.", nameof(file));

            var filePath = Path.Combine(_storagePath, safeFileName);
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }
        }

        public FileStream? GetFileStream(string fileName)
        {
            // Strip any directory components to prevent path traversal.
            var safeFileName = Path.GetFileName(fileName);
            if (string.IsNullOrWhiteSpace(safeFileName))
                return null;

            var filePath = Path.Combine(_storagePath, safeFileName);
            if (!File.Exists(filePath))
                return null;

            return new FileStream(filePath, FileMode.Open, FileAccess.Read);
        }

        public IEnumerable<FileMetaData> GetAllFiles()
        {
            return Directory.GetFiles(_storagePath)
                .Select(filePath =>
                {
                    var fileName = Path.GetFileName(filePath);
                    var contentType = "application/octet-stream";

                    // Get MIME type based on the file extension
                    if (_contentTypeProvider.TryGetContentType(fileName, out var mimeType))
                    {
                        contentType = mimeType;
                    }

                    return new FileMetaData
                    {
                        FileName = fileName,
                        FileType = contentType,
                        FileSize = new FileInfo(filePath).Length,
                        UploadedAt = File.GetCreationTime(filePath)
                    };
                });
        }

    }
}

