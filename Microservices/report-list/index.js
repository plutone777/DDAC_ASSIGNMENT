const AWSXRay = require('aws-xray-sdk-core');
const { S3Client, ListObjectsV2Command, GetObjectCommand } = require('@aws-sdk/client-s3');
const { getSignedUrl } = require('@aws-sdk/s3-request-presigner');

const s3Client = AWSXRay.captureAWSv3Client(new S3Client({}));
const BUCKET = process.env.S3_BUCKET;
const MAX_ITEMS = parseInt(process.env.MAX_ITEMS || '20', 10);

function respond(statusCode, body) {
  return {
    statusCode,
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body)
  };
}

exports.handler = async (event) => {
  try {
    const listed = await s3Client.send(new ListObjectsV2Command({
      Bucket: BUCKET,
      Prefix: 'reports/'
    }));

    const objects = (listed.Contents || [])
      .filter(o => o.Key !== 'reports/' && o.Size > 0)
      .sort((a, b) => new Date(b.LastModified) - new Date(a.LastModified))
      .slice(0, MAX_ITEMS);

    const reports = await Promise.all(objects.map(async (o) => {
      const fileName = o.Key.replace(/^reports\//, '');
      return {
        Key: o.Key,
        FileName: fileName,
        SizeBytes: o.Size,
        LastModified: o.LastModified,
        DownloadUrl: await getSignedUrl(
          s3Client,
          new GetObjectCommand({
            Bucket: BUCKET,
            Key: o.Key,
            ResponseContentDisposition: 'attachment; filename="' + fileName + '"'
          }),
          { expiresIn: 3600 }
        )
      };
    }));

    return respond(200, reports);

  } catch (err) {
    console.error('report-list error:', err);
    return respond(500, { message: 'Internal server error', detail: err.message });
  }
};
