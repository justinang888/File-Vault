using Amazon.S3;
using Amazon.S3.Model;

namespace FileSharingandStorageSystem.Services
{
    // Abstraction over where uploaded file bytes physically live. Metadata always
    // stays in the database; only the raw bytes are handled here. This lets the app
    // run against local disk in development and S3-compatible object storage
    // (Cloudflare R2, AWS S3, MinIO) in production without changing callers.
    public interface IObjectStorage
    {
        Task UploadAsync(string key, Stream content, string contentType, CancellationToken ct = default);
        Task<Stream?> OpenReadAsync(string key, CancellationToken ct = default);
        Task DeleteAsync(string key, CancellationToken ct = default);
    }

    // Stores objects on the local file system under ContentRootPath/Storage.
    // Used when no object-storage bucket is configured (typically local dev).
    public class LocalObjectStorage : IObjectStorage
    {
        private readonly string _storagePath;

        public LocalObjectStorage(IWebHostEnvironment env)
        {
            _storagePath = Path.Combine(env.ContentRootPath, "Storage");
            if (!Directory.Exists(_storagePath))
                Directory.CreateDirectory(_storagePath);
        }

        public async Task UploadAsync(string key, Stream content, string contentType, CancellationToken ct = default)
        {
            var path = Path.Combine(_storagePath, key);
            await using var file = new FileStream(path, FileMode.Create, FileAccess.Write);
            await content.CopyToAsync(file, ct);
        }

        public Task<Stream?> OpenReadAsync(string key, CancellationToken ct = default)
        {
            var path = Path.Combine(_storagePath, key);
            if (!File.Exists(path))
                return Task.FromResult<Stream?>(null);

            Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read);
            return Task.FromResult<Stream?>(stream);
        }

        public Task DeleteAsync(string key, CancellationToken ct = default)
        {
            var path = Path.Combine(_storagePath, key);
            if (File.Exists(path))
                File.Delete(path);
            return Task.CompletedTask;
        }
    }

    // Stores objects in an S3-compatible bucket. Works with AWS S3, Cloudflare R2,
    // and MinIO; the specific endpoint is set on the injected IAmazonS3 client.
    public class S3ObjectStorage : IObjectStorage
    {
        private readonly IAmazonS3 _s3;
        private readonly string _bucket;

        public S3ObjectStorage(IAmazonS3 s3, IConfiguration config)
        {
            _s3 = s3;
            _bucket = config["Storage:S3:Bucket"]
                ?? throw new InvalidOperationException("Storage:S3:Bucket is not configured.");
        }

        public async Task UploadAsync(string key, Stream content, string contentType, CancellationToken ct = default)
        {
            var request = new PutObjectRequest
            {
                BucketName = _bucket,
                Key = key,
                InputStream = content,
                ContentType = contentType,
                AutoCloseStream = false,
                DisablePayloadSigning = true // required for Cloudflare R2 streaming uploads
            };
            await _s3.PutObjectAsync(request, ct);
        }

        public async Task<Stream?> OpenReadAsync(string key, CancellationToken ct = default)
        {
            try
            {
                var response = await _s3.GetObjectAsync(_bucket, key, ct);
                return response.ResponseStream;
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        public async Task DeleteAsync(string key, CancellationToken ct = default)
        {
            await _s3.DeleteObjectAsync(_bucket, key, ct);
        }
    }
}
