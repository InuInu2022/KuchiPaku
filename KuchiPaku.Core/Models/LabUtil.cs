using System;
using System.IO;
using System.Threading.Tasks;

namespace KuchiPaku.Models;

public static class LabUtil
{
    public static async ValueTask<Lab> MakeLabAsync(FileInfo file, int fps = 30){
        var str = await Task.Run(() => File.ReadAllText(
            file!.FullName,
            System.Text.Encoding.UTF8
        ));

        var lab = new Lab(str, fps);

        return lab;
    }
}