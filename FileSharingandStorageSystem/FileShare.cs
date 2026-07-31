namespace FileSharingandStorageSystem
{
    public class FileShare
    {
        public int Id { get; set; }

        // Unguessable URL-safe token that identifies this share link.
        public string Token { get; set; } = string.Empty;

        public int FileMetaDataId { get; set; }
        public FileMetaData? File { get; set; }

        // User who created (owns) the share link.
        public string CreatedByUserId { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        // Null means the link never expires.
        public DateTime? ExpiresAt { get; set; }

        // Null means unlimited downloads.
        public int? MaxDownloads { get; set; }

        public int DownloadCount { get; set; }

        public bool IsRevoked { get; set; }

        public bool IsActive(DateTime nowUtc)
        {
            if (IsRevoked) return false;
            if (ExpiresAt.HasValue && ExpiresAt.Value <= nowUtc) return false;
            if (MaxDownloads.HasValue && DownloadCount >= MaxDownloads.Value) return false;
            return true;
        }
    }
}
