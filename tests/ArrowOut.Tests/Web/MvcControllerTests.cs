using ArrowOut.Game;
using ArrowOut.Game.Generation;
using ArrowOut.Services.Challenges;
using ArrowOut.Services.Exceptions;
using ArrowOut.Services.Leaderboard;
using ArrowOut.Services.Levels;
using ArrowOut.Services.Models;
using ArrowOut.Web.Areas.Administration.Models;
using ArrowOut.Web.Controllers;
using ArrowOut.Web.Infrastructure;
using ArrowOut.Web.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using AdminLevelsController = ArrowOut.Web.Areas.Administration.Controllers.LevelsController;

namespace ArrowOut.Tests.Web;

public class MvcControllerTests
{
    private static readonly LevelDesignReport OkReport = new([], true, 3, 3, Difficulty.Easy);

    [Fact]
    public async Task ChallengePlay_RendersViewWithChallengeInfo()
    {
        var challenges = new Mock<IChallengeService>();
        var info = new ChallengeInfo(7, ChallengeKind.Hard, 50, 50, 512, 3, false, 0);
        challenges.Setup(s => s.GetInfoAsync(7, "u", It.IsAny<CancellationToken>())).ReturnsAsync(info);

        var result = await new ChallengeController(challenges.Object).WithUser("u").Play(7, CancellationToken.None);

        var model = Assert.IsType<ChallengeViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Same(info, model.Challenge);
    }

    [Fact]
    public async Task ChallengeNew_CreatesForTheSignedInUserAndRedirectsToIt()
    {
        var challenges = new Mock<IChallengeService>();
        challenges.Setup(s => s.CreateAsync("u", ChallengeKind.Easy, It.IsAny<CancellationToken>())).ReturnsAsync(42);

        var result = await new ChallengeController(challenges.Object).WithUser("u").New(ChallengeKind.Easy, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(ChallengeController.Play), redirect.ActionName);
        Assert.Equal(42, redirect.RouteValues!["id"]);
    }

    [Theory]
    [InlineData(ChallengeKind.Hard, ChallengeKind.Hard)]
    [InlineData((ChallengeKind)42, ChallengeKind.Easy)] // tampered ?kind= falls back to the first tab
    public async Task Leaderboard_ShowsTheRankingOfTheSelectedKind(ChallengeKind requested, ChallengeKind shown)
    {
        var leaderboard = new Mock<ILeaderboardService>();
        leaderboard.Setup(s => s.GetPageAsync(shown, 1, LeaderboardController.PageSize, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PagedResult<LeaderboardEntry>.Empty());

        var controller = new LeaderboardController(leaderboard.Object, Mock.Of<IChallengeService>())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }, // a visitor
        };

        var result = await controller.Index(requested, 1, from: 7, CancellationToken.None);

        var model = Assert.IsType<LeaderboardViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal(shown, model.Kind);
        Assert.Equal(shown.ToString(), model.Pagination.RouteValues["kind"]);
        Assert.Null(model.FromChallengeId); // visitors have no game to go back to
        leaderboard.Verify(s => s.GetPageAsync(shown, 1, LeaderboardController.PageSize, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(12, 30, 12, 30)]     // came from #12, another unfinished board #30
    [InlineData(12, 12, 12, null)]   // the unfinished board is the one they came from: shown once
    [InlineData(null, 30, null, 30)] // opened the leaderboard from the menu
    public async Task Leaderboard_OffersTheWayBackIntoTheGame(int? from, int? unfinished, int? expectedFrom, int? expectedUnfinished)
    {
        var leaderboard = new Mock<ILeaderboardService>();
        leaderboard.Setup(s => s.GetPageAsync(It.IsAny<ChallengeKind>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PagedResult<LeaderboardEntry>.Empty());
        var challenges = new Mock<IChallengeService>();
        challenges.Setup(s => s.GetSummaryAsync("u", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChallengeSummary(3, 1, 3, 4, unfinished));

        var result = await new LeaderboardController(leaderboard.Object, challenges.Object).WithUser("u")
            .Index(ChallengeKind.Medium, 1, from, CancellationToken.None);

        var model = Assert.IsType<LeaderboardViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal((expectedFrom, expectedUnfinished), (model.FromChallengeId, model.UnfinishedChallengeId));
    }

    [Fact]
    public async Task ChallengeNew_RejectsTamperedKind()
    {
        var challenges = new Mock<IChallengeService>();

        var result = await new ChallengeController(challenges.Object).WithUser("u").New((ChallengeKind)42, CancellationToken.None);
        Assert.IsType<BadRequestResult>(result);

        // Text that isn't a kind ("kind=Impossible") keeps the default value but makes ModelState invalid.
        var controller = new ChallengeController(challenges.Object).WithUser("u");
        controller.ModelState.AddModelError("kind", "The value 'Impossible' is not valid.");
        Assert.IsType<BadRequestResult>(await controller.New(ChallengeKind.Hard, CancellationToken.None));

        challenges.Verify(s => s.CreateAsync(It.IsAny<string>(), It.IsAny<ChallengeKind>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AdminCreate_InvalidModelState_RedisplaysForm()
    {
        var admin = new Mock<ILevelAdminService>();
        admin.Setup(s => s.Analyze(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>())).Returns(OkReport);
        var controller = new AdminLevelsController(admin.Object).WithUser("admin", isAdmin: true);
        controller.ModelState.AddModelError("Input.Name", "Required");

        var result = await controller.Create(new LevelInputModel(), CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("Form", view.ViewName);
        admin.Verify(s => s.CreateAsync(It.IsAny<LevelInputModel>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AdminCreate_UnsolvableDesign_AddsModelErrors()
    {
        var admin = new Mock<ILevelAdminService>();
        admin.Setup(s => s.Analyze(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>())).Returns(OkReport);
        admin.Setup(s => s.CreateAsync(It.IsAny<LevelInputModel>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidLevelDesignException(["jammed"]));
        var controller = new AdminLevelsController(admin.Object).WithUser("admin", isAdmin: true);

        var result = await controller.Create(new LevelInputModel(), CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.Contains("jammed", controller.ModelState["Input.ArrowsJson"]!.Errors.Select(e => e.ErrorMessage));
    }

    [Fact]
    public async Task AdminCreate_DuplicateNumber_FlagsNumberField()
    {
        var admin = new Mock<ILevelAdminService>();
        admin.Setup(s => s.Analyze(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>())).Returns(OkReport);
        admin.Setup(s => s.CreateAsync(It.IsAny<LevelInputModel>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DuplicateLevelNumberException(4));
        var controller = new AdminLevelsController(admin.Object).WithUser("admin", isAdmin: true);

        await controller.Create(new LevelInputModel { Number = 4 }, CancellationToken.None);

        Assert.True(controller.ModelState.ContainsKey("Input.Number"));
    }

    [Fact]
    public async Task AdminCreate_Success_RedirectsToEdit()
    {
        var admin = new Mock<ILevelAdminService>();
        admin.Setup(s => s.CreateAsync(It.IsAny<LevelInputModel>(), It.IsAny<CancellationToken>())).ReturnsAsync(42);
        var controller = new AdminLevelsController(admin.Object).WithUser("admin", isAdmin: true);

        var result = await controller.Create(new LevelInputModel { Number = 9 }, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Edit", redirect.ActionName);
        Assert.Equal(42, redirect.RouteValues!["id"]);
        Assert.NotNull(controller.TempData[StatusMessages.SuccessKey]);
    }

    [Fact]
    public async Task AdminEdit_RouteIdWinsOverPostedId()
    {
        var admin = new Mock<ILevelAdminService>();
        var controller = new AdminLevelsController(admin.Object).WithUser("admin", isAdmin: true);
        var input = new LevelInputModel { Id = 999, Number = 1 };

        await controller.Edit(5, input, CancellationToken.None);

        admin.Verify(s => s.UpdateAsync(5, It.Is<LevelInputModel>(m => m.Id == 5), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AdminIsNumberAvailable_ReturnsRemoteValidationPayload()
    {
        var admin = new Mock<ILevelAdminService>();
        admin.Setup(s => s.IsNumberAvailableAsync(3, null, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var controller = new AdminLevelsController(admin.Object).WithUser("admin", isAdmin: true);

        var result = await controller.IsNumberAvailable(3, null, CancellationToken.None);

        Assert.IsType<string>(Assert.IsType<JsonResult>(result).Value);
    }

    [Fact]
    public void Error404_SetsStatusAndView()
    {
        var controller = new ErrorController { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };

        var result = controller.PageNotFound();

        Assert.Equal(StatusCodes.Status404NotFound, controller.Response.StatusCode);
        var model = Assert.IsType<ErrorViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal(404, model.StatusCode);
    }

    [Fact]
    public void Error500_SetsStatusAndView()
    {
        var controller = new ErrorController { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };

        var result = controller.ServerError();

        Assert.Equal(StatusCodes.Status500InternalServerError, controller.Response.StatusCode);
        Assert.Equal("ServerError", Assert.IsType<ViewResult>(result).ViewName);
    }

    [Fact]
    public void Pagination_PreservesFiltersAndWindowsPages()
    {
        var page = new PagedResult<int>([], 5, 10, 200);
        var pagination = new PaginationViewModel(page, "Index", new Dictionary<string, string?> { ["search"] = "knot" });

        Assert.Equal([3, 4, 5, 6, 7], pagination.VisiblePages());
        var route = pagination.RouteFor(6);
        Assert.Equal("knot", route["search"]);
        Assert.Equal("6", route["page"]);
    }

    [Fact]
    public void LevelFormViewModel_IsEditOnlyWithId()
    {
        Assert.False(new LevelFormViewModel { Input = new LevelInputModel() }.IsEdit);
        Assert.True(new LevelFormViewModel { Id = 1, Input = new LevelInputModel() }.IsEdit);
    }
}
