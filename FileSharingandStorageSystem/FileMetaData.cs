namespace FileSharingandStorageSystem
{
    public class FileMetaData
    {
        public int Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string FileType { get; set; } = string.Empty;
        public long FileSize { get; set; } // Size in bytes
        public DateTime UploadedAt { get; set; }
    }
}
