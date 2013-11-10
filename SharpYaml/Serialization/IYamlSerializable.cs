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

using SharpYaml.Serialization.Serializers;

namespace SharpYaml.Serialization
{
    public struct ObjectContext
    {
        public ObjectContext(SerializerContext context, object instance, ITypeDescriptor descriptor) : this()
        {
            Context = context;
            Instance = instance;
            Descriptor = descriptor;
        }

        public readonly SerializerContext Context;

        /// <summary>
        /// Gets the current YAML reader. Equivalent to calling directly <see cref="SerializerContext.Reader"/>.
        /// </summary>
        /// <value>The current YAML reader.</value>
        public EventReader Reader
        {
            get
            {
                return Context.Reader;
            }
        }

        /// <summary>
        /// Gets the writer used while deserializing.
        /// </summary>
        /// <value>The writer.</value>
        public IEventEmitter Writer
        {
            get
            {
                return Context.Writer;
            }
        }

        /// <summary>
        /// Gets the settings.
        /// </summary>
        /// <value>The settings.</value>
        public SerializerSettings Settings
        {
            get { return Context.Settings; }
        }

        public IVisitSerializer Visitor
        {
            get
            {
                return Context.Visitor;
            }
        }

        public object Instance;

        public ITypeDescriptor Descriptor;

        public string Tag;

        public string Anchor;

        public YamlStyle Style;
    }


	/// <summary>
	/// Allows an object to customize how it is serialized and deserialized.
	/// </summary>
	public interface IYamlSerializable
	{
	    /// <summary>
	    /// Reads this object's state from a YAML parser.
	    /// </summary>
	    /// <param name="objectContext"></param>
	    /// <returns>A instance of the object deserialized from Yaml.</returns>
	    object ReadYaml(ref ObjectContext objectContext);

	    /// <summary>
	    /// Writes this object's state to a YAML emitter.
	    /// </summary>
	    /// <param name="objectContext"></param>
	    /// <param name="value">The value.</param>
	    void WriteYaml(ref ObjectContext objectContext);
	}
}