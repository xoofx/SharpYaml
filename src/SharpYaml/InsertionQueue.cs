// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace SharpYaml;

/// <summary>
/// Generic queue on which items may be inserted
/// </summary>
internal sealed class InsertionQueue<T>
{
    private readonly List<T> _items = new();
    private int _headIndex;

    /// <summary>
    /// Gets the number of items that are contained by the queue.
    /// </summary>
    public int Count => _items.Count - _headIndex;

    /// <summary>
    /// Enqueues the specified item.
    /// </summary>
    /// <param name="item">The item to be enqueued.</param>
    public void Enqueue(T item)
    {
        _items.Add(item);
    }

    /// <summary>
    /// Dequeues an item.
    /// </summary>
    /// <returns>Returns the item that been dequeued.</returns>
    public T Dequeue()
    {
        if (Count == 0)
        {
            throw new InvalidOperationException("The queue is empty");
        }

        var item = _items[_headIndex++];

        // Periodically compact the buffer to avoid unbounded growth when dequeuing many items.
        // This keeps Dequeue O(1) while supporting Insert(...) into the live window.
        if (_headIndex > 64 && _headIndex * 2 > _items.Count)
        {
            _items.RemoveRange(0, _headIndex);
            _headIndex = 0;
        }

        return item;
    }

    /// <summary>
    /// Inserts an item at the specified index.
    /// </summary>
    /// <param name="index">The index where to insert the item.</param>
    /// <param name="item">The item to be inserted.</param>
    public void Insert(int index, T item)
    {
        _items.Insert(_headIndex + index, item);
    }
}