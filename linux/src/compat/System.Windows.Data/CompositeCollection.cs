using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;

namespace System.Windows.Data;

/// <summary>
/// WPF's CompositeCollection: a read-only, live concatenation of several collections, each wrapped in a
/// <see cref="CollectionContainer"/>. Supports the collection initializer syntax upstream uses.
/// </summary>
public class CompositeCollection : IList, INotifyCollectionChanged, INotifyPropertyChanged
{
    private readonly List<object> _parts = [];
    private List<object> _items = [];

    public void Add(CollectionContainer container) => ((IList) this).Add(container);

    int IList.Add(object value)
    {
        _parts.Add(value);
        if (value is CollectionContainer { Collection: INotifyCollectionChanged incc })
            incc.CollectionChanged += (_, _) => Rebuild();
        Rebuild();
        return _parts.Count - 1;
    }

    private void Rebuild()
    {
        _items = _parts.SelectMany(p => p is CollectionContainer c ? c.Collection?.Cast<object>() ?? [] : [p]).ToList();
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Count)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    public event NotifyCollectionChangedEventHandler CollectionChanged;
    public event PropertyChangedEventHandler PropertyChanged;

    public int Count => _items.Count;
    public object this[int index] { get => _items[index]; set => throw new NotSupportedException(); }
    public IEnumerator GetEnumerator() => _items.GetEnumerator();
    public bool Contains(object value) => _items.Contains(value);
    public int IndexOf(object value) => _items.IndexOf(value);
    public void CopyTo(Array array, int index) => ((ICollection) _items).CopyTo(array, index);
    public bool IsReadOnly => true;
    public bool IsFixedSize => false;
    public bool IsSynchronized => false;
    public object SyncRoot => this;
    public void Clear() => throw new NotSupportedException();
    public void Insert(int index, object value) => throw new NotSupportedException();
    public void Remove(object value) => throw new NotSupportedException();
    public void RemoveAt(int index) => throw new NotSupportedException();
}

public class CollectionContainer
{
    public IEnumerable Collection { get; set; }
}
