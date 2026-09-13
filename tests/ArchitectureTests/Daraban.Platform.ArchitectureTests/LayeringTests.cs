using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using NetArchTest.Rules;
using Xunit;

namespace Daraban.Platform.ArchitectureTests;

/// <summary>
/// Layering rules for the Daraban modular monolith (Task 8.1).
///
/// Module boundary contract:
///   Api      -> own Services + own Data (lookup queries) + Shared
///   Services -> own Data + Shared (+ other modules' Data for read-only aggregation: Dashboard, Reporting)
///   Data     -> Shared only
///
/// The ArchitectureTests project references every module project, so all
/// assemblies are loaded transitively and available at runtime.
/// </summary>
public class LayeringTests
{
    private const string ModulesRoot = "Daraban.Modules.";

    private static readonly string[] ModuleNames =
    [
        "Assets", "Automation", "Dashboard", "Discovery", "Financial", "Identity",
        "Inventory", "Knowledge", "Notifications", "Plugins", "Reporting",
        "ServiceDesk", "Settings", "Software"
    ];

    private static Assembly ApiAssembly(string module) => Assembly.Load($"{ModulesRoot}{module}.Api");
    private static Assembly ServicesAssembly(string module) => Assembly.Load($"{ModulesRoot}{module}.Services");
    private static Assembly DataAssembly(string module) => Assembly.Load($"{ModulesRoot}{module}.Data");

    private static void AssertPass(TestResult result, string ruleName)
    {
        Assert.True(result.IsSuccessful,
            $"{ruleName} failed. Violations:\n{string.Join("\n", result.FailingTypeNames)}");
    }

    // ---------------------------------------------------------------------
    // Rule 1: Api projects must not reference other modules' Data projects.
    //         (Own Data is allowed for lookup queries.)
    // ---------------------------------------------------------------------
    [Fact]
    public void ModuleApiProjects_ShouldNotDependOnOtherModulesDataProjects()
    {
        var violations = new List<string>();

        foreach (var module in ModuleNames)
        {
            var others = ModuleNames.Where(m => m != module).ToList();

            var result = Types.InAssembly(ApiAssembly(module))
                .Should()
                .NotHaveDependencyOnAny(others.Select(m => $"{ModulesRoot}{m}.Data").ToArray())
                .GetResult();

            if (!result.IsSuccessful)
                violations.AddRange(result.FailingTypeNames.Select(t => $"Api({module}) -> {t}"));
        }

        Assert.True(violations.Count == 0,
            "Api projects must not depend on other modules' Data projects.\nViolations:\n" +
            string.Join("\n", violations));
    }

    // ---------------------------------------------------------------------
    // Rule 2: Services must not reference other modules' Services.
    // ---------------------------------------------------------------------
    [Fact]
    public void ModuleServicesProjects_ShouldNotDependOnOtherModulesServicesProjects()
    {
        var violations = new List<string>();

        foreach (var module in ModuleNames)
        {
            var others = ModuleNames.Where(m => m != module).ToList();

            var result = Types.InAssembly(ServicesAssembly(module))
                .Should()
                .NotHaveDependencyOnAny(others.Select(m => $"{ModulesRoot}{m}.Services").ToArray())
                .GetResult();

            if (!result.IsSuccessful)
                violations.AddRange(result.FailingTypeNames.Select(t => $"Services({module}) -> {t}"));
        }

        Assert.True(violations.Count == 0,
            "Services projects must not depend on other modules' Services projects.\nViolations:\n" +
            string.Join("\n", violations));
    }

    // ---------------------------------------------------------------------
    // Rule 3: No circular references between modules.
    //         For every ordered pair (A, B): if A's Data depends on B's Data,
    //         then B's Data must not depend back on A's Data. Cross-module
    //         Data references are allowed one-directionally (Dashboard and
    //         Reporting aggregate other modules' Data read-only).
    // ---------------------------------------------------------------------
    [Fact]
    public void Modules_ShouldNotHaveCircularReferences()
    {
        var violations = new List<string>();

        foreach (var a in ModuleNames)
        {
            foreach (var b in ModuleNames.Where(m => m != a))
            {
                // Does A's Data depend on B's Data?
                var aDependsOnB = Types.InAssembly(DataAssembly(a))
                    .That()
                    .HaveDependencyOn($"{ModulesRoot}{b}.Data")
                    .GetTypes()
                    .Any();

                if (!aDependsOnB)
                    continue;

                // Does B's Data depend back on A's Data?
                var bDependsOnA = Types.InAssembly(DataAssembly(b))
                    .That()
                    .HaveDependencyOn($"{ModulesRoot}{a}.Data")
                    .GetTypes()
                    .Any();

                if (bDependsOnA)
                    violations.Add($"Data({a}) <-> Data({b})");
            }
        }

        Assert.True(violations.Count == 0,
            "Modules must not have circular Data references.\nViolations:\n" +
            string.Join("\n", violations));
    }

    // ---------------------------------------------------------------------
    // Rule 4: Controller actions must have [RequirePermission] attribute.
    //         Every public action method carrying an Http* attribute in a
    //         module Api assembly must also carry [RequirePermission] or
    //         [AllowAnonymous]. Identity.AuthController is exempt (anonymous
    //         register/login/refresh/logout by design).
    // ---------------------------------------------------------------------
    [Fact]
    public void ControllerActions_MustHaveRequirePermissionAttribute()
    {
        var exempt = new[]
        {
            "Daraban.Modules.Identity.Api.Controllers.AuthController",
        };

        var violations = new List<string>();

        foreach (var module in ModuleNames)
        {
            var asm = ApiAssembly(module);
            var controllerTypes = asm.GetTypes()
                .Where(t => t is { IsClass: true, IsAbstract: false }
                    && t.Name.EndsWith("Controller", StringComparison.Ordinal));

            foreach (var controller in controllerTypes)
            {
                if (exempt.Contains(controller.FullName))
                    continue;

                var actions = controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .Where(m => m.GetCustomAttributes(inherit: false)
                        .Any(a => a.GetType().Name.StartsWith("Http", StringComparison.Ordinal)));

                foreach (var action in actions)
                {
                    var hasRequirePermission = action.GetCustomAttributes(inherit: false)
                        .Any(a => a.GetType().Name == "RequirePermissionAttribute");
                    var hasAllowAnonymous = action.GetCustomAttributes(inherit: false)
                        .OfType<AllowAnonymousAttribute>()
                        .Any();

                    if (!hasRequirePermission && !hasAllowAnonymous)
                        violations.Add($"{controller.FullName}.{action.Name}");
                }
            }
        }

        Assert.True(violations.Count == 0,
            "Controller actions must carry [RequirePermission] (or [AllowAnonymous]).\nViolations:\n" +
            string.Join("\n", violations));
    }

    // ---------------------------------------------------------------------
    // Rule 5: Repository interfaces must live in the Data layer.
    //         No I*Repository interface may be declared in a Services or Api
    //         assembly — repository contracts belong to the module's Data
    //         layer next to their EF implementations.
    // ---------------------------------------------------------------------
    [Fact]
    public void RepositoryInterfaces_MustLiveInDataLayer()
    {
        var violations = new List<string>();

        foreach (var module in ModuleNames)
        {
            foreach (var (layer, assembly) in new[]
                     {
                         ("Services", ServicesAssembly(module)),
                         ("Api", ApiAssembly(module)),
                     })
            {
                var result = Types.InAssembly(assembly)
                    .That()
                    .AreInterfaces()
                    .And().HaveNameStartingWith("I")
                    .And().HaveNameEndingWith("Repository")
                    .Should()
                    .NotResideInNamespace($"{ModulesRoot}{module}.{layer}")
                    .GetResult();

                if (!result.IsSuccessful)
                    violations.AddRange(result.FailingTypeNames.Select(t => $"{layer}({module}): {t}"));
            }
        }

        Assert.True(violations.Count == 0,
            "Repository interfaces must live in the Data layer.\nViolations:\n" +
            string.Join("\n", violations));
    }
}
