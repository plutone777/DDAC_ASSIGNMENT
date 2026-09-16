namespace DDAC.Models.Admin
{
    public class ReportRequestResult
    {
        public string Message { get; set; } = string.Empty;
        public string RequestID { get; set; } = string.Empty;
        public string ReportType { get; set; } = string.Empty;
    }

    public class ReportSummary
    {
        public string Key { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public DateTime LastModified { get; set; }
        public string DownloadUrl { get; set; } = string.Empty;
    }

    public class VerificationResult
    {
        public int EmployerID { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public string VerificationStatus { get; set; } = string.Empty;
        public bool NotificationSent { get; set; }
    }
}
