using System.Windows;

namespace MouseTool;

internal enum CloseChoiceAction
{
    Cancel,
    MinimizeToTray,
    StopAndExit
}

internal partial class CloseChoiceWindow : Window
{
    internal CloseChoiceAction SelectedAction { get; private set; } = CloseChoiceAction.Cancel;

    internal CloseChoiceWindow(MouseKeeperApplicationContext context)
    {
        InitializeComponent();
        Title = context.T("CloseDialogTitle");
        QuestionTextBlock.Text = context.T("CloseDialogQuestion");
        BodyTextBlock.Text = context.IsRunning ? context.T("CloseDialogBodyRunning") : context.T("CloseDialogBodyPaused");
        TrayButton.Content = context.T("CloseDialogTray");
        ExitButton.Content = context.T("CloseDialogExit");
        CancelButtonElement.Content = context.T("CloseDialogCancel");
        Icon = BrandAssetInterop.CreateBitmapSource(BrandAssets.AppIcon);
    }

    private void TrayButton_Click(object sender, RoutedEventArgs e)
    {
        SelectedAction = CloseChoiceAction.MinimizeToTray;
        DialogResult = true;
    }

    private void ExitButton_Click(object sender, RoutedEventArgs e)
    {
        SelectedAction = CloseChoiceAction.StopAndExit;
        DialogResult = true;
    }

    private void CancelButtonElement_Click(object sender, RoutedEventArgs e)
    {
        SelectedAction = CloseChoiceAction.Cancel;
        DialogResult = false;
    }
}
