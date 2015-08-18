using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;
using System.Xml.Linq;

namespace NuspecGenerator
{
  class Program
  {
    class Library
    {
      public string LibDLL { get; set; }
      public string LibName { get; set; }
      public XDocument NuspecDoc { get; set; }
      public string[] Dependencies { get; set; }
    }

    static void Main(string[] args)
    {
      var template = File.ReadAllText("template.nuspec");

      var libs = new Dictionary<string, Library>();

      foreach (var file in Directory.GetFiles(".", "*.dll"))
      {
        var libdll = Path.GetFileNameWithoutExtension(file);

        if (libdll.StartsWith("IronScheme"))
        {
          continue;
        }

        var parts = libdll.Split('.');

        for (int i = 0; i < parts.Length; i++)
        {
          if (char.IsDigit(parts[i][0]))
          {
            parts[i] = ":" + parts[i];
          }
        }

        var libname = "(" + string.Join(" ", parts) + ")";
        var content = Regex.Replace(template, Regex.Escape("{library-name}"), libname);
        content = Regex.Replace(content, Regex.Escape("{library-dll}"), libdll);

        var lib = new Library
          {
            LibDLL = libdll,
            LibName = libname,
            NuspecDoc = XDocument.Parse(content)
          };

        libs.Add(libdll, lib);

        var depproc = new Process
          {
            StartInfo = new ProcessStartInfo
              {
                FileName = "IronScheme.Console32-v2.exe",
                Arguments = "--show-loaded-libraries",
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
              },
          };

        Console.WriteLine("Getting dependencies for: {0}", libname);
        depproc.Start();

        depproc.StandardInput.WriteLine("(import {0})", libname);
        depproc.StandardInput.Close();

        depproc.WaitForExit();

        var output = depproc.StandardOutput.ReadToEnd();

        var imports = output.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

        lib.Dependencies = imports.Skip(1).Select(Path.GetFileNameWithoutExtension).ToArray();

        foreach (var dep in lib.Dependencies)
        {
          Console.WriteLine(" - {0}", dep); 
        }

        //File.WriteAllText(libdll + ".nuspec", content);
      }

      foreach (var lib in libs.Values)
      {
        foreach (var dep in lib.Dependencies)
        {
          lib.Dependencies = lib.Dependencies.Except(libs[dep].Dependencies).ToArray();
        }
      }

      foreach (var lib in libs.Values)
      {
        var name = XName.Get("dependencies", "http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd");

        var depsec = lib.NuspecDoc.Descendants(name).Single();
        foreach (var dep in lib.Dependencies)
        {
          // the namespace seems to be added in .NET 4.6 regardless if it is target namespace or not
          depsec.Add(XElement.Parse(string.Format("<dependency xmlns=\"http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd\" id=\"{0}\" version=\"[0.9.$TFSREV$]\" />", libs[dep].LibDLL)));
        }

        lib.NuspecDoc.Save(lib.LibDLL + ".nuspec");
      }


    }
  }
}

