using Microsoft.Data.SqlClient;

namespace DDAC.Employer.Interviews.LocalApi;

internal static class LocalDatabase
{
    // Intentionally not configurable: this host must never fall back to team/RDS settings.
    internal const string Connection = @"Server=(localdb)\MSSQLLocalDB;Database=DDAC_Employer_LocalTest;Trusted_Connection=True;TrustServerCertificate=True;Connect Timeout=10;";

    internal static async Task VerifyAsync()
    {
        await using var connection = new SqlConnection(Connection);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT CASE WHEN DB_NAME() = N'DDAC_Employer_LocalTest' AND CONVERT(int, SERVERPROPERTY('IsLocalDB')) = 1 THEN 1 ELSE 0 END";
        if (Convert.ToInt32(await command.ExecuteScalarAsync()) != 1)
            throw new InvalidOperationException("Local target verification failed.");
    }
}
