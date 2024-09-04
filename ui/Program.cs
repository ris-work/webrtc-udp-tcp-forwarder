// See https://aka.ms/new-console-template for more information
using Terminal.Gui;
using Tomlyn.Model;
using Tomlyn;

if (args.Length > 0)
{
    StartConfig.Filename = args[0];
}
Application.Run<RV.WebRTCForwarders.Window>();





public static class StartConfig
{
    public static string Filename = "sample.toml";
}

public class Loader: Window
{
    public Loader() {
        ColorScheme = new ColorScheme{ Normal = new Terminal.Gui.Attribute(Color.DarkGray, Color.Cyan) };
        Title = $"Configuration Editor ({Application.QuitKey} to Quit)";
        string TomlFileContents = System.IO.File.ReadAllText(StartConfig.Filename);
        var model = Toml.ToModel(TomlFileContents);
        var FileNameLabel = new Label() { Text = StartConfig.Filename };
        var EndpointType = (string)model["Type"];
        var EndpointTypeLabel = new Label() { Text = EndpointType };
        Add(FileNameLabel, EndpointTypeLabel);
    }
}