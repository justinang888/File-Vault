namespace FileSharingandStorageSystem
{
    public class FileMetaData
    {
        public int Id { get; set; }

        // Original file name as uploaded by the user.
        public string FileName { get; set; } = string.Empty;

        // Randomized name used on disk to avoid collisions and path issues.
        public string StoredFileName { get; set; } = string.Empty;

        public string FileType { get; set; } = string.Empty;
        public long FileSize { get; set; } // Size in bytes
        public DateTime UploadedAt { get; set; }

        // Id of the owning user (FK to AspNetUsers).
        public string OwnerId { get; set; } = string.Empty;
    }
}
