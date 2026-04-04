#nullable enable
using System.ComponentModel;

namespace MouseTool;

internal sealed partial class MainForm
{
    private IContainer? components;

    private void InitializeComponent()
    {
        components = new Container();
        AutoScaleMode = AutoScaleMode.Font;
        Name = nameof(MainForm);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            components?.Dispose();
        }

        base.Dispose(disposing);
    }
}
