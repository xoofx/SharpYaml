using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpYaml.Serialization;

namespace SharpYaml.Tests.Serialization;

[TestClass]
public sealed class YamlConverterExceptionTests
{
    private static readonly YamlSerializerOptions s_throwingOptions = new()
    {
        Converters = new YamlConverter[] { new ThrowingExceptionValueConverter() },
    };

    [TestMethod]
    public void Reflection_RootValue_WrapsConverterException()
    {
        var exception = Assert.ThrowsExactly<YamlException>(() => YamlSerializer.Deserialize<ExceptionValue>("x\n", s_throwingOptions));

        AssertWrapsConverterException(exception);
    }

    [TestMethod]
    public void Reflection_RootList_WrapsConverterException()
    {
        var exception = Assert.ThrowsExactly<YamlException>(() => YamlSerializer.Deserialize<List<ExceptionValue>>("- x\n", s_throwingOptions));

        AssertWrapsConverterException(exception);
    }

    [TestMethod]
    public void Reflection_Member_WrapsConverterException()
    {
        var exception = Assert.ThrowsExactly<YamlException>(() => YamlSerializer.Deserialize<ExceptionValueHolder>("Value: x\n", s_throwingOptions));

        AssertWrapsConverterException(exception);
    }

    [TestMethod]
    public void Reflection_TextReader_WrapsConverterException()
    {
        var exception = Assert.ThrowsExactly<YamlException>(() => YamlSerializer.Deserialize<ExceptionValue>(new StringReader("x\n"), s_throwingOptions));

        AssertWrapsConverterException(exception);
    }

    [TestMethod]
    public void Reflection_TryDeserialize_ReturnsFalseForConverterException()
    {
        Assert.IsFalse(YamlSerializer.TryDeserialize<ExceptionValue>("x\n", out var value, s_throwingOptions));
        Assert.IsNull(value);
    }

    [TestMethod]
    public void Generated_Member_WrapsConverterException()
    {
        var exception = Assert.ThrowsExactly<YamlException>(() => YamlSerializer.Deserialize("Name: a\nValue: x\n", ConverterExceptionContext.Default.ExceptionValueHolder));

        AssertWrapsConverterException(exception);
        // The reader is still on the value the converter failed to read.
        Assert.AreEqual(1, exception.Start.Line);
        Assert.AreEqual(7, exception.Start.Column);
    }

    [TestMethod]
    public void Generated_AttributeConverterMember_WrapsConverterException()
    {
        var exception = Assert.ThrowsExactly<YamlException>(() => YamlSerializer.Deserialize("Value: x\n", AttributeConverterExceptionContext.Default.ExceptionValueAttributeHolder));

        AssertWrapsConverterException(exception);
    }

    [TestMethod]
    public void Generated_ListElement_WrapsConverterException()
    {
        var exception = Assert.ThrowsExactly<YamlException>(() => YamlSerializer.Deserialize("Values: [x]\n", ConverterExceptionContext.Default.ExceptionValueListHolder));

        AssertWrapsConverterException(exception);
    }

    [TestMethod]
    public void Generated_ConstructorParameter_WrapsConverterException()
    {
        var exception = Assert.ThrowsExactly<YamlException>(() => YamlSerializer.Deserialize("Value: x\n", ConverterExceptionContext.Default.ExceptionValueCtorHolder));

        AssertWrapsConverterException(exception);
    }

    [TestMethod]
    public void Generated_TryDeserialize_ReturnsFalseForConverterException()
    {
        Assert.IsFalse(YamlSerializer.TryDeserialize<ExceptionValueHolder>("Value: x\n", ConverterExceptionContext.Default, out var value));
        Assert.IsNull(value);
    }

    [TestMethod]
    public void YamlExceptionFromConverter_IsNotWrapped()
    {
        var options = new YamlSerializerOptions { Converters = new YamlConverter[] { new YamlExceptionValueConverter() } };

        var exception = Assert.ThrowsExactly<YamlException>(() => YamlSerializer.Deserialize<ExceptionValue>("x\n", options));

        Assert.IsNull(exception.InnerException);
        StringAssert.Contains(exception.Message, "converter message");
    }

    [TestMethod]
    public void NotSupportedExceptionFromConverter_IsNotWrapped()
    {
        var options = new YamlSerializerOptions { Converters = new YamlConverter[] { new NotSupportedExceptionValueConverter() } };

        Assert.ThrowsExactly<NotSupportedException>(() => YamlSerializer.Deserialize<ExceptionValue>("x\n", options));
    }

    private static void AssertWrapsConverterException(YamlException exception)
    {
        Assert.IsInstanceOfType<InvalidOperationException>(exception.InnerException);
        Assert.AreEqual("boom", exception.InnerException!.Message);
        StringAssert.Contains(exception.Message, "boom");
    }
}

public sealed class ExceptionValue
{
    public string? Text { get; set; }
}

public sealed class ExceptionValueHolder
{
    public string? Name { get; set; }

    public ExceptionValue? Value { get; set; }
}

public sealed class ExceptionValueAttributeHolder
{
    [YamlConverter(typeof(ThrowingExceptionValueConverter))]
    public ExceptionValue? Value { get; set; }
}

public sealed class ExceptionValueListHolder
{
    public List<ExceptionValue>? Values { get; set; }
}

public sealed class ExceptionValueCtorHolder
{
    public ExceptionValueCtorHolder(ExceptionValue? value)
    {
        Value = value;
    }

    public ExceptionValue? Value { get; }
}

public sealed class ThrowingExceptionValueConverter : YamlConverter<ExceptionValue>
{
    public override ExceptionValue Read(YamlReader reader) => throw new InvalidOperationException("boom");

    public override void Write(YamlWriter writer, ExceptionValue value) => writer.WriteScalar(value.Text ?? string.Empty);
}

public sealed class YamlExceptionValueConverter : YamlConverter<ExceptionValue>
{
    public override ExceptionValue Read(YamlReader reader) => throw new YamlException(reader.SourceName, reader.Start, reader.End, "converter message");

    public override void Write(YamlWriter writer, ExceptionValue value) => writer.WriteScalar(value.Text ?? string.Empty);
}

public sealed class NotSupportedExceptionValueConverter : YamlConverter<ExceptionValue>
{
    public override ExceptionValue Read(YamlReader reader) => throw new NotSupportedException("not supported");

    public override void Write(YamlWriter writer, ExceptionValue value) => writer.WriteScalar(value.Text ?? string.Empty);
}

[YamlSourceGenerationOptions(Converters = new[] { typeof(ThrowingExceptionValueConverter) })]
[YamlSerializable(typeof(ExceptionValueHolder))]
[YamlSerializable(typeof(ExceptionValueListHolder))]
[YamlSerializable(typeof(ExceptionValueCtorHolder))]
internal partial class ConverterExceptionContext : YamlSerializerContext
{
}

[YamlSerializable(typeof(ExceptionValueAttributeHolder))]
internal partial class AttributeConverterExceptionContext : YamlSerializerContext
{
}
