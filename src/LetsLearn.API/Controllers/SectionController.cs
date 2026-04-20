using LetsLearn.UseCases.DTOs;
using LetsLearn.UseCases.ServiceInterfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LetsLearn.API.Controllers
{
    [ApiController]
    [Route("[controller]")]
    [Authorize]
    public class SectionController : ControllerBase
    {
        private readonly ISectionService _sectionService;
        private readonly ILogger<SectionController> _logger;

        public SectionController(ISectionService sectionService, ILogger<SectionController> logger)
        {
            _sectionService = sectionService;
            _logger = logger;
        }

        /// <summary>
        /// POST /api/sections — Create a new Section (optionally with its Topic list).
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(SectionResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<SectionResponse>> Create([FromBody] CreateSectionRequest request, CancellationToken ct)
        {
            try
            {
                if (request is null) throw new ArgumentNullException(nameof(request));
                var result = await _sectionService.CreateSectionAsync(request, ct);

                // Return 201 Created + Location header pointing to GET /api/sections/{id}
                return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
            }
            catch (ArgumentNullException ex)
            {
                _logger.LogWarning(ex, "CreateSection: bad request");
                return BadRequest(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "CreateSection: invalid argument");
                return BadRequest(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "CreateSection: invalid operation");
                return Conflict(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CreateSection: unhandled");
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Internal server error." });
            }
        }

        /// <summary>
        /// GET /api/sections/{id} — Get a Section by Id (including its Topics).
        /// </summary>
        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(SectionResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<SectionResponse>> GetById([FromRoute] Guid id, CancellationToken ct)
        {
            try
            {
                var result = await _sectionService.GetSectionByIdAsync(id, ct);
                return Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogInformation(ex, "GetSectionById: not found {SectionId}", id);
                return NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetSectionById: unhandled {SectionId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Internal server error." });
            }
        }

        /// <summary>
        /// PUT /api/sections/{id} — Update a Section and synchronize its Topics
        /// (remove topics not present in DTO; upsert the rest).
        /// </summary>
        [HttpPut("{id:guid}")]
        [ProducesResponseType(typeof(SectionResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<SectionResponse>> Update([FromRoute] Guid id, [FromBody] UpdateSectionRequest request, CancellationToken ct)
        {
            try
            {
                if (request is null) throw new ArgumentNullException(nameof(request));
                if (id != request.Id)
                {
                    _logger.LogWarning("UpdateSection: mismatched IDs. Route ID: {RouteId}, Body ID: {BodyId}", id, request.Id);
                    return BadRequest(new { error = "The ID in the URL must match the ID in the request body" });
                }
                // Ensure Topics is non-null to avoid null-coalescing issues later
                request.Topics = request.Topics == null ? new List<TopicUpsertDTO>() : request.Topics;

                var result = await _sectionService.UpdateSectionAsync(request, ct);
                return Ok(result);
            }
            catch (ArgumentNullException ex)
            {
                _logger.LogWarning(ex, "UpdateSection: bad request {SectionId}", request.Id);
                return BadRequest(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "UpdateSection: invalid argument {SectionId}", request.Id);
                return BadRequest(new { error = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogInformation(ex, "UpdateSection: not found {SectionId}", request.Id);
                return NotFound(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "UpdateSection: invalid operation {SectionId}", request.Id);
                return Conflict(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UpdateSection: unhandled {SectionId}", request.Id);
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Internal server error." });
            }
        }
    }
}
