using System.Windows;
using StayAwake.ViewModels;

namespace StayAwake.Views;

public partial class ProcessPickerWindow : Window
{
    public ProcessPickerViewModel ViewModel { get; }

    public ProcessPickerWindow(ProcessPickerViewModel vm)
    {
        InitializeComponent();
        ViewModel = vm;
        DataContext = vm;
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.Confirmed)
        {
            DialogResult = true;
            Close();
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
