﻿// Copyright (c) 2013 SharpYaml - Alexandre Mutel
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
using System.IO;
using SharpYaml.Events;
using SharpYaml.Serialization.Descriptors;
using SharpYaml.Serialization.Serializers;

namespace SharpYaml.Serialization
{
	/// <summary>
	/// Serializes and deserializes objects into and from YAML documents.
	/// </summary>
	public sealed class Serializer
	{
		private readonly SerializerSettings settings;

	    internal readonly IYamlSerializable ObjectSerializer;
	    internal readonly ITypeDescriptorFactory TypeDescriptorFactory;

		private static readonly IYamlSerializableFactory[] DefaultFactories = new IYamlSerializableFactory[]
			{
				new PrimitiveSerializer(),
				new DictionarySerializer(),
				new CollectionSerializer(),
				new ArraySerializer(),
				new ObjectSerializer(),
			};

		/// <summary>
		/// Initializes a new instance of the <see cref="Serializer"/> class.
		/// </summary>
		public Serializer() : this(null)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Serializer"/> class.
		/// </summary>
		/// <param name="settings">The settings.</param>
		public Serializer(SerializerSettings settings)
		{
			this.settings = settings ?? new SerializerSettings();
            TypeDescriptorFactory = CreateTypeDescriptorFactory();
            ObjectSerializer = CreateProcessor();
		}

		/// <summary>
		/// Gets the settings.
		/// </summary>
		/// <value>The settings.</value>
		public SerializerSettings Settings { get { return settings; } }

		/// <summary>
		/// Serializes the specified object to a string.
		/// </summary>
		/// <param name="graph">The graph.</param>
		/// <returns>A YAML string of the object.</returns>
		public string Serialize(object graph)
		{
			var stringWriter = new StringWriter();
			Serialize(stringWriter, graph);
			return stringWriter.ToString();
		}

		/// <summary>
		/// Serializes the specified object to a string.
		/// </summary>
		/// <param name="graph">The graph.</param>
		/// <param name="expectedType">The expected type.</param>
		/// <returns>A YAML string of the object.</returns>
		public string Serialize(object graph, Type expectedType)
		{
			var stringWriter = new StringWriter();
			Serialize(stringWriter, graph, expectedType);
			return stringWriter.ToString();
		}

		/// <summary>
		/// Serializes the specified object.
		/// </summary>
		/// <param name="stream">The stream.</param>
		/// <param name="graph">The object to serialize.</param>
		public void Serialize(Stream stream, object graph)
		{
			Serialize(stream, graph, null);
		}

		/// <summary>
		/// Serializes the specified object.
		/// </summary>
		/// <param name="stream">The stream.</param>
		/// <param name="graph">The object to serialize.</param>
		public void Serialize(Stream stream, object graph, Type expectedType)
		{
			var writer = new StreamWriter(stream);
			try
			{
				Serialize(writer, graph, expectedType);
			}
			finally
			{
				try
				{
					writer.Flush();
				}
				catch (Exception)
				{
				}
			}
		}

		/// <summary>
		/// Serializes the specified object.
		/// </summary>
		/// <param name="writer">The <see cref="TextWriter" /> where to serialize the object.</param>
		/// <param name="graph">The object to serialize.</param>
		public void Serialize(TextWriter writer, object graph)
		{
			Serialize(new Emitter(writer, Settings.PreferredIndent), graph);
		}

		/// <summary>
		/// Serializes the specified object.
		/// </summary>
		/// <param name="writer">The <see cref="TextWriter" /> where to serialize the object.</param>
		/// <param name="graph">The object to serialize.</param>
		/// <param name="type">The static type of the object to serialize.</param>
		public void Serialize(TextWriter writer, object graph, Type type)
		{
			Serialize(new Emitter(writer, Settings.PreferredIndent), graph, type);
		}

		/// <summary>
		/// Serializes the specified object.
		/// </summary>
		/// <param name="emitter">The <see cref="IEmitter" /> where to serialize the object.</param>
		/// <param name="graph">The object to serialize.</param>
		public void Serialize(IEmitter emitter, object graph)
		{
			Serialize(emitter, graph, graph == null ? typeof(object) : null);
		}

		/// <summary>
		/// Serializes the specified object.
		/// </summary>
		/// <param name="emitter">The <see cref="IEmitter" /> where to serialize the object.</param>
		/// <param name="graph">The object to serialize.</param>
		/// <param name="type">The static type of the object to serialize.</param>
		public void Serialize(IEmitter emitter, object graph, Type type)
		{
			if (emitter == null)
			{
				throw new ArgumentNullException("emitter");
			}

			if (graph == null && type == null)
			{
				throw new ArgumentNullException("type");
			}

			// Configure the emitter
			// TODO the current emitter is not enough configurable to format its output
			// This should be improved
			var defaultEmitter = emitter as Emitter;
			if (defaultEmitter != null)
			{
				defaultEmitter.ForceIndentLess = settings.IndentLess;
			}

            var context = new SerializerContext(this) {Emitter = emitter, Writer = CreateEmitter(emitter)};

		    // Serialize the document
			context.Writer.StreamStart();
			context.Writer.DocumentStart();
			context.WriteYaml(graph, type);
			context.Writer.DocumentEnd();
			context.Writer.StreamEnd();
		}

		/// <summary>
		/// Deserializes an object from the specified <see cref="Stream"/>.
		/// </summary>
		/// <param name="stream">The stream.</param>
		/// <returns>A deserialized object.</returns>
		public object Deserialize(Stream stream)
		{
			return Deserialize(stream, null);
		}

		/// <summary>
		/// Deserializes an object from the specified <see cref="TextReader"/>.
		/// </summary>
		/// <param name="reader">The reader.</param>
		/// <returns>A deserialized object.</returns>
		public object Deserialize(TextReader reader)
		{
			return Deserialize((TextReader)reader, null);
		}

		/// <summary>
		/// Deserializes an object from the specified <see cref="Stream" /> with an expected specific type.
		/// </summary>
		/// <param name="stream">The stream.</param>
		/// <param name="expectedType">The expected type.</param>
		/// <returns>A deserialized object.</returns>
		/// <exception cref="System.ArgumentNullException">stream</exception>
		public object Deserialize(Stream stream, Type expectedType)
		{
			if (stream == null) throw new ArgumentNullException("stream");

			return Deserialize(new StreamReader(stream), expectedType);
		}

		/// <summary>
		/// Deserializes an object from the specified <see cref="Stream" /> with an expected specific type.
		/// </summary>
		/// <typeparam name="T">The expected type</typeparam>
		/// <param name="stream">The stream.</param>
		/// <returns>A deserialized object.</returns>
		/// <exception cref="System.ArgumentNullException">stream</exception>
		public T Deserialize<T>(Stream stream)
		{
			return (T) Deserialize(stream, typeof (T));
		}

	    /// <summary>
	    /// Deserializes an object from the specified <see cref="TextReader" /> with an expected specific type.
	    /// </summary>
	    /// <param name="reader">The reader.</param>
	    /// <param name="expectedType">The expected type.</param>
	    /// <param name="existingObject">The object to deserialize into. If null (the default) then a new object will be created</param>
	    /// <returns>A deserialized object.</returns>
	    /// <exception cref="System.ArgumentNullException">reader</exception>
	    public object Deserialize(TextReader reader, Type expectedType, object existingObject=null)
		{
			if (reader == null) throw new ArgumentNullException("reader");
			return Deserialize(new EventReader(new Parser(reader)), expectedType, existingObject);
		}

	    /// <summary>
	    /// Deserializes an object from the specified <see cref="TextReader" /> with an expected specific type.
	    /// </summary>
	    /// <typeparam name="T">The expected type</typeparam>
	    /// <param name="reader">The reader.</param>
        /// <param name="existingObject">The object to deserialize into. If null (the default) then a new object will be created</param>
	    /// <returns>A deserialized object.</returns>
	    /// <exception cref="System.ArgumentNullException">reader</exception>
	    public T Deserialize<T>(TextReader reader, object existingObject=null)
		{
			return (T) Deserialize(reader, typeof (T), existingObject);
		}

	    /// <summary>
	    /// Deserializes an object from the specified string.
	    /// </summary>
	    /// <param name="fromText">The text.</param>
        /// <param name="existingObject">The object to deserialize into. If null (the default) then a new object will be created</param>
	    /// <returns>A deserialized object.</returns>
	    public object Deserialize(string fromText, object existingObject=null)
		{
			return Deserialize(fromText, null, existingObject);
		}

	    /// <summary>
	    /// Deserializes an object from the specified string. with an expected specific type.
	    /// </summary>
	    /// <param name="fromText">From text.</param>
	    /// <param name="expectedType">The expected type.</param>
        /// <param name="existingObject">The object to deserialize into. If null (the default) then a new object will be created</param>
	    /// <returns>A deserialized object.</returns>
	    /// <exception cref="System.ArgumentNullException">stream</exception>
	    public object Deserialize(string fromText, Type expectedType, object existingObject=null)
		{
			if (fromText == null) throw new ArgumentNullException("fromText");
			return Deserialize(new StringReader(fromText), expectedType,existingObject);
		}

	    /// <summary>
	    /// Deserializes an object from the specified string. with an expected specific type.
	    /// </summary>
	    /// <typeparam name="T">The expected type</typeparam>
	    /// <param name="fromText">From text.</param>
        /// <param name="existingObject">The object to deserialize into. If null (the default) then a new object will be created</param>
	    /// <returns>A deserialized object.</returns>
	    /// <exception cref="System.ArgumentNullException">stream</exception>
	    public T Deserialize<T>(string fromText)
		{
			return (T) Deserialize(fromText, typeof (T));
		}

		/// <summary>
		/// Deserializes an object from the specified <see cref="EventReader" /> with an expected specific type.
		/// </summary>
		/// <typeparam name="T">The expected type</typeparam>
		/// <param name="reader">The reader.</param>
		/// <returns>A deserialized object.</returns>
		/// <exception cref="System.ArgumentNullException">reader</exception>
		public T Deserialize<T>(EventReader reader)
		{
			return (T) Deserialize(reader, typeof (T));
		}


        /// <summary>
        /// Deserializes an object from the specified string. with an expected specific type.
        /// </summary>
        /// <typeparam name="T">The expected type</typeparam>
        /// <param name="fromText">From text.</param>
        /// <param name="existingObject">The object to deserialize into.</param>
        /// <returns>A deserialized object.</returns>
        /// <exception cref="System.ArgumentNullException">stream</exception>
        /// 
        /// Note: These need a different name, because otherwise they will conflict with existing Deserialize(string,Type). They are new so the difference should not matter
        public T DeserializeInto<T>(string fromText, T existingObject) { 
            return (T)Deserialize(fromText, typeof(T), existingObject);
        }

        /// <summary>
        /// Deserializes an object from the specified <see cref="EventReader" /> with an expected specific type.
        /// </summary>
        /// <typeparam name="T">The expected type</typeparam>
        /// <param name="reader">The reader.</param>
        /// <param name="existingObject">The object to deserialize into.</param>
        /// <returns>A deserialized object.</returns>
        /// <exception cref="System.ArgumentNullException">reader</exception>
        public T DeserializeInto<T>(EventReader reader, T existingObject) {
            return (T)Deserialize(reader, typeof(T), existingObject);
        }


	    /// <summary>
	    /// Deserializes an object from the specified <see cref="EventReader" /> with an expected specific type.
	    /// </summary>
	    /// <param name="reader">The reader.</param>
	    /// <param name="expectedType">The expected type.</param>
	    /// <param name="existingObject">An object to deserialize into. By default a new object will be created</param>
	    /// <returns>A deserialized object.</returns>
	    /// <exception cref="System.ArgumentNullException">reader</exception>
	    public object Deserialize(EventReader reader, Type expectedType, object existingObject = null)
		{
			if (reader == null) throw new ArgumentNullException("reader");
			
			var hasStreamStart = reader.Allow<StreamStart>() != null;
			var hasDocumentStart = reader.Allow<DocumentStart>() != null;

			object result = null;
			if (!reader.Accept<DocumentEnd>() && !reader.Accept<StreamEnd>())
			{
			    var context = new SerializerContext(this) {Reader = reader};
			    result = context.ReadYaml(existingObject, expectedType);
			}

			if (hasDocumentStart)
			{
				reader.Expect<DocumentEnd>();
			}

			if (hasStreamStart)
			{
				reader.Expect<StreamEnd>();
			}

			return result;
		}

		private IYamlSerializable CreateProcessor()
		{
			var routintSerializer = new RoutingSerializer();

			// Add registered serializer
			foreach (var typeAndSerializer in settings.serializers)
			{
				routintSerializer.AddSerializer(typeAndSerializer.Key, typeAndSerializer.Value);
			}

			// Add registered factories
			foreach (var factory in settings.AssemblyRegistry.SerializableFactories)
			{
				routintSerializer.AddSerializerFactory(factory);
			}

			// Add default factories
			foreach (var defaultFactory in DefaultFactories)
			{
				routintSerializer.AddSerializerFactory(defaultFactory);
			}

			var typingSerializer = new TagTypeSerializer(routintSerializer);
			return settings.EmitAlias ? (IYamlSerializable)new AnchorSerializer(typingSerializer) : typingSerializer;
		}

	    private ITypeDescriptorFactory CreateTypeDescriptorFactory()
	    {
            return new TypeDescriptorFactory(Settings.Attributes, Settings.EmitDefaultValues);
	    }

		private IEventEmitter CreateEmitter(IEmitter emitter)
		{
			var writer = (IEventEmitter)new WriterEventEmitter(emitter);

			if (settings.EmitJsonComptible)
			{
				writer = new JsonEventEmitter(writer);
			}
		    return Settings.EmitAlias ? new AnchorEventEmitter(writer) : writer;
		}
   }
}