using Application.Abstractions.Authentication;
using Application.Operations.CleaningPlans;
using TestUtilities.Builders;
using Web.Api.IntegrationTests.Infrastructure;

namespace Web.Api.IntegrationTests.Operations;

public sealed class CleaningPlansEndpointTests : IClassFixture<ApiFactory>, IAsyncLifetime
{
  private readonly ApiFactory _factory;

  public CleaningPlansEndpointTests(ApiFactory factory) => _factory = factory;

  public Task InitializeAsync() => _factory.ResetAllAsync();
  public Task DisposeAsync() => Task.CompletedTask;

  private HttpClient Client(params string[] roles)
  {
    HttpClient client = _factory.CreateClient();
    if (roles.Length > 0)
    {
      client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, string.Join(",", roles));
    }
    return client;
  }

  private async Task<CleaningPlanDetailResponse> GetPlanByDateAsync(DateOnly date)
  {
    HttpResponseMessage response = await Client(Roles.CleaningStaff).GetAsync(
      new Uri($"cleaning-plans/{date:yyyy-MM-dd}", UriKind.Relative));
    response.StatusCode.ShouldBe(
      HttpStatusCode.OK,
      _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception");
    CleaningPlanDetailResponse? plan = await response.Content.ReadFromJsonAsync<CleaningPlanDetailResponse>();
    plan.ShouldNotBeNull();
    return plan;
  }

  private async Task<Guid> AddCleanInfoAsync(DateOnly date, Guid spotId)
  {
    HttpResponseMessage response = await Client(Roles.Receptionist).PostAsJsonAsync(
      new Uri($"cleaning-plans/{date:yyyy-MM-dd}/clean-infos", UriKind.Relative),
      new { SpotId = spotId });
    response.StatusCode.ShouldBe(
      HttpStatusCode.Created,
      _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception");
    return await response.Content.ReadFromJsonAsync<Guid>();
  }

  [Fact]
  public async Task MarkCleaned_StampsCompletedAtAndResponsibleUser()
  {
    var spotId = Guid.NewGuid();
    await _factory.WithDbAsync(async db =>
    {
      db.Spots.Add(new SpotBuilder().WithId(spotId).WithName("S-01").Build());
      await db.SaveChangesAsync();
    });

    DateOnly date = new(2026, 5, 10);
    Guid cleanInfoId = await AddCleanInfoAsync(date, spotId);

    var userId = Guid.NewGuid();
    HttpClient staffClient = _factory.CreateClient();
    staffClient.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.CleaningStaff);
    staffClient.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId.ToString());

    HttpResponseMessage markResponse = await staffClient.PostAsJsonAsync(
      new Uri($"clean-infos/{cleanInfoId}/mark-cleaned", UriKind.Relative),
      new { });
    markResponse.StatusCode.ShouldBe(
      HttpStatusCode.NoContent,
      _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception");

    await _factory.WithDbAsync(async db =>
    {
      Domain.Operations.CleanInfos.CleanInfo? ci = await db.CleanInfos.FindAsync(cleanInfoId);
      ci.ShouldNotBeNull();
      ci.CompletedAtUtc.ShouldNotBeNull();
      ci.ResponsibleUserId.ShouldBe(userId);
    });
  }

  [Fact]
  public async Task MarkCleaned_WhenAlreadyCompleted_Returns409()
  {
    var spotId = Guid.NewGuid();
    await _factory.WithDbAsync(async db =>
    {
      db.Spots.Add(new SpotBuilder().WithId(spotId).WithName("S-01").Build());
      await db.SaveChangesAsync();
    });

    DateOnly date = new(2026, 5, 11);
    Guid cleanInfoId = await AddCleanInfoAsync(date, spotId);

    HttpClient staffClient = Client(Roles.CleaningStaff);

    HttpResponseMessage first = await staffClient.PostAsJsonAsync(
      new Uri($"clean-infos/{cleanInfoId}/mark-cleaned", UriKind.Relative),
      new { });
    first.StatusCode.ShouldBe(
      HttpStatusCode.NoContent,
      _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception");

    HttpResponseMessage second = await staffClient.PostAsJsonAsync(
      new Uri($"clean-infos/{cleanInfoId}/mark-cleaned", UriKind.Relative),
      new { });
    second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
  }

  [Fact]
  public async Task Patch_SetNote_Persists()
  {
    var spotId = Guid.NewGuid();
    await _factory.WithDbAsync(async db =>
    {
      db.Spots.Add(new SpotBuilder().WithId(spotId).WithName("S-01").Build());
      await db.SaveChangesAsync();
    });

    DateOnly date = new(2026, 5, 12);
    Guid cleanInfoId = await AddCleanInfoAsync(date, spotId);

    HttpResponseMessage patchResponse = await Client(Roles.Receptionist).PatchAsJsonAsync(
      new Uri($"clean-infos/{cleanInfoId}", UriKind.Relative),
      new { Note = "broken faucet - flagged for maintenance" });
    patchResponse.StatusCode.ShouldBe(
      HttpStatusCode.NoContent,
      _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception");

    CleaningPlanDetailResponse refreshed = await GetPlanByDateAsync(date);
    refreshed.CleanInfos.Single().Note.ShouldBe("broken faucet - flagged for maintenance");
  }

  [Fact]
  public async Task GetByDate_WithNoExistingPlan_AutoCreatesEmptyPlan()
  {
    var spotId = Guid.NewGuid();
    await _factory.WithDbAsync(async db =>
    {
      db.Spots.Add(new SpotBuilder().WithId(spotId).WithName("S-01").Build());
      await db.SaveChangesAsync();
    });

    DateOnly date = new(2026, 6, 1);
    CleaningPlanDetailResponse plan = await GetPlanByDateAsync(date);

    plan.Date.ShouldBe(date);
    plan.UpdatedAtUtc.ShouldBeNull();
    plan.UpdatedByUserId.ShouldBeNull();
    plan.CleanInfos.ShouldBeEmpty();

    await _factory.WithDbAsync(async db =>
    {
      Domain.Operations.CleaningPlans.CleaningPlan? persisted =
        await db.CleaningPlans.FirstOrDefaultAsync(p => p.Date == date);
      persisted.ShouldNotBeNull();
      persisted.UpdatedAtUtc.ShouldBeNull();
      persisted.UpdatedByUserId.ShouldBeNull();
      int cleanInfoCount = await db.CleanInfos.CountAsync(ci => ci.CleaningPlanId == persisted.Id);
      cleanInfoCount.ShouldBe(0);
    });
  }

  [Fact]
  public async Task GetByDate_WhenCalledTwice_DoesNotDuplicate()
  {
    DateOnly date = new(2026, 6, 2);
    CleaningPlanDetailResponse first = await GetPlanByDateAsync(date);
    CleaningPlanDetailResponse second = await GetPlanByDateAsync(date);

    second.Id.ShouldBe(first.Id);

    await _factory.WithDbAsync(async db =>
    {
      int planCount = await db.CleaningPlans.CountAsync(p => p.Date == date);
      planCount.ShouldBe(1);
      int cleanInfoCount = await db.CleanInfos.CountAsync(ci => ci.CleaningPlanId == first.Id);
      cleanInfoCount.ShouldBe(0);
    });
  }

  [Fact]
  public async Task GetByDate_AsAnonymous_Returns401()
  {
    HttpResponseMessage response = await _factory.CreateClient().GetAsync(
      new Uri($"cleaning-plans/{new DateOnly(2026, 6, 3):yyyy-MM-dd}", UriKind.Relative));

    response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
  }

  [Fact]
  public async Task GetByDate_AsForbiddenRole_Returns403()
  {
    HttpResponseMessage response = await Client(Roles.Accountant).GetAsync(
      new Uri($"cleaning-plans/{new DateOnly(2026, 6, 4):yyyy-MM-dd}", UriKind.Relative));

    response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
  }

  [Fact]
  public async Task MarkCleaned_StampsUpdatedAtAndUpdatedByOnPlan()
  {
    var spotId = Guid.NewGuid();
    await _factory.WithDbAsync(async db =>
    {
      db.Spots.Add(new SpotBuilder().WithId(spotId).WithName("S-01").Build());
      await db.SaveChangesAsync();
    });

    DateOnly date = new(2026, 6, 5);
    Guid cleanInfoId = await AddCleanInfoAsync(date, spotId);

    DateTime? planUpdatedBeforeMark = null;
    await _factory.WithDbAsync(async db =>
    {
      planUpdatedBeforeMark = await db.CleaningPlans
        .Where(p => p.Date == date)
        .Select(p => p.UpdatedAtUtc)
        .FirstAsync();
    });
    planUpdatedBeforeMark.ShouldNotBeNull();

    var userId = Guid.NewGuid();
    HttpClient staffClient = _factory.CreateClient();
    staffClient.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.CleaningStaff);
    staffClient.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId.ToString());

    HttpResponseMessage markResponse = await staffClient.PostAsJsonAsync(
      new Uri($"clean-infos/{cleanInfoId}/mark-cleaned", UriKind.Relative),
      new { });
    markResponse.StatusCode.ShouldBe(
      HttpStatusCode.NoContent,
      _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception");

    await _factory.WithDbAsync(async db =>
    {
      Domain.Operations.CleaningPlans.CleaningPlan? persisted =
        await db.CleaningPlans.FirstOrDefaultAsync(p => p.Date == date);
      persisted.ShouldNotBeNull();
      persisted.UpdatedByUserId.ShouldBe(userId);
      persisted.UpdatedAtUtc.ShouldNotBeNull();
      persisted.UpdatedAtUtc!.Value.ShouldBeGreaterThanOrEqualTo(planUpdatedBeforeMark!.Value);
    });
  }

  [Fact]
  public async Task AddCleanInfo_WhenPlanMissing_AutoCreatesAndAddsSpot()
  {
    var spotId = Guid.NewGuid();
    await _factory.WithDbAsync(async db =>
    {
      db.Spots.Add(new SpotBuilder().WithId(spotId).WithName("S-01").Build());
      await db.SaveChangesAsync();
    });

    DateOnly date = new(2026, 6, 7);
    HttpResponseMessage response = await Client(Roles.Receptionist).PostAsJsonAsync(
      new Uri($"cleaning-plans/{date:yyyy-MM-dd}/clean-infos", UriKind.Relative),
      new { SpotId = spotId });

    response.StatusCode.ShouldBe(
      HttpStatusCode.Created,
      _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception");

    await _factory.WithDbAsync(async db =>
    {
      Domain.Operations.CleaningPlans.CleaningPlan persisted =
        await db.CleaningPlans.FirstAsync(p => p.Date == date);
      List<Guid> spotIds = await db.CleanInfos
        .Where(ci => ci.CleaningPlanId == persisted.Id)
        .Select(ci => ci.SpotId)
        .ToListAsync();
      spotIds.ShouldBe(new[] { spotId });
    });
  }

  [Fact]
  public async Task AddCleanInfo_WhenSpotAlreadyInPlan_Returns409()
  {
    var spotId = Guid.NewGuid();
    await _factory.WithDbAsync(async db =>
    {
      db.Spots.Add(new SpotBuilder().WithId(spotId).WithName("S-01").Build());
      await db.SaveChangesAsync();
    });

    DateOnly date = new(2026, 6, 8);
    await AddCleanInfoAsync(date, spotId);

    HttpResponseMessage response = await Client(Roles.Receptionist).PostAsJsonAsync(
      new Uri($"cleaning-plans/{date:yyyy-MM-dd}/clean-infos", UriKind.Relative),
      new { SpotId = spotId });

    response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
  }
}
