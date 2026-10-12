using LoDb.Testing;

namespace LoDb.Api.Tests.Hosting;

/// <summary>
/// The shared test host leaves nothing behind: its storage root disappears with it.
/// </summary>
/// <remarks>
/// Its clock and its switched-off background services need no test of their own: every test
/// that sets a clock or counts on a quiet host fails without them.
/// </remarks>
public sealed class ApiFactoryTests
{
    [Fact]
    public async Task StorageRootIsRemovedWithTheFactory()
    {
        string root;
        await using (var factory = new ApiFactory())
        {
            root = factory.StorageRoot;
            Assert.True(Directory.Exists(root));
        }

        Assert.False(Directory.Exists(root));
    }
}
