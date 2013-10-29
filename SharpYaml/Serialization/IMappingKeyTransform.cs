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