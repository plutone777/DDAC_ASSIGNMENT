const AWSXRay = require('aws-xray-sdk-core');
const sql = require('mssql');
const { SNSClient, PublishCommand } = require('@aws-sdk/client-sns');

const snsClient = AWSXRay.captureAWSv3Client(new SNSClient({}));
const TOPIC_ARN = process.env.SNS_TOPIC_ARN;

const dbConfig = {
  server: process.env.DB_SERVER,
  database: process.env.DB_NAME,
  user: process.env.DB_USER,
  password: process.env.DB_PASSWORD,
  port: parseInt(process.env.DB_PORT || '1433', 10),
  options: { encrypt: true, trustServerCertificate: true },
  pool: { max: 5, min: 0, idleTimeoutMillis: 30000 },
  connectionTimeout: 15000,
  requestTimeout: 15000
};

let poolPromise = null;
function getPool() {
  if (!poolPromise) {
    poolPromise = new sql.ConnectionPool(dbConfig).connect().catch(err => {
      poolPromise = null;
      throw err;
    });
  }
  return poolPromise;
}

function respond(statusCode, body) {
  return {
    statusCode,
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body)
  };
}

exports.handler = async (event) => {
  const method = event.requestContext?.http?.method || event.httpMethod;

  try {
    const pool = await getPool();

    if (method === 'GET') {
      const result = await pool.request().query(
        'SELECT EmployerID, CompanyName, Industry, CompanyDescription, Address, ' +
        'Website, VerificationStatus FROM EmployerProfiles ' +
        "WHERE VerificationStatus = 'Pending'"
      );
      return respond(200, result.recordset);
    }

    if (method === 'POST') {
      const body = JSON.parse(event.body || '{}');
      const employerId = parseInt(body.EmployerID, 10);
      const decision = body.Decision;

      if (!employerId) return respond(400, { message: 'EmployerID is required' });
      if (!['Approved', 'Rejected'].includes(decision)) {
        return respond(400, { message: 'Decision must be "Approved" or "Rejected"' });
      }

      const result = await pool.request()
        .input('id', sql.Int, employerId)
        .input('status', sql.NVarChar(20), decision)
        .query(
          'UPDATE EmployerProfiles SET VerificationStatus = @status ' +
          'OUTPUT INSERTED.EmployerID, INSERTED.CompanyName, INSERTED.VerificationStatus ' +
          'WHERE EmployerID = @id'
        );

      if (result.recordset.length === 0) {
        return respond(404, { message: 'Employer not found' });
      }
      const employer = result.recordset[0];

      const userRes = await pool.request()
        .input('id', sql.Int, employerId)
        .query('SELECT FullName, Email FROM Users WHERE UserID = @id');
      const contact = userRes.recordset[0] || {};

      if (TOPIC_ARN) {
        await snsClient.send(new PublishCommand({
          TopicArn: TOPIC_ARN,
          Subject: ('Employer ' + decision.toLowerCase() + ': ' + employer.CompanyName).substring(0, 100),
          Message:
            `An employer account has been reviewed on the Job Matching Platform.\n\n` +
            `Company: ${employer.CompanyName}\n` +
            `Contact: ${contact.FullName || '-'} (${contact.Email || '-'})\n` +
            `Decision: ${decision}\n` +
            `Reviewed at: ${new Date().toISOString()}\n\n` +
            (decision === 'Approved'
              ? 'This employer can now post job vacancies.\n'
              : 'This employer has been rejected and cannot post job vacancies.\n'),
          MessageAttributes: {
            eventType: { DataType: 'String', StringValue: 'EMPLOYER_' + decision.toUpperCase() }
          }
        }));
      }

      return respond(200, {
        EmployerID: employer.EmployerID,
        CompanyName: employer.CompanyName,
        VerificationStatus: employer.VerificationStatus,
        NotificationSent: Boolean(TOPIC_ARN)
      });
    }

    return respond(405, { message: `Method ${method} not supported on this route` });

  } catch (err) {
    console.error('employer-verification error:', err);
    return respond(500, { message: 'Internal server error', detail: err.message });
  }
};
