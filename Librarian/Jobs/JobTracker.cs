using System;
using System.Collections.Concurrent;

namespace Librarian.Jobs
{
    public class JobTracker
    {
        public ConcurrentDictionary<Guid, JobDetails> Jobs { get; } = new();

        public JobToken StartJob(string jobName)
        {
            Guid id;
            do
            {
                id = Guid.NewGuid();
            }
            while (!Jobs.TryAdd(id, new JobDetails(id, jobName)));

            var token = new JobToken(id, 1);
            token.Progressed += OnJobProgressed;
            token.Finished += OnJobFinished;
            return token;
        }

        private void OnJobFinished(object? sender, JobFinishedEventArgs e)
        {
            if (Jobs.TryRemove(e.JobId, out var jobDetails))
            {
                jobDetails.CompletedTime = DateTimeOffset.UtcNow;
            }
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
