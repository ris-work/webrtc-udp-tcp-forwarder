// See https://aka.ms/new-console-template for more information
using Terminal.Gui;
using Tomlyn.Model;
using Tomlyn;

if (args.Length > 0)
{
    StartConfig.Filename = args[0];
}
Application.Run<RV.WebRTCForwarders.Window>().Dispose();


public partial class IceServers
{
    public string[] URLs;
    public string Username;
    public string Credential;
}


public static class StartConfig
{
    public static string Filename = "sample.toml";
}

