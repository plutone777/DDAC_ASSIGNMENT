namespace DDAC.Options;

// Supply through runtime configuration, e.g. EmployerInterview__BaseUrl / __CallerKey.
public sealed class EmployerInterviewOptions
{
    public string BaseUrl { get; set; } = "";
    public string CallerKey { get; set; } = "";
}
