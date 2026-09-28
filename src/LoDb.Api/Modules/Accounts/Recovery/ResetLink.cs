using LoDb.Infrastructure.Persistence.Accounts;

namespace LoDb.Api.Modules.Accounts.Recovery;

/// <summary>A reset link found valid: the account it names, and its decoded token.</summary>
internal sealed record ResetLink(User Account, string Token);
