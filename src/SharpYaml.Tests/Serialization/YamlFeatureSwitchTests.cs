#nullable enable

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SharpYaml.Tests.Serialization;

[TestClass]
public sealed class YamlFeatureSwitchTests
{
    [TestMethod]
    public void Assembly_EmbedsIlLinkSubstitutions()
    {
        var resources = typeof(SharpYaml.YamlSerializer).Assembly.GetManifestResourceNames();
        Assert.IsTrue(resources.Contains("ILLink.Substitutions.xml", StringComparer.Ordinal), "Expected embedded resource 'ILLink.Substitutions.xml'.");
    }

    [TestMethod]
    public void Project_PackagesBuildTransitiveTargets()
    {
        var root = FindRepoRoot();
        var csprojPath = Path.Combine(root, "src", "SharpYaml", "SharpYaml.csproj");
        var targetsPath = Path.Combine(root, "src", "SharpYaml", "SharpYaml.targets");

        Assert.IsTrue(File.Exists(csprojPath), $"Missing {csprojPath}.");
        Assert.IsTrue(File.Exists(targetsPath), $"Missing {targetsPath}.");

        var csprojText = File.ReadAllText(csprojPath);
        StringAssert.Contains(csprojText, "PackagePath=\"buildTransitive/SharpYaml.targets\"");
        StringAssert.Contains(csprojText, "EmbeddedResource Include=\"ILLink.Substitutions.xml\"");

        var targetsText = File.ReadAllText(targetsPath);
        StringAssert.Contains(targetsText, "RuntimeHostConfigurationOption Include=\"SharpYaml.YamlSerializer.IsReflectionEnabledByDefault\"");
        StringAssert.Contains(targetsText, "<SharpYamlIsReflectionEnabledByDefault");
        StringAssert.Contains(targetsText, "Trim=\"true\"");
    }

    [TestMethod]
    public void ReflectionSwitch_DisablesReflectionButKeepsUntypedContainersWorking()
    {
        var root = FindRepoRoot();
        var smokeDll = Path.Combine(root, "src", "SharpYaml.FeatureSwitchSmoke", "bin", "Release", "net10.0", "SharpYaml.FeatureSwitchSmoke.dll");

        Assert.IsTrue(File.Exists(smokeDll), $"Missing smoke executable at {smokeDll}.");

        var startInfo = new ProcessStartInfo("dotnet", $"\"{smokeDll}\"")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        startInfo.Environment["DOTNET_NOLOGO"] = "1";

        using var process = Process.Start(startInfo);
        Assert.IsNotNull(process);
        process.WaitForExit(30_000);

        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();

        if (process.ExitCode != 0)
        {
            Assert.Fail($"Smoke executable failed with exit code {process.ExitCode}.{Environment.NewLine}stdout:{Environment.NewLine}{output}{Environment.NewLine}stderr:{Environment.NewLine}{error}");
        }
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "SharpYaml", "SharpYaml.csproj");
            if (File.Exists(candidate))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root containing src/SharpYaml/SharpYaml.csproj.");
    }
}

