const AWSXRay = require('aws-xray-sdk-core');
const { randomUUID } = require('crypto');
const { SQSClient, SendMessageCommand } = require('@aws-sdk/client-sqs');

const sqsClient = AWSXRay.captureAWSv3Client(new SQSClient({}));
const QUEUE_URL = process.env.SQS_QUEUE_URL;

function respond(statusCode, body) {
  return {
    statusCode,
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body)
  };
}

exports.handler = async (event) => {
  try {
    const body = JSON.parse(event.body || '{}');
    const reportType = body.ReportType || 'Employment';

    if (!['Employment', 'User'].includes(reportType)) {
      return respond(400, { message: 'ReportType must be "Employment" or "User"' });
    }
    if (reportType === 'User' && !body.UserID) {
      return respond(400, { message: 'UserID is required for a User report' });
    }

    const requestId = randomUUID();
    const job = {
      RequestID: requestId,
      ReportType: reportType,
      UserID: body.UserID || null,
      RequestedBy: body.RequestedBy || 'unknown',
      RequestedAt: new Date().toISOString()
    };

    await sqsClient.send(new SendMessageCommand({
      QueueUrl: QUEUE_URL,
      MessageBody: JSON.stringify(job),
      MessageAttributes: {
        reportType: { DataType: 'String', StringValue: reportType }
      }
    }));

    console.log('Queued report job', requestId, reportType);

    return respond(202, {
      message: 'Report generation has been queued.',
      RequestID: requestId,
      ReportType: reportType
    });

  } catch (err) {
    console.error('report-request error:', err);
    return respond(500, { message: 'Internal server error', detail: err.message });
  }
};
