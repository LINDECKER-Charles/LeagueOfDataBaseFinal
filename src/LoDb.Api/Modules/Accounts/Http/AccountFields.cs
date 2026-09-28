namespace LoDb.Api.Modules.Accounts.Http;

/// <summary>
/// Names of the request fields as the JSON writes them, the keys of <c>errors</c> in a
/// validation problem.
/// </summary>
internal static class AccountFields
{
    public const string Email = "email";
    public const string Username = "username";
    public const string Password = "password";
    public const string AcceptTerms = "acceptTerms";
    public const string Identifier = "identifier";
    public const string Token = "token";
    public const string RefreshToken = "refreshToken";
    public const string Code = "code";
    public const string CodeVerifier = "codeVerifier";
    public const string RedirectUri = "redirectUri";
    public const string ClientId = "clientId";
}
