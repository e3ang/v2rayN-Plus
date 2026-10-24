namespace v2rayN.Views;

/// <summary>
/// Per-app traffic and routing control
/// </summary>
public partial class AppTrafficView
{
    public AppTrafficView()
    {
        InitializeComponent();

        this.WhenActivated(disposables =>
        {
            this.OneWayBind(ViewModel, vm => vm.AppItems, v => v.lstApps.ItemsSource).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSource, v => v.lstApps.SelectedItem).DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.BlockCmd, v => v.menuAppBlock).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.DirectCmd, v => v.menuAppDirect).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.ProxyCmd, v => v.menuAppProxy).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.ResetRuleCmd, v => v.menuAppReset).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.CloseConnectionsCmd, v => v.menuAppCloseConnections).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.ResetStatsCmd, v => v.menuAppResetStats).DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.BlockCmd, v => v.btnBlock).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.DirectCmd, v => v.btnDirect).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.ProxyCmd, v => v.btnProxy).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.ResetRuleCmd, v => v.btnReset).DisposeWith(disposables);
        });
    }
}
