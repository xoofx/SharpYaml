// Copyright (c) 2013 SharpYaml - Alexandre Mutel
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
// 
// -------------------------------------------------------------------------------
// SharpYaml is a fork of YamlDotNet https://github.com/aaubry/YamlDotNet
// published with the following license:
// -------------------------------------------------------------------------------
// 
// Copyright (c) 2008, 2009, 2010, 2011, 2012 Antoine Aubry
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of
// this software and associated documentation files (the "Software"), to deal in
// the Software without restriction, including without limitation the rights to
// use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies
// of the Software, and to permit persons to whom the Software is furnished to do
// so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
using System;
using SharpYaml.Events;

namespace SharpYaml.Serialization.Serializers
{
	public abstract class ScalarSerializerBase : IYamlSerializable
	{
		public ValueOutput ReadYaml(SerializerContext context, object value, ITypeDescriptor typeDescriptor)
		{
			var scalar = context.Reader.Expect<Scalar>();
			return new ValueOutput(ConvertFrom(context, value, scalar, typeDescriptor));
		}

		public abstract object ConvertFrom(SerializerContext context, object value, Scalar fromScalar, ITypeDescriptor typeDescriptor);

		public void WriteYaml(SerializerContext context, ValueInput input, ITypeDescriptor typeDescriptor)
		{
			var value = input.Value;
			var typeOfValue = value.GetType();

			var isSchemaImplicitTag = context.Schema.IsTagImplicit(input.Tag);
			var scalar = new ScalarEventInfo(value, typeOfValue)
				{
					IsPlainImplicit = isSchemaImplicitTag,
					Style = ScalarStyle.Plain,
					Anchor = context.GetAnchor(),
					Tag = input.Tag,
				};


			// Parse default types 
			switch (Type.GetTypeCode(typeOfValue))
			{
				case TypeCode.Object:
				case TypeCode.String:
				case TypeCode.Char:
					scalar.Style = ScalarStyle.Any;
					break;
			}

			scalar.RenderedValue =  ConvertTo(context, value, typeDescriptor);

            // If we are encoding a key, 
		    if (context.EncodeScalarKey != null)
		    {
                // Set it back to null, as the key is encoded.
                scalar.RenderedValue = context.EncodeScalarKey(value, scalar.RenderedValue);
                context.EncodeScalarKey = null;
		    }

		    // Emit the scalar
			context.Writer.Emit(scalar);
		}

		public abstract string ConvertTo(SerializerContext context, object value, ITypeDescriptor typeDescriptor);
	}
}