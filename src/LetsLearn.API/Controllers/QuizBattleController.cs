using System.Security.Claims;
using LetsLearn.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LetsLearn.API.Controllers;

[ApiController]
[Authorize]
[Route("quiz-battles")]
[Route("api/quiz-battles")]
public sealed class QuizBattleController(QuizBattleService service) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] BattleCreateRequest request, CancellationToken ct) =>
        await Execute(() => service.CreateAsync(UserId(), request, ct));

    [HttpPost("join/{code}")]
    public async Task<IActionResult> Join(string code, CancellationToken ct) =>
        await Execute(() => service.JoinAsync(UserId(), code, ct));

    [HttpPost("{id:guid}/start")]
    public async Task<IActionResult> Start(Guid id, CancellationToken ct) =>
        await Execute(() => service.StartAsync(id, UserId(), ct));

    [HttpPost("{id:guid}/end")]
    public async Task<IActionResult> End(Guid id, CancellationToken ct) =>
        await Execute(() => service.EndEarlyAsync(id, UserId(), ct));

    [HttpPost("{id:guid}/rematch")]
    public async Task<IActionResult> Rematch(Guid id, CancellationToken ct) =>
        await Execute(() => service.RematchAsync(id, UserId(), ct));

    [HttpDelete("{id:guid}/leave")]
    public async Task<IActionResult> Leave(Guid id, CancellationToken ct) =>
        await Execute(() => service.LeaveAsync(id, UserId(), ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) =>
        await Execute(() => service.GetAsync(id, UserId(), ct));

    [HttpPost("{id:guid}/answers")]
    public async Task<IActionResult> Answer(Guid id, [FromBody] BattleAnswerRequest request, CancellationToken ct) =>
        await Execute(() => service.AnswerAsync(id, UserId(), request, ct));

    [HttpGet("{id:guid}/results")]
    public async Task<IActionResult> Results(Guid id, CancellationToken ct) =>
        await Execute(() => service.ResultsAsync(id, UserId(), ct));

    [HttpGet("/leaderboard")]
    [HttpGet("/api/leaderboard")]
    public async Task<IActionResult> Leaderboard([FromQuery] string period = "all", [FromQuery] string? courseId = null, CancellationToken ct = default) =>
        await Execute(() => service.LeaderboardAsync(period, courseId, UserId(), ct));

    private Guid UserId()
    {
        var value = User.Claims.FirstOrDefault(c => c.Type == "userID")?.Value;
        if (!Guid.TryParse(value, out var id) || id == Guid.Empty) throw new UnauthorizedAccessException("Sign in to use Quiz Battle.");
        return id;
    }

    private async Task<IActionResult> Execute<T>(Func<Task<T>> operation)
    {
        try { return Ok(await operation()); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }
}
