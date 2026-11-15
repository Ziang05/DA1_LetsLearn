using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using LetsLearn.API.Services;

namespace LetsLearn.API.Hubs;

[Authorize]
public sealed class QuizBattleHub(QuizBattleService battleService) : Hub
{
    public async Task JoinBattle(string battleId)
    {
        if (!Guid.TryParse(battleId, out var id)) throw new HubException("Invalid battle ID.");
        var userId = GetUserId();
        try { await battleService.GetAsync(id, userId, Context.ConnectionAborted); }
        catch (KeyNotFoundException ex) { throw new HubException(ex.Message); }
        catch (UnauthorizedAccessException ex) { throw new HubException(ex.Message); }
        await Groups.AddToGroupAsync(Context.ConnectionId, Room(id));
    }

    private Guid GetUserId()
    {
        var raw = Context.User?.FindFirst("userID")?.Value ?? Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(raw, out var id) && id != Guid.Empty ? id : throw new HubException("Unauthorized.");
    }
    private static string Room(Guid id) => $"quiz-battle:{id:N}";
}
