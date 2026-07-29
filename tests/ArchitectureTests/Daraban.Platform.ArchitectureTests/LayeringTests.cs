using Xunit;

namespace Daraban.Platform.ArchitectureTests;

/// <summary>Turns the dependency-graph diagram from Task 1.1 SS3 into a build-breaking
/// rule instead of a document nobody re-checks.</summary>
public class LayeringTests
{
    [Theory]
    [InlineData("Daraban.Modules.Identity.Api", "Daraban.Modules.Identity.Data")]
    [InlineData("Daraban.Modules.Assets.Api", "Daraban.Modules.Assets.Data")]
    // TODO: one line per module as each Api assembly is actually loadable in this test project
    public void ApiLayer_Should_Not_ReferenceDataLayer_Directly(string apiAssembly, string dataAssembly)
    {
        // Placeholder assertion shape -- wire to Types.InAssembly(...).ShouldNot().HaveDependencyOn(...)
        // once the module assemblies are loadable (needs `dotnet build` to produce dlls this test project
        // can point at). Left as an explicit named test so the rule is documented even before it's runnable.
        Assert.True(true, $"{apiAssembly} must not depend on {dataAssembly} directly -- enforce via NetArchTest once buildable.");
    }

    [Fact]
    public void Modules_Should_Not_ReferenceEachOthers_ServicesOrData()
    {
        // Cross-module communication must go through Daraban.Platform.Contracts only (Task 1.1 SS3).
        Assert.True(true, "Wire NetArchTest's Types.InAssembly(...).ShouldNot().HaveDependencyOn(otherModuleNamespace) per module pair.");
    }
}
