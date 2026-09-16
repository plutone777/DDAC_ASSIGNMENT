using System.Net.Http.Json;
using System.Text.Json;
using DDAC.Models;
using DDAC.Models.Admin;

namespace DDAC.Services
{
    public class AdminApiClient
    {
        private readonly HttpClient _http;
        private readonly ILogger<AdminApiClient> _logger;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public AdminApiClient(HttpClient http, ILogger<AdminApiClient> logger)
        {
            _http = http;
            _logger = logger;
        }

        public async Task<List<Announcement>> GetAnnouncementsAsync()
        {
            var response = await _http.GetAsync("announcements");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<List<Announcement>>(JsonOptions)
                   ?? new List<Announcement>();
        }

        public async Task<Announcement?> GetAnnouncementAsync(int id)
        {
            var response = await _http.GetAsync($"announcements/{id}");
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<Announcement>(JsonOptions);
        }

        public async Task<Announcement?> CreateAnnouncementAsync(Announcement announcement)
        {
            var payload = new
            {
                announcement.AdminID,
                announcement.Title,
                announcement.Content,
                announcement.Status
            };

            var response = await _http.PostAsJsonAsync("announcements", payload, JsonOptions);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<Announcement>(JsonOptions);
        }

        public async Task<Announcement?> UpdateAnnouncementAsync(int id, Announcement announcement)
        {
            var payload = new
            {
                announcement.Title,
                announcement.Content,
                announcement.Status
            };

            var response = await _http.PutAsJsonAsync($"announcements/{id}", payload, JsonOptions);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<Announcement>(JsonOptions);
        }

        public async Task<bool> DeleteAnnouncementAsync(int id)
        {
            var response = await _http.DeleteAsync($"announcements/{id}");
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return false;
            response.EnsureSuccessStatusCode();
            return true;
        }

        public async Task<List<EmployerProfile>> GetPendingEmployersAsync()
        {
            var response = await _http.GetAsync("employer-verification");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<List<EmployerProfile>>(JsonOptions)
                   ?? new List<EmployerProfile>();
        }

        public async Task<VerificationResult?> DecideEmployerAsync(int employerId, string decision)
        {
            var payload = new { EmployerID = employerId, Decision = decision };
            var response = await _http.PostAsJsonAsync("employer-verification", payload, JsonOptions);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<VerificationResult>(JsonOptions);
        }

        public async Task<ReportRequestResult?> RequestReportAsync(
            string reportType, int? userId, string requestedBy)
        {
            var payload = new { ReportType = reportType, UserID = userId, RequestedBy = requestedBy };
            var response = await _http.PostAsJsonAsync("reports", payload, JsonOptions);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<ReportRequestResult>(JsonOptions);
        }

        public async Task<List<ReportSummary>> GetReportsAsync()
        {
            var response = await _http.GetAsync("reports");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<List<ReportSummary>>(JsonOptions)
                   ?? new List<ReportSummary>();
        }
    }
}

