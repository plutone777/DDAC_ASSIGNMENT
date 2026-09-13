using Amazon;
using Amazon.S3;
using Amazon.S3.Model;

namespace DDAC.Services
{
    public class S3Service
    {
        private readonly IConfiguration _configuration;
        private readonly IAmazonS3 _s3Client;
        private readonly string _bucketName;

        public S3Service(IConfiguration configuration)
        {
            _configuration = configuration;

            var region = _configuration["AWS:region"];
            _bucketName = _configuration["AWS:bucket_name"]
                ?? throw new InvalidOperationException(
                    "AWS bucket name is not configured.");

            _s3Client = new AmazonS3Client(
                RegionEndpoint.GetBySystemName(region)
            );
        }

        public async Task<string> UploadResumeAsync(
            IFormFile resume,
            int userId)
        {
            var extension = Path
                .GetExtension(resume.FileName)
                .ToLowerInvariant();

            var fileName =
                $"resumes/{userId}/{Guid.NewGuid()}{extension}";

            var uploadRequest = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = fileName,
                InputStream = resume.OpenReadStream(),
                ContentType = resume.ContentType
            };

            await _s3Client.PutObjectAsync(uploadRequest);

            return fileName;
        }

        public async Task<string> GetResumeUrlAsync(
            string s3Key)
        {
            var request = new GetPreSignedUrlRequest
            {
                BucketName = _bucketName,
                Key = s3Key,
                Expires = DateTime.UtcNow.AddMinutes(10),
                Verb = HttpVerb.GET
            };

            return _s3Client.GetPreSignedURL(request);
        }
    }
}