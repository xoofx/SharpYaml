// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

using System;

namespace SharpYaml;

/// <summary>
/// Generic queue on which items may be inserted
/// </summary>
/// <remarks>
/// The items are the live window <c>[_head, _head + _count)</c> of an array. The scanner empties the queue every few
/// tokens, which brings the window back to the start of the array, so it rarely moves or grows.
/// </remarks>
internal sealed class InsertionQueue<T>
{
    private Slot[] _items = new Slot[16];
    private int _head;
    private int _count;

    /// <summary>
    /// Gets the number of items that are contained by the queue.
    /// </summary>
    public int Count => _count;

    /// <summary>
    /// Enqueues the specified item.
    /// </summary>
    /// <param name="item">The item to be enqueued.</param>
    public void Enqueue(T item)
    {
        if (_head + _count == _items.Length)
        {
            MakeRoom();
        }

        _items[_head + _count].Value = item;
        _count++;
    }

    /// <summary>
    /// Dequeues an item.
    /// </summary>
    /// <returns>Returns the item that been dequeued.</returns>
    public T Dequeue()
    {
        if (_count == 0)
        {
            throw new InvalidOperationException("The queue is empty");
        }

        ref var slot = ref _items[_head];
        var item = slot.Value;
        slot.Value = default!;
        _count--;
        _head = _count == 0 ? 0 : _head + 1;
        return item;
    }

    /// <summary>
    /// Inserts an item at the specified index.
    /// </summary>
    /// <param name="index">The index where to insert the item.</param>
    /// <param name="item">The item to be inserted.</param>
    public void Insert(int index, T item)
    {
        if ((uint)index > (uint)_count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        if (_head + _count == _items.Length)
        {
            MakeRoom();
        }

        var position = _head + index;
        for (var i = _head + _count; i > position; i--)
        {
            _items[i] = _items[i - 1];
        }

        _items[position].Value = item;
        _count++;
    }

    /// <summary>Moves the window back to the start of the array, or grows the array when the window fills it.</summary>
    private void MakeRoom()
    {
        if (_head == 0)
        {
            Array.Resize(ref _items, _items.Length * 2);
            return;
        }

        Array.Copy(_items, _head, _items, 0, _count);
        Array.Clear(_items, _count, _head);
        _head = 0;
    }

    /// <summary>
    /// An item in a struct, so that the array is not an array of references: storing a reference into such an array
    /// checks its type against the array's on every write.
    /// </summary>
    private struct Slot
    {
        public T Value;
    }
}
