// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net.Http;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebView.Photino;
using Microsoft.Extensions.DependencyInjection;

namespace PhotinoTestApp;

public static class PhotinoMarkerType { }

class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        var isTestMode = args.Contains("testmode", StringComparer.Ordinal);
        Console.WriteLine($"Running in test mode? {isTestMode}");

        var hostPage = isTestMode ? "wwwroot/webviewtesthost.html" : "wwwroot/webviewhost.html";

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddBlazorWebView();
        serviceCollection.AddSingleton<HttpClient>();

        Console.WriteLine($"Creating BlazorWindow...");
        BlazorWindow mainWindow = null;
        try
        {
            mainWindow = new BlazorWindow(
                title: "Hello, world!",
                hostPage: hostPage,
                services: serviceCollection.BuildServiceProvider(),
                pathBase: "/subdir"); // The content in BasicTestApp assumes this
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception while creating window: {ex.Message}");
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
}
