// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;
using SharpYaml.Serialization;

namespace SharpYaml;

/// <summary>
/// Exception that is thrown at the end of a deserialization under <see cref="YamlInvalidValueHandling.FailAtEnd"/>
/// when at least one value was rejected by a converter and skipped.
/// </summary>
/// <remarks>
/// <see cref="YamlException.Start"/> and <see cref="YamlException.End"/> are those of the first invalid value. When the
/// deserialization ended with an error that could not be recovered from, that error is the <see cref="Exception.InnerException"/>.
/// </remarks>
public class YamlInvalidValuesException : YamlException
{
    private static readonly YamlInvalidValue[] None = Array.Empty<YamlInvalidValue>();

    /// <summary>
    /// Initializes a new instance of the <see cref="YamlInvalidValuesException"/> class.
    /// </summary>
    public YamlInvalidValuesException()
    {
        InvalidValues = None;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="YamlInvalidValuesException"/> class.
    /// </summary>
    /// <param name="message">The message.</param>
    public YamlInvalidValuesException(string message)
        : base(message)
    {
        InvalidValues = None;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="YamlInvalidValuesException"/> class.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="inner">The inner.</param>
    public YamlInvalidValuesException(string message, Exception inner)
        : base(message, inner)
    {
        InvalidValues = None;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="YamlInvalidValuesException"/> class listing the values that were skipped.
    /// </summary>
    /// <param name="invalidValues">The values, in document order. At least one is required.</param>
    /// <exception cref="ArgumentNullException"><paramref name="invalidValues"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="invalidValues"/> is empty.</exception>
    public YamlInvalidValuesException(IReadOnlyList<YamlInvalidValue> invalidValues)
        : this(invalidValues, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="YamlInvalidValuesException"/> class listing the values that were skipped
    /// and the error that ended the deserialization.
    /// </summary>
    /// <param name="invalidValues">The values, in document order. At least one is required.</param>
    /// <param name="innerException">The error that ended the deserialization, or <see langword="null"/> when it completed.</param>
    /// <exception cref="ArgumentNullException"><paramref name="invalidValues"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="invalidValues"/> is empty.</exception>
    public YamlInvalidValuesException(IReadOnlyList<YamlInvalidValue> invalidValues, Exception? innerException)
        : base(First(invalidValues).SourceName, First(invalidValues).Exception.Start, First(invalidValues).Exception.End, FormatMessage(invalidValues, innerException), innerException)
    {
        InvalidValues = invalidValues;
    }

    /// <summary>
    /// Gets the values that were rejected and skipped, in document order.
    /// </summary>
    public IReadOnlyList<YamlInvalidValue> InvalidValues { get; }

    private static YamlInvalidValue First(IReadOnlyList<YamlInvalidValue> invalidValues)
    {
        ArgumentGuard.ThrowIfNull(invalidValues);
        if (invalidValues.Count == 0)
        {
            throw new ArgumentException("At least one invalid value is required.", nameof(invalidValues));
        }

        return invalidValues[0];
    }

    private static string FormatMessage(IReadOnlyList<YamlInvalidValue> invalidValues, Exception? innerException)
    {
        var builder = new StringBuilder();
        builder.Append(invalidValues.Count).Append(invalidValues.Count == 1 ? " invalid value was skipped" : " invalid values were skipped");
        builder.Append(innerException is null ? " while deserializing:" : " before the deserialization failed:");
        foreach (var value in invalidValues)
        {
            builder.Append('\n').Append("  ").Append(value.Path).Append(": ").Append(value.Message);
        }

        if (innerException is not null)
        {
            builder.Append('\n').Append("  ").Append(innerException.Message);
        }

        return builder.ToString();
    }
}
