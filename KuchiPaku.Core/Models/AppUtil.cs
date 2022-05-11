using System;
using System.Linq;
using System.Reflection;

namespace KuchiPaku.Models;

public class AppUtil
{
    public static string GetWindowTitle(){
        var assembly = Assembly.GetEntryAssembly().GetName();

        var versionInfo = Assembly
            .GetEntryAssembly()
            .GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute))
            .Cast<AssemblyInformationalVersionAttribute>()
            .FirstOrDefault();

        return $"{assembly.Name} ver. {versionInfo.InformationalVersion}";
    }
}

