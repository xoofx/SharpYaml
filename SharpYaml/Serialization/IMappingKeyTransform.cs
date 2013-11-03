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
namespace SharpYaml.Serialization
{
    /// <summary>
    /// Interface used to hook the encoding and decoding of key in Yaml mapping.
    /// </summary>
    public interface IMappingKeyTransform
    {
        /// <summary>
        /// Encodes the specified key.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="thisObject">The this object.</param>
        /// <param name="descriptor">The descriptor.</param>
        /// <param name="key">The object key.</param>
        /// <param name="keyText">The key representation in text.</param>
        /// <returns>A new key representation that will be serialized as-is.</returns>
        string Encode(SerializerContext context, object thisObject, ITypeDescriptor descriptor, object key, string keyText);

        /// <summary>
        /// Decodes the specified key from text, this method is called method instantiating the actual key object.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="thisObject">The this object.</param>
        /// <param name="descriptor">The descriptor.</param>
        /// <param name="keyIn">The key being read from the input stream.</param>
        /// <param name="keyOut">The key decoded or same as keyIn if not handled.</param>
        /// <returns><c>true</c> if key was decoded, <c>false</c> otherwise.</returns>
        bool DecodePre(SerializerContext context, object thisObject, ITypeDescriptor descriptor, string keyIn, out string keyOut);

        /// <summary>
        /// Decodes the specified key from text, this method is called after the key has been decoded.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="thisObject">The this object.</param>
        /// <param name="descriptor">The descriptor.</param>
        /// <param name="key">The key object (will be a <see cref="IMemberDescriptor"/> for an object or the key object of a dictionary).</param>
        /// <param name="keyIn">The same keyIn from a previous call to <see cref="DecodePre"/>.</param>
        void DecodePost(SerializerContext context, object thisObject, ITypeDescriptor descriptor, object key, string keyIn);
    }
}