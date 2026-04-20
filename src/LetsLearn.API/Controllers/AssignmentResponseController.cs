using LetsLearn.UseCases.DTOs;
using LetsLearn.UseCases.ServiceInterfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LetsLearn.API.Controllers
{
    [ApiController]
    [Route("topic/{topicId:guid}/assignment-response")]
    [Authorize]
    public class AssignmentResponseController : ControllerBase
    {
        private readonly IAssignmentResponseService _assignmentResponseService;

        public AssignmentResponseController(IAssignmentResponseService assignmentResponseService)
        {
            _assignmentResponseService = assignmentResponseService;
        }

        [HttpPost]
        public async Task<ActionResult<AssignmentResponseDTO>> CreateAssignmentResponse([FromBody] CreateAssignmentResponseRequest dto, CancellationToken ct = default)
        {
            var userId = Guid.Parse(User.Claims.First(c => c.Type == "userID").Value);

            var result = await _assignmentResponseService.CreateAssigmentResponseAsync(dto, userId);
            return Ok(result);
        }

        [HttpGet("getAll")]
        public async Task<ActionResult<IEnumerable<AssignmentResponseDTO>>> GetAllAssignmentResponsesByTopicId([FromRoute] Guid topicId, [FromQuery] Guid? studentId, CancellationToken ct = default)
        {
            var res = await _assignmentResponseService.GetAllAssigmentResponseByTopicIdAsync(topicId);
            return Ok(res);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<AssignmentResponseDTO>> GetAssignmentResponseById([FromRoute] Guid id, CancellationToken ct = default)
        {
            var res = await _assignmentResponseService.GetAssigmentResponseByIdAsync(id);
            return Ok(res);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<AssignmentResponseDTO>> UpdateAssignmentResponseById([FromRoute] Guid id, [FromBody] UpdateAssignmentResponseRequest dto, CancellationToken ct = default)
        {
            var result = await _assignmentResponseService.UpdateAssigmentResponseByIdAsync(id, dto);
            return Ok(result);
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteAssignmentResponseById([FromRoute] Guid id, CancellationToken ct = default)
        {
            await _assignmentResponseService.DeleteAssigmentResponseAsync(id);
            return NoContent();
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<AssignmentResponseDTO>>> GetAssignmentResponsesByTopic(
            [FromRoute] Guid topicId,
            [FromQuery] Guid? studentId = null,
            CancellationToken ct = default)
        {
            var res = await _assignmentResponseService.GetAllAssigmentResponseByTopicIdAsync(topicId);
            return Ok(res);
        }
    }
}