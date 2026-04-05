using System;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace WatchCollection.ViewModels;

public abstract class ViewModelBase : ObservableObject, IDisposable
{
    private readonly CancellationTokenSource _cts = new();
    public void Dispose() { _cts.Cancel(); _cts.Dispose(); }
}
