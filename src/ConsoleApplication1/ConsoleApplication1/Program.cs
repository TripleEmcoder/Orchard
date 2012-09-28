using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.BuildEngine;
using Microsoft.Build.Evaluation;
using NuGet;
using Project = Microsoft.Build.Evaluation.Project;

namespace ConsoleApplication1
{
    class Program
    {
        static void Main(string[] args)
        {
            var cacheDirectory = Path.Combine(Directory.GetCurrentDirectory(), "cache");
            var outputDirectory = Path.Combine(Directory.GetCurrentDirectory(), "output");
            var inputDirectory = Path.Combine(Directory.GetCurrentDirectory(), @"E:\Marcin\Documents\Praca\Projekty\Orchard\pkg");

            var cacheFileSystem = new PhysicalFileSystem(cacheDirectory);
            var cachePackageResolver = new DefaultPackagePathResolver(cacheFileSystem, false);

            var inputRepository = new LocalPackageRepository(inputDirectory);
            var inputManager = new PackageManager(inputRepository, cachePackageResolver, cacheFileSystem);

            var references = new[] { "Orchard.Core", "Orchard.Framework", "Orchard.External" };

            foreach (var reference in references)
                inputManager.InstallPackage(inputRepository.FindPackage(reference), false, false);

            foreach (var inputPackage in inputRepository.GetPackages().Where(p => p.Id.StartsWith("Orchard.Module.")))
            {
                Do(inputRepository, inputManager, inputPackage, cacheDirectory, references, outputDirectory);
            }
        }

        private static void Do(LocalPackageRepository inputRepository, PackageManager inputManager, IPackage inputPackage, string cacheDirectory, IEnumerable<string> references, string outputDirectory)
        {
            var solution = new ProjectCollection();
            var logger = new ConsoleLogger();

            inputManager.InstallPackage(inputPackage, false, false);
            var packageDirectory = Path.Combine(cacheDirectory, inputPackage.Id);

            var moduleDirectory = Directory.GetDirectories(Path.Combine(packageDirectory, "Content", "Modules"))[0];
            var moduleName = Path.GetFileName(moduleDirectory);
            var project = solution.LoadProject(Path.Combine(moduleDirectory, moduleName + ".csproj"));

            foreach (var reference in references)
            {
                var item = project.Items.SingleOrDefault(i => i.ItemType == "ProjectReference" && i.GetMetadataValue("Name") == reference);

                if (item == null)
                    continue;

                ReplaceReference(project, item, reference, @"..\..\..\..\" + reference + @"\lib\net");
            }

            foreach (var item in project.Items.Where(i => i.ItemType == "Reference").ToList())
            {
                if (item.GetMetadataValue("HintPath").StartsWith(@"..\..\..\..\lib"))
                {
                    ReplaceReference(project, item, Path.GetFileNameWithoutExtension(item.GetMetadataValue("HintPath")), @"..\..\..\..\Orchard.External\lib\net");
                }
                else if (item.EvaluatedInclude.StartsWith("ClaySharp,"))
                {
                    ReplaceReference(project, item, "ClaySharp", @"..\..\..\..\Orchard.External\lib\net");
                }
            }

            foreach (var item in project.Items.Where(i => i.ItemType == "ProjectReference").ToList())
            {
                if (item.EvaluatedInclude.StartsWith(@"..\") && !item.EvaluatedInclude.StartsWith(@"..\..\"))
                {
                    Console.WriteLine(moduleName + " -> " + item.EvaluatedInclude);
                    var referencedModuleName = item.GetMetadataValue("Name");
                    var referencedPackageId = "Orchard.Module." + item.GetMetadataValue("Name");

                    Do(inputRepository, inputManager, inputRepository.FindPackage(referencedPackageId), cacheDirectory, references, outputDirectory);
                    ReplaceReference(project, item, referencedModuleName, @"..\..\..\..\" + referencedPackageId + @"\Content\Modules\" + referencedModuleName);
                }
            }

            if (!project.Build(logger))
                throw new Exception("Failed to build");

            var rules = new[]
                {
                    @"bin\" + moduleName + ".dll",
                    @"Module.txt",
                    @"Placement.info",
                    @"Web.config",
                    @"Content\**",
                    @"Scripts\**",
                    @"Styles\**",
                    @"Views\**"
                };

            var manifest = Manifest.Create(inputPackage);
            manifest.Files = rules.Select(r => new ManifestFile { Source = @"Content\Modules\" + moduleName + @"\**\" + r, Target = @"Content\Modules\" + moduleName }).ToList();

            var b = new PackageBuilder();
            b.Populate(manifest.Metadata);
            b.PopulateFiles(packageDirectory, manifest.Files);

            Directory.CreateDirectory(outputDirectory);
            using (var outputPackageFile = File.Create(Path.Combine(outputDirectory, manifest.Metadata.Id + "." + manifest.Metadata.Version + ".nupkg")))
                b.Save(outputPackageFile);
        }

        private static void ReplaceReference(Project project, ProjectItem item, string reference, string path)
        {
            project.RemoveItem(item);
            project.AddItem("Reference", reference, new[] { new KeyValuePair<string, string>("HintPath", Path.Combine(path, reference + ".dll")) });
        }

        private static void Latest()
        {
            var orchard = new DataServicePackageRepository(new Uri("http://packages.orchardproject.net/FeedService.svc"));
            var nuget = new DataServicePackageRepository(new Uri("https://nuget.org/api/v2"));
            var symbolsource =
                new DataServicePackageRepository(new Uri("http://nuget.gw.symbolsource.org/Public/Orchard/FeedService.mvc"));
            var cache = Directory.GetCurrentDirectory();


            var solution = new ProjectCollection();
            var logger = new ConsoleLogger();

            foreach (var d in Directory.EnumerateDirectories(Path.Combine(cache, "aa", "Content", "Modules")))
            {
                var project = solution.LoadProject(Path.Combine(d, Path.GetFileName(d) + ".csproj"));
                project.RemoveItem(
                    project.Items.Where(i => i.ItemType == "ProjectReference" && i.GetMetadataValue("Name") == "Orchard.Core").
                        Single());
                project.RemoveItem(
                    project.Items.Where(
                        i => i.ItemType == "ProjectReference" && i.GetMetadataValue("Name") == "Orchard.Framework").Single());
                project.AddItem("Reference", "Orchard.Core",
                                new[]
                                    {
                                        new KeyValuePair<string, string>("HintPath",
                                                                         @"C:\Users\marcin.mikolajczak\Downloads\Temp\Orchard.Core.dll")
                                    });
                project.AddItem("Reference", "Orchard.Framework",
                                new[]
                                    {
                                        new KeyValuePair<string, string>("HintPath",
                                                                         @"C:\Users\marcin.mikolajczak\Downloads\Temp\Orchard.Framework.dll")
                                    });
            }

            foreach (var project in solution.LoadedProjects)
                project.Build(logger);
        }
    }
}
