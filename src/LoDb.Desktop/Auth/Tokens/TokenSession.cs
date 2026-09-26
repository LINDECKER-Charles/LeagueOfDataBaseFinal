using LoDb.Desktop.Auth.Api;

namespace LoDb.Desktop.Auth.Tokens;

/// <summary>
/// The app's tokens, held by the host and never handed to the page (ADR 0009): the access
/// token in memory, the refresh token in memory and, for "remember me", in the vault.
/// </summary>
/// <remarks>
/// One gate serialises every change and every renewal, so that concurrent proxied
/// requests share a single refresh call and a sign-out never races one.
/// </remarks>
internal sealed partial class TokenSession(
    AccountApiClient api,
    RefreshTokenVault vault,
    TimeProvider time,
    ILogger<TokenSession> logger) : IDisposable
{
    // Renewing a little ahead keeps a token from expiring between the proxy and the API.
    private static readonly TimeSpan ExpiryMargin = TimeSpan.FromSeconds(30);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private volatile SessionTokens? _tokens;
    private bool _isRemembered;
    private bool _isVaultRead;

    public async Task<SessionState> ReadStateAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            ReadVaultOnce();
            var isSignedIn = _tokens is not null;
            return new SessionState
            {
                IsSignedIn = isSignedIn,
                IsRemembered = isSignedIn && _isRemembered,
            };
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// A valid access token, renewed when due; null when signed out, or when the API could
    /// not renew it (the request then goes out anonymous and the API answers it).
    /// </summary>
    public async Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (_tokens is { } current && IsFresh(current))
        {
            return current.AccessToken;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            ReadVaultOnce();
            return _tokens switch
            {
                null => null,
                { } tokens when IsFresh(tokens) => tokens.AccessToken,
                { } tokens => await RenewAsync(tokens.RefreshToken, cancellationToken),
            };
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Holds the tokens of a sign-in, replacing any previous session.</summary>
    public async Task AdoptAsync(
        TokenResponse grant,
        bool isRemembered,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            // A new sign-in supersedes whatever the vault held.
            _isVaultRead = true;
            _isRemembered = isRemembered;
            Hold(grant);
            if (!isRemembered)
            {
                vault.Delete();
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SignOutAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            _isVaultRead = true;
            Forget();
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose() => _gate.Dispose();

    private async Task<string?> RenewAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var call = await api.RefreshAsync(refreshToken, cancellationToken);
        if (call.Grant is { } grant)
        {
            Hold(grant);
            return grant.AccessToken;
        }

        if (call.Problem is { } problem && IsFinalRefusal(problem.Status))
        {
            LogRefreshRefused(logger, problem.Status);
            Forget();
        }

        return null;
    }

    // Throttling and server errors pass: the refresh token is kept for the next request.
    private static bool IsFinalRefusal(int status) =>
        status is >= StatusCodes.Status400BadRequest and < StatusCodes.Status500InternalServerError
            and not StatusCodes.Status429TooManyRequests;

    private void Hold(TokenResponse grant)
    {
        _tokens = new SessionTokens
        {
            AccessToken = grant.AccessToken,
            AccessExpiresAt = time.GetUtcNow().AddSeconds(grant.ExpiresIn),
            RefreshToken = grant.RefreshToken,
        };
        if (_isRemembered)
        {
            vault.Save(grant.RefreshToken);
        }
    }

    private void Forget()
    {
        _tokens = null;
        _isRemembered = false;
        vault.Delete();
    }

    // Lazily, so that the smoke check and a signed-out start never touch the keys.
    private void ReadVaultOnce()
    {
        if (_isVaultRead)
        {
            return;
        }

        _isVaultRead = true;
        if (vault.Load() is { } refreshToken)
        {
            _tokens = new SessionTokens
            {
                AccessToken = null,
                AccessExpiresAt = DateTimeOffset.MinValue,
                RefreshToken = refreshToken,
            };
            _isRemembered = true;
        }
    }

    private bool IsFresh(SessionTokens tokens) =>
        tokens.AccessToken is not null && time.GetUtcNow() + ExpiryMargin < tokens.AccessExpiresAt;

    [LoggerMessage(
        EventName = "desktop.refresh.refused",
        Level = LogLevel.Information,
        Message = "The API refused the refresh token ({Status}); the session ends.")]
    private static partial void LogRefreshRefused(ILogger logger, int status);
}
