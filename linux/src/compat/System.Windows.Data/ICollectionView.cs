using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;

namespace System.ComponentModel
{
    /// <summary>The subset of WPF's ICollectionView FModel's view models use.</summary>
    public interface ICollectionView : IEnumerable, INotifyCollectionChanged, INotifyPropertyChanged
    {
        IEnumerable SourceCollection { get; }
        Predicate<object> Filter { get; set; }
        bool CanFilter { get; }
        bool CanSort { get; }
        bool CanGroup { get; }
        bool IsEmpty { get; }
        object CurrentItem { get; }
        int CurrentPosition { get; }
        ObservableCollection<SortDescription> SortDescriptions { get; }
        ObservableCollection<GroupDescription> GroupDescriptions { get; }
        void Refresh();
        IDisposable DeferRefresh();
    }

    public readonly record struct SortDescription(string PropertyName, ListSortDirection Direction);
}

namespace System.Windows.Data
{
    public abstract class GroupDescription
    {
        public abstract object GroupNameFromItem(object item, int level, CultureInfo culture);
    }

    public class PropertyGroupDescription : GroupDescription
    {
        public PropertyGroupDescription() { }
        public PropertyGroupDescription(string propertyName) => PropertyName = propertyName;
        public PropertyGroupDescription(string propertyName, IValueConverter converter) : this(propertyName) => Converter = converter;

        public string PropertyName { get; set; }
        public IValueConverter Converter { get; set; }

        public override object GroupNameFromItem(object item, int level, CultureInfo culture)
        {
            var value = item;
            foreach (var part in (PropertyName ?? string.Empty).Split('.', StringSplitOptions.RemoveEmptyEntries))
                value = value?.GetType().GetProperty(part)?.GetValue(value);
            return Converter != null ? Converter.Convert(value, typeof(object), null, culture) : value;
        }
    }

    public static class CollectionViewSource
    {
        public static ICollectionView GetDefaultView(object source) => source switch
        {
            ICollectionView view => view,
            IList list => new ListCollectionView(list),
            _ => null
        };
    }

    public static class Binding
    {
        public static readonly object DoNothing = Avalonia.Data.BindingOperations.DoNothing;
    }
}
