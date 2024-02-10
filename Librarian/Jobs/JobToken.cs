using System;

namespace Librarian.Jobs
{
    public class JobToken
    {
        public Guid JobId { get; }

        public int TotalUnits { get; set; }

        public int Done { get; private set; }

        public event EventHandler<JobProgressEventArgs>? Progressed;

        public event EventHandler<JobFinishedEventArgs>? Finished;

        public JobToken(Guid jobId, int totalUnits)
        {
            JobId = jobId;
            TotalUnits = totalUnits;
            Done = 0;
        }

        public void AdvanceProgress(int amount, string? message)
        {
            Done += amount;
            Progressed?.Invoke(this, new JobProgressEventArgs(JobId, Convert.ToSingle(Done) / Convert.ToSingle(TotalUnits), message));
        }

        public void Finish()
        {
            Finished?.Invoke(this, new JobFinishedEventArgs(JobId));
        }
    }
}
