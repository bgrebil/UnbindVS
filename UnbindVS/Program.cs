using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace VisualStudio.Unbind
{
   class Program
   {
      static void Main(string[] args)
      {
         if (args.Length < 1) {
            Console.WriteLine("Error: No directory specified!");
            return;
         }

         var dir = Path.GetFullPath(args[0].Trim());
         if (!Directory.Exists(dir)) {
            Console.WriteLine("Error: Directory does not exist!");
            return;
         }

         var solutionFiles = new List<string>();
         var projectFiles = new List<string>();
         var filesToDelete = new List<string>();

         foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)) {
            if (IsSolutionFile(file)) {
               solutionFiles.Add(file);
            }
            else if (IsProjectFile(file)) {
               projectFiles.Add(file);
            }
            else if (FileToDelete(file)) {
               filesToDelete.Add(file);
            }
         }

         if ((solutionFiles.Count + projectFiles.Count + filesToDelete.Count) == 0) {
            Console.WriteLine("No files to modify or delete.");
            return;
         }

         solutionFiles.ForEach(ModifySolution);
         projectFiles.ForEach(ModifyProject);
         filesToDelete.ForEach(f => {
            File.SetAttributes(f, FileAttributes.Normal);
            File.Delete(f);
         });
      }

      static bool IsProjectFile(string filename)
      {
         if (filename.EndsWith(".vdproj", StringComparison.OrdinalIgnoreCase)) {
            return false;
         }

         if (filename.Contains('.')) {
            if (filename.EndsWith("proj", StringComparison.OrdinalIgnoreCase)) {
               return true;
            }
         }

         return false;
      }

      static bool IsSolutionFile(string filename)
      {
         return filename.EndsWith(".sln", StringComparison.OrdinalIgnoreCase);
      }

      static bool FileToDelete(string filename)
      {
         if (filename.EndsWith(".vssscc", StringComparison.OrdinalIgnoreCase)) return true;
         if (filename.EndsWith(".vspscc", StringComparison.OrdinalIgnoreCase)) return true;

         return false;
      }

      private static readonly IEnumerable<ILineComparer> _sectionChecks = new List<ILineComparer> {
         new LineComparer(), new RegexComparer()
      };

      static void ModifySolution(string solution)
      {
         Console.WriteLine("Modifying solution: {0}", solution);


         var output = new List<string>();

         bool inSccSection = false;
         foreach (var line in File.ReadAllLines(solution)) {
            var cleanLine = Uri.EscapeDataString(line.Trim());

            if (_sectionChecks.Any(t => t.IsMatch(line))) { // determine start of section
               inSccSection = true;
            }
            else if (inSccSection && cleanLine.StartsWith("EndGlobalSection")) {
               inSccSection = false;
            }
            else if (!inSccSection && !cleanLine.StartsWith("Scc")) {
               output.Add(line);
            }
         }

         File.SetAttributes(solution, FileAttributes.Normal);
         File.WriteAllLines(solution, output);
      }

      static void ModifyProject(string project)
      {
         Console.WriteLine("Modifying project: {0}", project);

         var xdoc = XDocument.Load(project);
         RemoveSccElementsAndAttributes(xdoc.Root);

         File.SetAttributes(project, FileAttributes.Normal);
         xdoc.Save(project);
      }

      static void RemoveSccElementsAndAttributes(XElement el)
      {
         el.Elements().Where(x => x.Name.LocalName.StartsWith("Scc")).Remove();
         el.Attributes().Where(x => x.Name.LocalName.StartsWith("Scc")).Remove();

         foreach (var child in el.Elements()) {
            RemoveSccElementsAndAttributes(child);
         }
      }
   }

   interface ILineComparer
   {
      bool IsMatch(string line);
   }

   class LineComparer : ILineComparer
   {
      public bool IsMatch(string line)
      {
         return line.StartsWith("GlobalSection(SourceCodeControl)") || 
                line.StartsWith("GlobalSection(TeamFoundationVersionControl)");
      }
   }

   class RegexComparer : ILineComparer
   {
      public bool IsMatch(string line)
      {
         return Regex.IsMatch(line, "GlobalSection\\(.*Version.*Control", RegexOptions.IgnoreCase);
      }
   }


}
