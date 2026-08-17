namespace FileSharingandStorageSystem.Models
{
    // Details shown on the public download confirmation page (GET /s/{token})
    // before the visitor confirms and actually downloads the file.
    public class SharedFileViewModel
    {
        public string Token { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string FileType { get; set; } = string.Empty;
        public DateTime? ExpiresAt { get; set; }
        public int DownloadCount { get; set; }
        public int? MaxDownloads { get; set; }

        public int? DownloadsRemaining =>
            MaxDownloads.HasValue ? Math.Max(0, MaxDownloads.Value - DownloadCount) : null;
    }
}
