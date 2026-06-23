using Librarian.ApiModels;
using Librarian.Jobs;
using Librarian.Resources;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Net.Mime;

namespace Librarian.Controllers.Api
{
    [ApiController]
    [Route("api/{controller}")]
    [Produces(MediaTypeNames.Application.Json)]
    public class JobsController : ControllerBase
    {
        private readonly JobTracker jobTracker;

        public JobsController(JobTracker jobTracker)
        {
            this.jobTracker = jobTracker;
        }

        [HttpGet]
        public ActionResult<JobsSummary> GetJobsSummary()
        {
            var jobs = jobTracker.Jobs.ToArray();

            var result = new JobsSummary()
            {
                RunningJobCount = jobs.Length,
            };

            if (jobs.Length == 1)
            {
                result.Message = jobs[0].Value.Name;
                if (!string.IsNullOrEmpty(jobs[0].Value.LastStatus))
                    result.Message += $"({jobs[0].Value.LastStatus})";

                result.Progress = jobs[0].Value.LastProgress;
            }
            else if (jobs.Length > 1)
            {
                result.Message = string.Format(Strings.RunningXJobs, jobs.Length);
                result.Progress = jobs.Average(x => x.Value.LastProgress);
            }

            return result;
        }
    }
}
