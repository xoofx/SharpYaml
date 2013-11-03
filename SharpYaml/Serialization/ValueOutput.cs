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
using SharpYaml.Events;

namespace SharpYaml.Serialization
{
	/// <summary>
	/// A deserialized value used by <see cref="IYamlSerializable.ReadYaml"/> that
	/// can be a direct value or an alias. This is used to handle for forward alias.
	/// If an alias is found in a <see cref="ValueOutput"/>, the caller usually
	/// register a late binding instruction through <see cref="SerializerContext.AddAliasBinding"/>
	/// that will be called once the whole document has been parsed, in order to 
	/// resolve all remaining aliases.
	/// </summary>
	public struct ValueOutput
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="ValueOutput"/> struct that contains a value.
		/// </summary>
		/// <param name="value">The value.</param>
		public ValueOutput(object value) : this()
		{
			Value = value;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ValueOutput" /> struct that contains an <see cref="AnchorAlias"/>.
		/// </summary>
		/// <param name="value">The value.</param>
		public ValueOutput(AnchorAlias value)
		{
			Value = value;
		}

		/// <summary>
		/// The returned value or null if no value.
		/// </summary>
		public readonly object Value;

		/// <summary>
		/// True if this value result is an alias.
		/// </summary>
		public bool IsAlias
		{
			get { return Value is AnchorAlias; }
		}

		/// <summary>
		/// Gets the alias, only valid if <see cref="IsAlias"/> is true, null otherwise.
		/// </summary>
		/// <value>The alias.</value>
		public AnchorAlias Alias
		{
			get { return Value as AnchorAlias; }
		}

		/// <summary>
		/// Returns a <see cref="System.String" /> that represents this instance.
		/// </summary>
		/// <returns>A <see cref="System.String" /> that represents this instance.</returns>
		public override string ToString()
		{
			return string.Format("{0}", Value);
		}
	}
}