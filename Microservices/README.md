# Task #2 - Admin Microservices (AWS account 183364521779, us-east-1)

Five Lambda functions behind one API Gateway HTTP API, one SQS queue, one SNS
topic and one S3 bucket. The three functions that touch the database run inside
the VPC; the two stateless ones do not. All five have X-Ray active tracing.

## What gets built

| Microservice | Trigger | AWS services | In VPC | Replaces (Task #1) |
|---|---|---|---|---|
| `ddac-announcement-service` | API Gateway | RDS, SNS | yes | `AdminController` EF calls on `Announcements` |
| `ddac-employer-verification` | API Gateway | RDS, SNS | yes | `ApproveEmployer` / `RejectEmployer` |
| `ddac-report-request` | API Gateway | SQS | no | new front door for reports |
| `ddac-report-generator` | SQS | RDS, S3, SNS | yes | `GenerateEmploymentReport`, `GenerateUserReport` |
| `ddac-report-list` | API Gateway | S3 | no | new archive view |

Report flow: MVC app -> POST /reports -> `ddac-report-request` -> SQS
`ddac-report-queue` -> `ddac-report-generator` -> S3 file + SNS email.

## Resources

- SNS topic: `ddac-admin-notifications`, email subscription confirmed.
- SQS queue: `ddac-report-queue`, Standard, visibility timeout 60 s.
- S3 bucket: `ddac-admin-reports-183364521779`, reports under `reports/`,
  Block Public Access on, downloads via pre-signed URLs (1 hour).
- IAM role: `ddac-lambda-role` with `iam/ddac-lambda-policy.json`,
  `AWSLambdaBasicExecutionRole`, `AWSLambdaVPCAccessExecutionRole` and
  `AWSXRayDaemonWriteAccess`.
- Lambda layer: `ddac-node-deps` from `layer/ddac-node-deps.zip`
  (mssql, AWS SDK v3 S3/SNS/SQS clients, s3-request-presigner, aws-xray-sdk-core).
  Compatible runtime Node.js 22.x.
- RDS: `ddac-rds` (SQL Server Express), database `DDAC`, security group
  `ddac-rds-sg`.
- API Gateway: HTTP API `ddac-admin-api`, `$default` stage with auto-deploy,
  invoke URL `https://c02zy1d2ch.execute-api.us-east-1.amazonaws.com`.

## Lambda configuration

Runtime Node.js 22.x, role `ddac-lambda-role`, layer `ddac-node-deps`,
Active tracing on (Configuration -> Monitoring and operations tools).
Paste the matching `index.js` from this folder into the console editor and
click Deploy.

Shared environment variables for the three database functions:

```
DB_SERVER   = ddac-rds.cixuca6gwirw.us-east-1.rds.amazonaws.com
DB_NAME     = DDAC
DB_USER     = admin
DB_PASSWORD = <RDS password>
DB_PORT     = 1433
```

| Function | Extra env vars | Timeout | Memory |
|---|---|---|---|
| `ddac-announcement-service` | `SNS_TOPIC_ARN` | 30 s | 256 MB |
| `ddac-employer-verification` | `SNS_TOPIC_ARN` | 30 s | 256 MB |
| `ddac-report-request` | `SQS_QUEUE_URL` | 10 s | 128 MB |
| `ddac-report-generator` | `S3_BUCKET`, `SNS_TOPIC_ARN` | 60 s | 512 MB |
| `ddac-report-list` | `S3_BUCKET` | 15 s | 256 MB |

SQS trigger on `ddac-report-generator` only: `ddac-report-queue`, batch size 1,
Report batch item failures ticked.

## VPC setup for the database functions

A Lambda inside a VPC has no internet access, so every AWS service it calls
needs a private path:

- S3 gateway endpoint `vpce-0b0f84f3cdb5cec87` on the VPC route table (free).
- SNS interface endpoint `vpce-094ee0924827f91ee` in subnets us-east-1d and
  us-east-1e, private DNS on, security group `ddac-endpoint-sg`
  (inbound HTTPS 443 from `ddac-lambda-sg`).
- `ddac-announcement-service`, `ddac-employer-verification` and
  `ddac-report-generator`: Configuration -> VPC -> `vpc-05d77f18f39080204`,
  subnets `subnet-041ae18c80f75c7be` (1d) and `subnet-07080c9ec5f922771` (1e),
  security group `ddac-lambda-sg`.
- `ddac-rds-sg` inbound TCP 1433 allows only: `ddac-lambda-sg`, the Elastic
  Beanstalk instance security group, the VPC default security group and the
  team members' laptop IPs. The former `0.0.0.0/0` rule has been removed.

`ddac-report-request` and `ddac-report-list` stay outside the VPC: they never
touch the database, and reaching SQS from inside would need a paid interface
endpoint.

## API Gateway routes

| Method | Route | Integration |
|---|---|---|
| GET | `/announcements` | `ddac-announcement-service` |
| GET | `/announcements/{id}` | `ddac-announcement-service` |
| POST | `/announcements` | `ddac-announcement-service` |
| PUT | `/announcements/{id}` | `ddac-announcement-service` |
| DELETE | `/announcements/{id}` | `ddac-announcement-service` |
| GET | `/employer-verification` | `ddac-employer-verification` |
| POST | `/employer-verification` | `ddac-employer-verification` |
| POST | `/reports` | `ddac-report-request` |
| GET | `/reports` | `ddac-report-list` |

## The MVC app side

`DDAC/appsettings.json`:

```json
"Microservices": {
  "Enabled": true,
  "BaseUrl": "https://c02zy1d2ch.execute-api.us-east-1.amazonaws.com"
}
```

`Enabled: true` routes the Admin module through `AdminApiClient` to the API.
`Enabled: false` runs the original Task #1 EF code paths in the same build, and
if the API is unreachable the controller falls back to EF automatically. This
switch is what makes the Section 3 comparison clean: only the architecture
changes between the two runs.

## Smoke test

```powershell
.\test-api.ps1
```

Every line should print a 2xx status. A 500 means the Lambda ran but failed:
check CloudWatch -> Log groups -> `/aws/lambda/<function-name>`. A timeout on
a database route means the VPC or `ddac-rds-sg` configuration.

Then in the app: publish an announcement, approve an employer and generate a
report. Each should produce an SNS email, and the report appears in the archive
after a few seconds with a download link.

## Monitoring

CloudWatch -> Application Signals -> Trace Map (Last 30 minutes) shows the
API -> Lambda -> SQS -> Lambda -> S3 + SNS chain. Useful metrics for the
Task #1 vs Task #2 comparison: Elastic Beanstalk `TargetResponseTime` and
`RequestCount`, Lambda `Duration`, `Invocations`, `Errors` and
`ConcurrentExecutions`, SQS `ApproximateAgeOfOldestMessage` and
`NumberOfMessagesSent`, RDS `CPUUtilization` and `DatabaseConnections`.
