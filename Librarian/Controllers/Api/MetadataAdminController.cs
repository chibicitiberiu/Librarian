using Librarian.Services;
using Microsoft.AspNetCore.Mvc;
using System.Net.Mime;
using System.Threading.Tasks;

namespace Librarian.Controllers.Api
{
    [ApiController]
    [Route("api/metadata")]
    [Produces(MediaTypeNames.Application.Json)]
    public class MetadataAdminController : ControllerBase
    {
        private readonly RenormalizationService renormalizationService;

        public MetadataAdminController(RenormalizationService renormalizationService)
        {
            this.renormalizationService = renormalizationService;
        }

        /// <summary>
        /// Rebuilds canonical attributes from the stored raw metadata using the current
        /// rules, without re-reading any files.
        /// </summary>
        [HttpPost("renormalize")]
        public async Task<IActionResult> Renormalize()
        {
            int produced = await renormalizationService.RenormalizeAllAsync();
            return Ok(new { reprocessed = produced });
        }
    }
}
