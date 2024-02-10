using System;
using System.Collections.Generic;

namespace Librarian.Jobs
{
    public class JobTracker
    {
        public Dictionary<Guid, JobDetails> Jobs { get; } = new();

        public JobToken StartJob(string jobName)
        {
            Guid id = Guid.NewGuid();
            Jobs.Add(id, new JobDetails(id, jobName));

            var token = new JobToken(Guid.NewGuid(), 1);
            token.Progressed += OnJobProgressed;
            token.Finished += OnJobFinished;
            return token;
        }

        private void OnJobFinished(object? sender, JobFinishedEventArgs e)
        {
            Jobs.Remove(e.JobId);
        }

        private void OnJobProgressed(object? sender, JobProgressEventArgs e)
        {
            if (Jobs.TryGetValue(e.JobId, out JobDetails? jobDetails))
            {
                jobDetails.LastProgress = e.Progress;
                jobDetails.LastStatus = e.Message;
            }
        }
    }
}
