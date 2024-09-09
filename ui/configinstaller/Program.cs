using System.Management.Automation;
using System.Reflection;
using Terminal.Gui;
public static class ConfigInstaller {
    public static int Main(string[] args)
    {
        // See https://aka.ms/new-console-template for more information
        Terminal.Gui.Application.Init();
        if (args.Length == 0)
        {
            MessageBox.Query("Association", "Associating files with myself, an argument is necessary otherwise.", "Ok");
            return 1;
        }
        return 0;
        
    }
}