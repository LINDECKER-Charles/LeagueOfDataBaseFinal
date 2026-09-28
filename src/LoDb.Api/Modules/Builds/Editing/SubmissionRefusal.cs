namespace LoDb.Api.Modules.Builds.Editing;

/// <summary>
/// A refused submission: the validation problem of its fields, or the catalog's problem
/// when the patch could not be read, which refuses the write rather than saving a build no
/// rule checked.
/// </summary>
internal sealed record SubmissionRefusal(IResult Problem) : IResult
{
    public Task ExecuteAsync(HttpContext httpContext) => Problem.ExecuteAsync(httpContext);
}
