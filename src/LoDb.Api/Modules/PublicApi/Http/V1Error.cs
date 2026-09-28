namespace LoDb.Api.Modules.PublicApi.Http;

/// <summary>
/// A JSON refusal of <c>/v1</c>: its status and its envelope always travel together, and
/// the instance is shared by every request that gets it.
/// </summary>
internal sealed class V1Error : IResult
{
    private readonly V1JsonResult<ErrorEnvelope> _result;

    public V1Error(int statusCode, string code, string message)
    {
        StatusCode = statusCode;
        Code = code;
        Message = message;
        _result = new(statusCode, new ErrorEnvelope(new ErrorBody(code, message)));
    }

    public int StatusCode { get; }

    public string Code { get; }

    public string Message { get; }

    public Task ExecuteAsync(HttpContext httpContext) => _result.ExecuteAsync(httpContext);
}
