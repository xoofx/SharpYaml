﻿using System;

namespace SharpYaml.Serialization
{
    /// <summary>
    /// Provides tag discovery from a type and type discovery from a tag.
    /// </summary>
    public interface ITagTypeResolver
    {
        /// <summary>
        /// Finds a type from a tag, null if not found.
        /// </summary>
        /// <param name="tagName">Name of the tag.</param>
        /// <param name="isAlias"></param>
        /// <returns>A Type or null if not found</returns>
        Type? TypeFromTag(string? tagName, out bool isAlias);

        /// <summary>
        /// Finds a tag from a type, null if not found.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns>A tag or null if not found</returns>
        string TagFromType(Type type);

        /// <summary>
        /// Resolves a type from the specified typeName.
        /// </summary>
        /// <param name="typeName">Name of the type.</param>
        /// <returns>Type found for this typeName</returns>
        Type? ResolveType(string typeName);
    }
}
