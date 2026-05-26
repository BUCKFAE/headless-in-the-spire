using Sts2Headless.Eval;
using Xunit;

namespace Sts2Headless.EvalTests;

public sealed class SeedBankTests
{
    [Fact]
    public void Committed_Smoke_Bank_Loads_From_Disk()
    {
        var bank = SeedBanks.Smoke;
        Assert.Equal("smoke", bank.Name);
        Assert.NotEmpty(bank.Seeds);
        Assert.Contains((ulong)42, bank.Seeds);
    }

    [Fact]
    public void Committed_Reference_Bank_Has_Fifty_Seeds()
    {
        var bank = SeedBanks.Reference;
        Assert.Equal("reference", bank.Name);
        Assert.Equal(50, bank.Seeds.Count);
    }

    [Fact]
    public void Inline_Banks_Carry_Inline_Version_Marker()
    {
        var bank = SeedBanks.Inline([1, 2, 3], name: "test-bank");
        Assert.Equal("test-bank", bank.Name);
        Assert.Equal("inline", bank.Version);
        Assert.Equal(3, bank.Seeds.Count);
    }
}
