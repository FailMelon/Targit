using System.Collections.ObjectModel;

namespace GitLlama.Desktop.ViewModels;

internal static class CollectionUpdates
{
    internal static void Sync<T>(ObservableCollection<T> target, IEnumerable<T> values, Func<T, string> key)
    {
        var desired = values.ToArray();
        for (var index = 0; index < desired.Length; index++)
        {
            var found = -1;
            for (var candidate = index; candidate < target.Count; candidate++)
                if (key(target[candidate]) == key(desired[index])) { found = candidate; break; }
            if (found < 0) target.Insert(index, desired[index]);
            else
            {
                if (found != index) target.Move(found, index);
                if (!EqualityComparer<T>.Default.Equals(target[index], desired[index])) target[index] = desired[index];
            }
        }
        while (target.Count > desired.Length) target.RemoveAt(target.Count - 1);
    }
}
