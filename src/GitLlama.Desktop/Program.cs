using Avalonia;
namespace GitLlama.Desktop;
internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Length >= 2 && args[0] == "--gitllama-ssh")
        {
            try { Environment.ExitCode = GitLlama.Git.ManagedSshBridge.RunHelperAsync(args[1], args[2..], Console.OpenStandardInput(), Console.OpenStandardOutput(), Console.OpenStandardError(), CancellationToken.None).GetAwaiter().GetResult(); }
            catch { Console.Error.WriteLine("GitLlama SSH helper could not connect to its parent session."); Environment.ExitCode = 1; }
            return;
        }
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace();
}
