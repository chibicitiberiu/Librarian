using System;

namespace Librarian.Jobs
{
    public class JobProgressEventArgs : EventArgs
    {
        public Guid JobId { get; }

        public float Progress { get; }

        public string? Message { get; }

        public JobProgressEventArgs(Guid jobId, float percentage, string? message)
        {
            JobId = jobId;
            Progress = percentage;
            Message = message;
        }
    }
}
