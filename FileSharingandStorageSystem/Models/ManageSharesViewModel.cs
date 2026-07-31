namespace FileSharingandStorageSystem.Models
{
    public class ManageSharesViewModel
    {
        public FileMetaData File { get; set; } = null!;
        public IEnumerable<FileShare> Shares { get; set; } = Enumerable.Empty<FileShare>();
    }
}
