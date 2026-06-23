namespace Librarian.ApiModels
{
    public class JobsSummary
    {
        public string? Message { get; set; }
        public int RunningJobCount { get; set; }
        public float? Progress { get; set; }
    }
}
