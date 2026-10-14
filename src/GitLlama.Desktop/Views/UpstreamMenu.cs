using Avalonia.Controls;
using GitLlama.Desktop.ViewModels;

namespace GitLlama.Desktop.Views;

/// <summary>
/// The upstream picker's menu: each remote lists its branches to push to and pull from, plus a new branch with the
/// current branch's name, and the current choice is ticked.
/// </summary>
public static class UpstreamMenu
{
    public static object[] Items(Control owner, MainWindowViewModel vm)
    {
        var items = new List<object>
        {
            new MenuItem { Header = vm.UpstreamDetail, IsEnabled = false },
            new Separator(),
        };
        MenuItem Choice(string remote, string branch, string header)
        {
            var current = vm.Upstream is { } upstream && upstream.Remote == remote && upstream.Branch == branch;
            var item = new MenuItem { Header = (current ? "✓  " : "     ") + header, IsEnabled = !current };
            item.Click += async (_, _) => await vm.SetUpstreamToAsync(remote, branch);
            return item;
        }

        foreach (var remote in vm.Remotes)
        {
            var branches = vm.RemoteBranchesOf(remote.Name);
            var choices = new List<object>();
            if (!branches.Contains(vm.CurrentBranch))
            {
                choices.Add(Choice(remote.Name, vm.CurrentBranch, $"New branch {remote.Name}/{vm.CurrentBranch}"));
                if (branches.Count > 0)
                {
                    choices.Add(new Separator());
                }
            }

            choices.AddRange(branches.Select(branch => Choice(remote.Name, branch, $"{remote.Name}/{branch}")));
            items.Add(
                new MenuItem
                {
                    Header = (vm.Upstream?.Remote == remote.Name ? "✓  " : "     ") + remote.Name,
                    ItemsSource = choices,
                }
            );
        }

        if (vm.Remotes.Count == 0)
        {
            var add = new MenuItem { Header = "Add a remote…" };
            add.Click += async (_, _) =>
            {
                if (TopLevel.GetTopLevel(owner) is Window window)
                {
                    await RemoteEditDialog.ConnectionAsync(window, vm, null);
                }
            };
            items.Add(add);
        }

        if (vm.HasUpstream)
        {
            items.Add(new Separator());
            var stop = new MenuItem { Header = "Stop tracking" };
            stop.Click += async (_, _) => await vm.StopTrackingAsync();
            items.Add(stop);
        }

        return items.ToArray();
    }
}
