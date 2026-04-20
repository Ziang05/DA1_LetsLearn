using LetsLearn.Core.Entities;
using LetsLearn.UseCases.DTOs;
using LetsLearn.UseCases.ServiceInterfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("course/{courseId}/topic/{topicId}/comments")]
[Authorize]
public class CommentController : ControllerBase
{
    private readonly ICommentService _commentService;

    public CommentController(ICommentService commentService)
    {
        _commentService = commentService;
    }

    // POST: /course/{courseId}/topic/{topicId}/comments
    [HttpPost]
    public async Task<ActionResult<GetCommentResponse>> AddComment(
        string courseId,
        Guid topicId,
        [FromBody] CreateCommentRequest createcommentDTO,
        CancellationToken ct = default)
    {
        var userId = Guid.Parse(User.Claims.First(c => c.Type == "userID").Value);
        createcommentDTO.TopicId = topicId;
        var createdComment = await _commentService.AddCommentAsync(userId, createcommentDTO, ct);
        return Ok(createdComment);
    }

    // GET: /course/{courseId}/topic/{topicId}/comments
    [HttpGet]
    public async Task<ActionResult<List<GetCommentResponse>>> GetComments(
        string courseId,
        Guid topicId,
        CancellationToken ct = default)
    {
        var comments = await _commentService.GetCommentsByTopicAsync(topicId, ct);
        return Ok(comments);
    }

    // DELETE: /course/{courseId}/topic/{topicId}/comments/{commentId}
    [HttpDelete("{commentId:guid}")]
    public async Task<ActionResult> DeleteComment(
        string courseId,
        Guid topicId,
        Guid commentId,
        CancellationToken ct = default)
    {
        await _commentService.DeleteCommentAsync(commentId, ct);
        return NoContent();
    }
}