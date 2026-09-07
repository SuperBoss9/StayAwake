using System.Windows;
using StayAwake.Models;
using StayAwake.ViewModels;

namespace StayAwake.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow(SettingsViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void PickProcess_Click(object sender, RoutedEventArgs e)
    {
        var picker = new ProcessPickerWindow(new ProcessPickerViewModel())
        {
            Owner = this
        };
        if (picker.ShowDialog() == true && picker.ViewModel.Result is ProcessRuleModel rule
            && DataContext is SettingsViewModel svm)
        {
            svm.ProcessRules.Add(rule);
        }
    }
}
