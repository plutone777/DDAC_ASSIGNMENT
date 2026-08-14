using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

namespace DDAC.Services
{
    public class S3Service
    {
        private readonly IConfiguration _configuration;

        public S3Service(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<string> UploadResumeAsync(
            IFormFile resume,
            int userId)
        {
            // Get AWS settings
            var accessKey =
                _configuration["AWS:aws_access_key_id"];

            var secretKey =
                _configuration["AWS:aws_secret_access_key"];

            var sessionToken =
                _configuration["AWS:aws_session_token"];

            var region =
                _configuration["AWS:region"];

            var bucketName =
                _configuration["AWS:bucket_name"];


            // Create temporary AWS credentials
            var credentials = new SessionAWSCredentials(
                accessKey,
                secretKey,
                sessionToken
            );


            // Create S3 client
            var s3Client = new AmazonS3Client(
                credentials,
                RegionEndpoint.GetBySystemName(region)
            );


            // Create unique filename
            var extension =
                Path.GetExtension(resume.FileName)
                    .ToLowerInvariant();

            var fileName =
                $"resumes/{userId}/{Guid.NewGuid()}{extension}";


            // Create upload request
            var uploadRequest = new PutObjectRequest
            {
                BucketName = bucketName,
                Key = fileName,
                InputStream = resume.OpenReadStream(),
                ContentType = resume.ContentType
            };


            // Upload to S3
            await s3Client.PutObjectAsync(uploadRequest);


            // Return the S3 object key
            return fileName;
        }

        public async Task<string> GetResumeUrlAsync(string s3Key)
        {
            var accessKey =
                _configuration["AWS:aws_access_key_id"];

            var secretKey =
                _configuration["AWS:aws_secret_access_key"];

            var sessionToken =
                _configuration["AWS:aws_session_token"];

            var region =
                _configuration["AWS:region"];

            var bucketName =
                _configuration["AWS:bucket_name"];

            var credentials = new SessionAWSCredentials(
                accessKey,
                secretKey,
                sessionToken
            );

            var s3Client = new AmazonS3Client(
                credentials,
                RegionEndpoint.GetBySystemName(region)
            );

            var request = new GetPreSignedUrlRequest
            {
                BucketName = bucketName,
                Key = s3Key,
                Expires = DateTime.UtcNow.AddMinutes(10),
                Verb = HttpVerb.GET
            };

            return s3Client.GetPreSignedURL(request);
        }
    }
}