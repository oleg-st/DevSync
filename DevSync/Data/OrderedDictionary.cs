using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace DevSync.Data;

// Dictionary with insertion ordered values
// implemented as Dictionary + LinkedList
public class OrderedDictionary<TKey, TValue> where TKey : notnull
{
    protected Dictionary<TKey, LinkedListNode<TValue>> Dictionary = new();
    protected LinkedList<TValue> LinkedList = [];

    public void Clear()
    {
        Dictionary.Clear();
        LinkedList.Clear();
    }

    public int Count => Dictionary.Count;

    public IEnumerable<TValue> Values => LinkedList;

    public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
    {
        if (Dictionary.TryGetValue(key, out var node))
        {
            value = node.Value;
            return true;
        }
        value = default;
        return false;
    }

    public bool ContainsKey(TKey key) => Dictionary.ContainsKey(key);

    public void Remove(TKey key)
    {
        if (Dictionary.Remove(key, out var node))
        {
            LinkedList.Remove(node);
        }
    }

    public bool Remove(TKey key, [MaybeNullWhen(false)] out TValue value)
    {
        if (Dictionary.Remove(key, out var node))
        {
            value = node.Value;
            LinkedList.Remove(node);
            return true;
        }

        value = default;
        return false;
    }

    public TValue this[TKey key]
    {
        get => Dictionary[key].Value;
        set
        {
            if (Dictionary.TryGetValue(key, out var node))
            {
                node.Value = value;
            }
            else
            {
                Add(key, value);
            }
        }
    }

    public void Add(TKey key, TValue value)
    {
        var node = LinkedList.AddLast(value);
        Dictionary.Add(key, node);
    }
}