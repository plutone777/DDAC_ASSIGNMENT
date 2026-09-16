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

async function publishToSns(announcement) {
  if (!TOPIC_ARN) return;
  await snsClient.send(new PublishCommand({
    TopicArn: TOPIC_ARN,
    Subject: `New announcement: ${announcement.Title}`.substring(0, 100),
    Message:
      `A new announcement has been published on the Job Matching Platform.\n\n` +
      `Title: ${announcement.Title}\n` +
      `Published: ${announcement.PublishedDate}\n\n` +
      `${announcement.Content}\n`,
    MessageAttributes: {
      eventType: { DataType: 'String', StringValue: 'ANNOUNCEMENT_PUBLISHED' }
    }
  }));
}

exports.handler = async (event) => {
  const method = event.requestContext?.http?.method || event.httpMethod;
  const id = event.pathParameters?.id;

  try {
    const pool = await getPool();

    if (method === 'GET' && !id) {
      const result = await pool.request().query(
        'SELECT AnnouncementID, AdminID, Title, Content, PublishedDate, Status ' +
        'FROM Announcements ORDER BY PublishedDate DESC'
      );
      return respond(200, result.recordset);
    }

    if (method === 'GET' && id) {
      const result = await pool.request()
        .input('id', sql.Int, parseInt(id, 10))
        .query(
          'SELECT AnnouncementID, AdminID, Title, Content, PublishedDate, Status ' +
          'FROM Announcements WHERE AnnouncementID = @id'
        );
      if (result.recordset.length === 0) return respond(404, { message: 'Announcement not found' });
      return respond(200, result.recordset[0]);
    }

    if (method === 'POST') {
      const body = JSON.parse(event.body || '{}');
      if (!body.Title || !body.Content) {
        return respond(400, { message: 'Title and Content are required' });
      }

      const publishedDate = new Date();
      const status = body.Status === 'Draft' ? 'Draft' : 'Published';

      const result = await pool.request()
        .input('adminId', sql.Int, body.AdminID || 0)
        .input('title', sql.NVarChar(200), body.Title)
        .input('content', sql.NVarChar(sql.MAX), body.Content)
        .input('publishedDate', sql.DateTime2, publishedDate)
        .input('status', sql.NVarChar(20), status)
        .query(
          'INSERT INTO Announcements (AdminID, Title, Content, PublishedDate, Status) ' +
          'OUTPUT INSERTED.AnnouncementID, INSERTED.AdminID, INSERTED.Title, ' +
          'INSERTED.Content, INSERTED.PublishedDate, INSERTED.Status ' +
          'VALUES (@adminId, @title, @content, @publishedDate, @status)'
        );

      const created = result.recordset[0];
      if (created.Status === 'Published') {
        await publishToSns(created);
      }
      return respond(201, created);
    }

    if (method === 'PUT' && id) {
      const body = JSON.parse(event.body || '{}');
      if (!body.Title || !body.Content) {
        return respond(400, { message: 'Title and Content are required' });
      }

      const status = body.Status === 'Draft' ? 'Draft' : 'Published';

      const existing = await pool.request()
        .input('id', sql.Int, parseInt(id, 10))
        .query('SELECT Status FROM Announcements WHERE AnnouncementID = @id');
      if (existing.recordset.length === 0) return respond(404, { message: 'Announcement not found' });
      const wasPublished = existing.recordset[0].Status === 'Published';

      const result = await pool.request()
        .input('id', sql.Int, parseInt(id, 10))
        .input('title', sql.NVarChar(200), body.Title)
        .input('content', sql.NVarChar(sql.MAX), body.Content)
        .input('status', sql.NVarChar(20), status)
        .query(
          'UPDATE Announcements SET Title = @title, Content = @content, Status = @status ' +
          'OUTPUT INSERTED.AnnouncementID, INSERTED.AdminID, INSERTED.Title, ' +
          'INSERTED.Content, INSERTED.PublishedDate, INSERTED.Status ' +
          'WHERE AnnouncementID = @id'
        );

      const updated = result.recordset[0];
      if (updated.Status === 'Published' && !wasPublished) {
        await publishToSns(updated);
      }
      return respond(200, updated);
    }

    if (method === 'DELETE' && id) {
      const result = await pool.request()
        .input('id', sql.Int, parseInt(id, 10))
        .query('DELETE FROM Announcements WHERE AnnouncementID = @id');
      if (result.rowsAffected[0] === 0) return respond(404, { message: 'Announcement not found' });
      return respond(200, { message: 'Announcement deleted', AnnouncementID: parseInt(id, 10) });
    }

    return respond(405, { message: `Method ${method} not supported on this route` });

  } catch (err) {
    console.error('announcement-service error:', err);
    return respond(500, { message: 'Internal server error', detail: err.message });
  }
};
