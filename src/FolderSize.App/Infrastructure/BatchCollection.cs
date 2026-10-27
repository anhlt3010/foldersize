using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
namespace FolderSize.App.Infrastructure;
public sealed class BatchCollection<T> : ObservableCollection<T>
{
    public void Replace(IReadOnlyList<T> values)
    {
        CheckReentrancy();
        Items.Clear(); foreach (var value in values) Items.Add(value);
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}
