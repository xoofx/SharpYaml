using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpYaml.Syntax;

namespace SharpYaml.Tests;

[TestClass]
public sealed class YamlVersionDirectiveTests
{
    [TestMethod]
    public void Parse_WithYaml12Directive_ShouldSucceed()
    {
        var yaml = "%YAML 1.2\n---\na: 1\n";

        _ = YamlSyntaxTree.Parse(yaml);
    }

    [TestMethod]
    public void Parse_WithUnsupportedYamlDirective_ShouldThrow()
    {
        var yaml = "%YAML 1.3\n---\na: 1\n";

        var ex = Assert.Throws<SemanticErrorException>(() => YamlSyntaxTree.Parse(yaml));
        StringAssert.Contains(ex.Message, "incompatible");
    }
}
