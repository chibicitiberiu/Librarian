using System;

namespace Librarian.Jobs
{
    public class JobDetails
    {
        public Guid Id { get; }
        public string Name { get; }
        public string? LastStatus { get; set; }
        public float LastProgress { get; set; } = 0;
        public DateTimeOffset StartedTime { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? CompletedTime { get; set; }

        public JobDetails(Guid id, string name)
        {
            Id = id;
            Name = name;
        }
    }
}
