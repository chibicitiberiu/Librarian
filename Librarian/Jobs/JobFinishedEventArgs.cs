using System;

namespace Librarian.Jobs
{
    public class JobFinishedEventArgs : EventArgs
    {
        public Guid JobId { get; }

        public JobFinishedEventArgs(Guid jobId)
        {
            JobId = jobId;
        }
    }
}
