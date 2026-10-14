using Avalonia.Controls;
using Avalonia.Interactivity;
using GitLlama.Desktop.ViewModels;

namespace GitLlama.Desktop.Views;

public partial class RemotesView
{
    private async void AddRemote_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm && TopLevel.GetTopLevel(this) is Window owner)
        {
            await RemoteEditDialog.ConnectionAsync(owner, vm, null);
        }
    }

    private async void EditRemote_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm && TopLevel.GetTopLevel(this) is Window owner)
        {
            if (vm.SelectedRemote is { } remote)
            {
                await RemoteEditDialog.ConnectionAsync(owner, vm, remote);
            }
            else
            {
                vm.Status = "Select a remote to edit.";
            }
        }
    }

    private async void RemoveRemote_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm && TopLevel.GetTopLevel(this) is Window owner)
        {
            if (vm.SelectedRemote is { } remote)
            {
                await RemoteEditDialog.ConnectionAsync(owner, vm, remote, remove: true);
            }
            else
            {
                vm.Status = "Select a remote to remove.";
            }
        }
    }
}
