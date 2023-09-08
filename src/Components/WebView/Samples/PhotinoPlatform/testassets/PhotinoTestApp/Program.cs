// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net.Http;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebView.Photino;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;

namespace PhotinoTestApp;

public static class PhotinoMarkerType { }

class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        var isTestMode = args.Contains("testmode", StringComparer.Ordinal);
        Console.WriteLine($"Running in test mode? {isTestMode}");

        if (isTestMode)
        {
            Console.WriteLine($"Current directory: {Environment.CurrentDirectory}");
            Console.WriteLine($"Current assembly: {typeof(Program).Assembly.Location}");
            var thisProgramDir = Path.GetDirectoryName(typeof(Program).Assembly.Location);

            Console.WriteLine($"Old PATH: {Environment.GetEnvironmentVariable("PATH")}");
            Environment.SetEnvironmentVariable("PATH", Path.Combine(thisProgramDir, "runtimes", "win-x64", "native") + ";" + Environment.GetEnvironmentVariable("PATH"));
            Console.WriteLine($"New PATH: {Environment.GetEnvironmentVariable("PATH")}");

            var thisAppFiles = Directory.GetFiles(thisProgramDir, "*", SearchOption.AllDirectories).ToArray();
            Console.WriteLine($"Found {thisAppFiles.Length} files in this app:");
            foreach (var file in thisAppFiles)
            {
                Console.WriteLine($"\t{file}");
            }
        }

        var hostPage = isTestMode ? "wwwroot/webviewtesthost.html" : "wwwroot/webviewhost.html";

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddBlazorWebView();
        serviceCollection.AddSingleton<HttpClient>();

        Console.WriteLine($"Creating BlazorWindow...");
        BlazorWindow mainWindow = null;
        try
        {
            var contentRootDir = Path.GetDirectoryName(Path.GetFullPath(hostPage))!;
            Console.WriteLine($"contentRootDir = {contentRootDir}, exists = {Directory.Exists(contentRootDir)}, rooted = {Path.IsPathRooted(contentRootDir)}");
            var hostPageRelativePath = Path.GetRelativePath(contentRootDir, hostPage);
            Console.WriteLine($"hostPageRelativePath = {hostPageRelativePath}, exists = {File.Exists(Path.Combine(contentRootDir, hostPageRelativePath))}");
            var fileProvider = new PhysicalFileProvider(contentRootDir);

            string fullRoot = Path.GetFullPath(contentRootDir);
            // When we do matches in GetFullPath, we want to only match full directory names.
            var root2 = EnsureTrailingSlash(fullRoot);
            if (!Directory.Exists(root2))
            {
                throw new DirectoryNotFoundException(root2);
            }

            mainWindow = new BlazorWindow(
                title: "Hello, world!",
                hostPage: hostPage,
                services: serviceCollection.BuildServiceProvider(),
                pathBase: "/subdir"); // The content in BasicTestApp assumes this
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception {ex.GetType().FullName} while creating window: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }

        Console.WriteLine($"Hooking exception handler...");
        AppDomain.CurrentDomain.UnhandledException += (sender, error) =>
        {
            Console.Write(
                "Fatal exception" + Environment.NewLine +
                error.ExceptionObject.ToString() + Environment.NewLine);
        };

        Console.WriteLine($"Setting up root components...");
        if (isTestMode)
        {
            mainWindow.RootComponents.Add<Pages.TestPage>("root");
        }
        else
        {
            mainWindow.RootComponents.Add<BasicTestApp.Index>("root");
            mainWindow.RootComponents.RegisterForJavaScript<BasicTestApp.DynamicallyAddedRootComponent>("my-dynamic-root-component");
            mainWindow.RootComponents.RegisterForJavaScript<BasicTestApp.JavaScriptRootComponentParameterTypes>(
                "component-with-many-parameters",
                javaScriptInitializer: "myJsRootComponentInitializers.testInitializer");
        }

        Console.WriteLine($"Running window...");

        try
        {
            mainWindow.Run(isTestMode);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception while running window: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }

    internal static string EnsureTrailingSlash(string path)
    {
        if (!string.IsNullOrEmpty(path) &&
            path[path.Length - 1] != Path.DirectorySeparatorChar)
        {
            return path + Path.DirectorySeparatorChar;
        }

        return path;
    }
}
