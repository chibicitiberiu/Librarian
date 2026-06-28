using System;
using System.Threading;

namespace Librarian.Jobs
{
    public class JobToken
    {
        private int done;

        public Guid JobId { get; }

        public int TotalUnits { get; set; }

        public int Done => done;

        public event EventHandler<JobProgressEventArgs>? Progressed;

        public event EventHandler<JobFinishedEventArgs>? Finished;

        public JobToken(Guid jobId, int totalUnits)
        {
            JobId = jobId;
            TotalUnits = totalUnits;
            done = 0;
        }

        // Indexing advances this from multiple threads (files are extracted in parallel), so the counter
        // is updated atomically.
        public void AdvanceProgress(int amount, string? message)
        {
            int now = Interlocked.Add(ref done, amount);
            int total = TotalUnits;
            float fraction = total > 0 ? (float)now / total : 0f;
            Progressed?.Invoke(this, new JobProgressEventArgs(JobId, fraction, message));
        }

        public void Finish()
        {
            Finished?.Invoke(this, new JobFinishedEventArgs(JobId));
        }
    }
}
