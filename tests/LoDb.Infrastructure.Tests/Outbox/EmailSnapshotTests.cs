using System.Runtime.CompilerServices;
using System.Text.Json;
using LoDb.Domain.Languages;
using LoDb.Infrastructure.Outbox;
using LoDb.Infrastructure.Outbox.Rendering;

namespace LoDb.Infrastructure.Tests.Outbox;

/// <summary>
/// Every template, HTML and text, in English and French, compared with the reviewed copy in
/// <c>Snapshots/&lt;locale&gt;/</c>.
/// </summary>
/// <remarks>
/// After a deliberate change, <c>LODB_UPDATE_SNAPSHOTS=1 dotnet test</c> rewrites the copies
/// in the source tree; review their diff before committing it.
/// </remarks>
public sealed class EmailSnapshotTests
{
    private const string UpdateVariable = "LODB_UPDATE_SNAPSHOTS";

    public static TheoryData<EmailTemplate, UiLocale> Cases
    {
        get
        {
            var cases = new TheoryData<EmailTemplate, UiLocale>();
            foreach (var template in Enum.GetValues<EmailTemplate>())
            {
                cases.Add(template, UiLocale.En);
                cases.Add(template, UiLocale.Fr);
            }

            return cases;
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void RenderedEmailMatchesItsSnapshot(EmailTemplate template, UiLocale locale)
    {
        var model = template == EmailTemplate.ContactNotification
            ? OutboxHarness.ContactModel()
            : OutboxHarness.AccountModel();
        var email = EmailRenderer.Render(template, locale, JsonSerializer.Serialize(model));
        var name = Path.Combine(UiLocales.Code(locale), Name(template));

        Verify($"{name}.html", $"Subject: {email.Subject}\n\n{email.Html}");
        Verify($"{name}.txt", $"Subject: {email.Subject}\n\n{email.Text}");
    }

    private static string Name(EmailTemplate template) => template switch
    {
        EmailTemplate.ConfirmEmail => "confirm_email",
        EmailTemplate.ResetPassword => "reset_password",
        EmailTemplate.ContactNotification => "contact_notification",
        _ => throw new ArgumentOutOfRangeException(nameof(template), template, null),
    };

    private static void Verify(string name, string actual)
    {
        if (Environment.GetEnvironmentVariable(UpdateVariable) == "1")
        {
            var source = Path.Combine(SourceDirectory(), name);
            Directory.CreateDirectory(Path.GetDirectoryName(source)!);
            File.WriteAllText(source, actual);
            return;
        }

        var expected = Path.Combine(AppContext.BaseDirectory, "Outbox", "Snapshots", name);
        Assert.True(File.Exists(expected), $"No snapshot {name}: run with {UpdateVariable}=1.");
        Assert.Equal(File.ReadAllText(expected), actual);
    }

    private static string SourceDirectory([CallerFilePath] string path = "") =>
        Path.Combine(Path.GetDirectoryName(path)!, "Snapshots");
}
