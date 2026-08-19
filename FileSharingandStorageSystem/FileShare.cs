namespace FileSharingandStorageSystem
{
    // What a logged-in recipient of a share link is allowed to do.
    public enum SharePermission
    {
        // Can view details and download the file.
        Viewer = 0,

        // Everything a Viewer can do, plus manage the file: rename, replace
        // (re-upload a new version), and revoke the link.
        Editor = 1
    }

    public class FileShare
    {
        public int Id { get; set; }

        // Unguessable URL-safe token that identifies this share link.
        public string Token { get; set; } = string.Empty;

        // Access level granted to whoever opens this link (while logged in).
        public SharePermission Permission { get; set; } = SharePermission.Viewer;

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

        // Human-readable status shown on the manage-shares page. Kept here so the
        // server-rendered view and the live-status JSON endpoint stay in sync.
        public string StatusText(DateTime nowUtc)
        {
            if (IsActive(nowUtc)) return "Active";
            if (IsRevoked) return "Revoked";
            if (ExpiresAt.HasValue && ExpiresAt.Value <= nowUtc) return "Expired";
            return "Limit reached";
        }

        // True when this link grants management (edit) rights and is still usable.
        public bool CanEdit(DateTime nowUtc) => Permission == SharePermission.Editor && IsActive(nowUtc);
    }
}
