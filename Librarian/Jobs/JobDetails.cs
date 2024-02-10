using System;

namespace Librarian.Jobs
{
    public class JobDetails
    {
        public Guid Id { get; }
        public string Name { get; }
        public string? LastStatus { get; set; }
        public float LastProgress { get; set; } = 0;

        public JobDetails(Guid id, string name)
        {
            Id = id;
            Name = name;
        }
    }
}
