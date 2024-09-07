// See https://aka.ms/new-console-template for more information
using Terminal.Gui;
using Tomlyn.Model;
using Tomlyn;
using System.Text.RegularExpressions;

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

public static class Utils
{
    public static string MakeItLookLikeACdKey(string text)
    {
        char[] a = text.ToCharArray();
        string output = "";
        int i = 0;
        foreach (var character in a)
        {
            i++;
            output += character;
            if(i % 5 == 0)
            {
                output += "-";
            }
        }
        return output;
    }
    public static string MakeItNormalBase32(string text)
    {
        return text.ToUpperInvariant().Replace("-", String.Empty);
    }
}
