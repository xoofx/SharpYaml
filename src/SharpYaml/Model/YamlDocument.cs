// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using DocumentEnd = SharpYaml.Events.DocumentEnd;
using DocumentStart = SharpYaml.Events.DocumentStart;

namespace SharpYaml.Model;

/// <summary>Represents the Yaml Document.</summary>
public class YamlDocument : YamlNode
{
    private DocumentStart _documentStart;
    private DocumentEnd _documentEnd;
    private YamlElement? _contents;

    /// <summary>Initializes a new instance of this type.</summary>
    public YamlDocument()
    {
        _documentStart = new DocumentStart(null, new TagDirectiveCollection(), true);
        _documentEnd = new DocumentEnd(true);
    }

    YamlDocument(DocumentStart documentStart, DocumentEnd documentEnd, YamlElement? contents, YamlNodeTracker? tracker)
    {
        Tracker = tracker;

        DocumentStart = documentStart;
        DocumentEnd = documentEnd;
        Contents = contents;
    }

    /// <summary>Loads data.</summary>
    public static YamlDocument Load(EventReader eventReader, YamlNodeTracker? tracker = null)
    {
        var documentStart = eventReader.Allow<DocumentStart>();

        var anchors = new Dictionary<string, YamlElement>(StringComparer.Ordinal);
        var contents = ReadElement(eventReader, tracker, anchors);

        var documentEnd = eventReader.Allow<DocumentEnd>();

        return new YamlDocument(documentStart, documentEnd, contents, tracker);
    }

    /// <summary>Gets document Start.</summary>
    public DocumentStart DocumentStart
    {
        get => _documentStart;
        set
        {
            var oldValue = _documentStart;

            _documentStart = value;

            if (Tracker != null)
                Tracker.OnDocumentStartChanged(this, oldValue, value);
        }
    }

    /// <summary>Gets document End.</summary>
    public DocumentEnd DocumentEnd
    {
        get => _documentEnd;
        set
        {
            var oldValue = _documentEnd;

            _documentEnd = value;

            if (Tracker != null)
                Tracker.OnDocumentEndChanged(this, oldValue, value);
        }
    }

    /// <summary>Gets contents.</summary>
    public YamlElement? Contents
    {
        get { return _contents; }
        set
        {
            var oldValue = _contents;

            _contents = value;

            if (Tracker != null)
            {
                value.Tracker = Tracker;
                Tracker.OnDocumentContentsChanged(this, oldValue, value);
            }
        }
    }

    /// <summary>Gets tracker.</summary>
    public override YamlNodeTracker? Tracker
    {
        get { return base.Tracker; }
        internal set
        {
            if (Tracker == value)
                return;

            base.Tracker = value;

            if (_contents != null)
            {
                _contents.Tracker = value;
                Tracker.OnDocumentContentsChanged(this, null, _contents);
            }
        }
    }

    /// <summary>Creates a deep clone of the current value.</summary>
    public override YamlNode DeepClone(YamlNodeTracker? tracker = null)
    {
        return new YamlDocument(_documentStart, _documentEnd, (YamlElement?)Contents?.DeepClone(), tracker);
    }
}