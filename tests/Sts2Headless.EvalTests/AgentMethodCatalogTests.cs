using Sts2Headless.Eval.Protocol;
using Xunit;

namespace Sts2Headless.EvalTests;

public sealed class AgentMethodCatalogTests
{
    [Fact]
    public void Catalog_Contains_The_Three_Required_Methods()
    {
        var names = AgentMethodCatalog.All.Select(e => e.Name).ToHashSet();
        Assert.Contains("agent/init",     names);
        Assert.Contains("agent/decide",   names);
        Assert.Contains("agent/teardown", names);
    }

    [Fact]
    public void Init_And_Decide_Have_Params_Types()
    {
        var init = AgentMethodCatalog.All.Single(e => e.Name == "agent/init");
        var decide = AgentMethodCatalog.All.Single(e => e.Name == "agent/decide");
        Assert.NotNull(init.ParamsType);
        Assert.NotNull(decide.ParamsType);
    }

    [Fact]
    public void Teardown_Has_No_Params()
    {
        var teardown = AgentMethodCatalog.All.Single(e => e.Name == "agent/teardown");
        Assert.Null(teardown.ParamsType);
    }

    [Fact]
    public void Error_Codes_Sit_In_The_Agent_Range()
    {
        Assert.True(AgentErrorCode.AgentDeclinedToInit  is >= -32299 and <= -32200);
        Assert.True(AgentErrorCode.AgentDecisionRefused is >= -32299 and <= -32200);
        Assert.True(AgentErrorCode.AgentSnapshotInvalid is >= -32299 and <= -32200);
    }
}
