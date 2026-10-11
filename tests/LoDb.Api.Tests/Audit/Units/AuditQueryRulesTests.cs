using LoDb.Api.Modules.Accounts.Http;
using LoDb.Api.Modules.Audit.Reading.Query;

namespace LoDb.Api.Tests.Audit.Units;

/// <summary>
/// The filters and the page of a journal query: every invalid one reported at once, a page
/// from 1 and up to 100 entries, 40 when the size is absent.
/// </summary>
public sealed class AuditQueryRulesTests
{
    private static readonly DateOnly Day = new(2026, 9, 20);

    [Fact]
    public void InvalidFiltersAreReportedTogether()
    {
        var errors = new FieldErrors();
        var request = Request() with
        {
            Action = ["user.dance"],
            Category = "misc",
            Outcome = "maybe",
            ActorType = "robot",
            From = Day,
            To = Day.AddDays(-19),
            Page = 0,
            PageSize = PageWindow.MaxPageSize + 1,
        };

        var query = AuditQueryRules.Parse(request, errors);

        Assert.Null(query);
        var codes = errors.ToProblem().Errors!;
        Assert.Equal([AuditQueryRules.UnknownAction], codes["action"]);
        Assert.Equal([AuditQueryRules.UnknownCategory], codes["category"]);
        Assert.Equal([AuditQueryRules.UnknownOutcome], codes["outcome"]);
        Assert.Equal([AuditQueryRules.UnknownActorType], codes["actorType"]);
        Assert.Equal([AuditQueryRules.InvalidRange], codes["to"]);
        Assert.Equal([AuditQueryRules.OutOfRange], codes["page"]);
        Assert.Equal([AuditQueryRules.OutOfRange], codes["pageSize"]);
    }

    [Theory]
    [InlineData(null, null, 1, PageWindow.DefaultPageSize)]
    [InlineData(1, 1, 1, 1)]
    [InlineData(3, PageWindow.MaxPageSize, 3, PageWindow.MaxPageSize)]
    public void PageWindowWithinItsBoundsIsKept(int? page, int? size, int expected, int length)
    {
        var errors = new FieldErrors();

        var query = AuditQueryRules.Parse(Request() with { Page = page, PageSize = size }, errors);

        Assert.Equal(new PageWindow(expected, length), query?.Window);
    }

    [Fact]
    public void OneDayRangeIsKept()
    {
        var errors = new FieldErrors();

        var query = AuditQueryRules.Parse(Request() with { From = Day, To = Day }, errors);

        Assert.True(errors.IsEmpty);
        Assert.Equal(TimeSpan.FromDays(1), query!.Filter.Until - query.Filter.From);
    }

    private static AuditQueryRequest Request() =>
        new(null, null, null, null, null, null, null, null, null, null);
}
