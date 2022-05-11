using System;
using System.Linq;
using System.Reflection;

namespace KuchiPaku.Models;

public class AppUtil
{
    public static string GetWindowTitle(){
        var assembly = Assembly.GetExecutingAssembly().GetName();

        var versionInfo = Assembly
            .GetExecutingAssembly()
            .GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute))
            .Cast<AssemblyInformationalVersionAttribute>()
            .FirstOrDefault();

        return $"{assembly.Name} ver. {versionInfo.InformationalVersion}";
    }
}

