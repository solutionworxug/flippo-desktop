using System.Diagnostics.CodeAnalysis;
using Flippo.Cloud.Security;
using Xunit;

namespace Flippo.Tests.Cloud;

public class WindowsDpapiTokenVaultTests
{
    [SuppressMessage("CA1416", "CA1416", Justification = "Platform guard in each test")]
    private static WindowsDpapiTokenVault NewVault(out string dir)
    {
        dir = Directory.CreateTempSubdirectory().FullName;
        return new WindowsDpapiTokenVault(dir);
    }

    [Fact]
    public void Store_Then_Retrieve_RoundTrips()
    {
        if (!OperatingSystem.IsWindows()) return;
        var vault = NewVault(out var dir);
        try
        {
            var key = Guid.NewGuid().ToString();
            vault.Store(key, "refresh-token-secret-äöü");
            Assert.Equal("refresh-token-secret-äöü", vault.Retrieve(key));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Retrieve_UnknownKey_ReturnsNull()
    {
        if (!OperatingSystem.IsWindows()) return;
        var vault = NewVault(out var dir);
        try { Assert.Null(vault.Retrieve("does-not-exist")); }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Delete_RemovesSecret_RetrieveThenNull()
    {
        if (!OperatingSystem.IsWindows()) return;
        var vault = NewVault(out var dir);
        try
        {
            var key = Guid.NewGuid().ToString();
            vault.Store(key, "x");
            vault.Delete(key);
            Assert.Null(vault.Retrieve(key));
            vault.Delete(key);   // idempotent — kein Wurf
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Store_Twice_Overwrites()
    {
        if (!OperatingSystem.IsWindows()) return;
        var vault = NewVault(out var dir);
        try
        {
            var key = Guid.NewGuid().ToString();
            vault.Store(key, "first");
            vault.Store(key, "second");
            Assert.Equal("second", vault.Retrieve(key));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void StoredBlob_IsNotPlaintext()
    {
        if (!OperatingSystem.IsWindows()) return;
        var vault = NewVault(out var dir);
        try
        {
            var key = Guid.NewGuid().ToString();
            vault.Store(key, "SUPER-SECRET-MARKER");
            var raw = File.ReadAllText(Path.Combine(dir, key + ".bin"));
            Assert.DoesNotContain("SUPER-SECRET-MARKER", raw);
        }
        finally { Directory.Delete(dir, true); }
    }
}
