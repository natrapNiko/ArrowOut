using ArrowOut.Game;
using ArrowOut.Services.Gameplay;
using ArrowOut.Services.Levels;
using ArrowOut.Services.Models;
using ArrowOut.Web.Controllers.Api;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace ArrowOut.Tests.Web;

public class ApiControllerTests
{
    private readonly Mock<ILevelService> _levels = new();
    private readonly Mock<IGameService> _game = new();

    private LevelsApiController Controller(bool isAdmin = false) =>
        new LevelsApiController(_levels.Object, _game.Object).WithUser("user-1", isAdmin);

    [Fact]
    public async Task GetLevel_UsesTheAuthenticatedUserNotRequestData()
    {
        var dto = new LevelBoardDto(5, 1, "One", 4, 4, 3, Difficulty.Easy, []);
        _levels.Setup(s => s.GetBoardAsync(5, "user-1", false, It.IsAny<CancellationToken>())).ReturnsAsync(dto);

        var result = await Controller().GetLevel(5, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(dto, ok.Value);
    }

    [Fact]
    public async Task GetLevel_PassesAdminFlagFromRoles()
    {
        _levels.Setup(s => s.GetBoardAsync(5, "user-1", true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LevelBoardDto(5, 1, "One", 4, 4, 3, Difficulty.Easy, []));

        await Controller(isAdmin: true).GetLevel(5, CancellationToken.None);

        _levels.Verify(s => s.GetBoardAsync(5, "user-1", true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Complete_ForwardsTapsToTheReplayingService()
    {
        var request = new CompletionRequest { Taps = [3, 2, 1], HintsUsed = 1 };
        var expected = new CompletionResult(7, 3, 0, true, true, 8);
        _game.Setup(s => s.SubmitCompletionAsync(7, request, "user-1", false, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await Controller().Complete(7, request, CancellationToken.None);

        Assert.Same(expected, Assert.IsType<OkObjectResult>(result.Result).Value);
    }

    [Fact]
    public async Task Hint_ReturnsServiceResult()
    {
        _game.Setup(s => s.GetHintAsync(7, It.IsAny<IEnumerable<int>>(), "user-1", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HintResult(12));

        var result = await Controller().GetHint(7, new BoardStateRequest { RemainingArrowIds = [12, 13] }, CancellationToken.None);

        var hint = Assert.IsType<HintResult>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(12, hint.ArrowId);
    }

    [Fact]
    public async Task StartAttempt_ReturnsNoContent()
    {
        var result = await Controller().StartAttempt(7, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        _game.Verify(s => s.StartAttemptAsync(7, "user-1", false, It.IsAny<CancellationToken>()), Times.Once);
    }
}
