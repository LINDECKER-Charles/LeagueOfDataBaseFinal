namespace LoDb.Api.Modules.Accounts.Security.Limits;

/// <summary>
/// An hourly bound on one costly action of a signed-in account, which the module owning the
/// action declares (<see cref="AccountActionLimits.AddAccountActionLimit"/>).
/// </summary>
/// <param name="Policy">The rate limiting policy the action's endpoints require.</param>
/// <param name="Setting">
/// Its key under <c>LoDb:RateLimiting</c>, such as <c>CheckoutsPerHour</c>.
/// </param>
/// <param name="DefaultPerHour">Actions per sliding hour when the setting is absent.</param>
internal sealed record AccountActionLimit(string Policy, string Setting, int DefaultPerHour);
