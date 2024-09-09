using System.Management.Automation;
using System.Reflection;
public static class ConfigInstaller {
    public static int Main(string[] args)
    {
        // See https://aka.ms/new-console-template for more information
        var a = Assembly.GetExecutingAssembly();
        var stream = new StreamReader(a.GetManifestResourceStream("assoc.ps1")).ReadToEnd();
        Console.WriteLine(stream);

        Console.WriteLine("Hello, World!");
        return 0;
    }
}