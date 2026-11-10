using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using cYo.Projects.ComicRack.Engine;
using cYo.Projects.ComicRack.Plugins;

namespace ComicRack.Plugins.Tests;

public class SampleTests
{
	private static readonly string builtSamples = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Samples");

	private static void Install(ScriptsFolder scripts, string sample)
	{
		string source = Path.Combine(builtSamples, sample);
		foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
		{
			File.Copy(file, scripts.PathTo(Path.Combine("Scripts", sample, file.Substring(source.Length + 1))));
		}
	}

	// The other tests run with the assemblies and the binding redirects of the test project.
	// This one runs in the folder that ships, so a missing DLL or binding redirect of ComicRack.exe fails here.
	[Test]
	public async Task Both_samples_load_and_run_in_the_built_ComicRack_folder_with_the_config_of_ComicRack_exe()
	{
		string comicRackFolder = typeof(SampleTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Single(a => a.Key == "ComicRackFolder").Value;
		using var scripts = new ScriptsFolder();
		Install(scripts, "SamplePlugin");
		Install(scripts, "SampleSourcePlugin");

		IsolatedScan.Result result = IsolatedScan.Run(scripts, comicRackFolder, Path.Combine(comicRackFolder, "ComicRack.exe.config"), comicInfoHtmlOf: "SampleSourcePlugin");

		await Assert.That(result.Keys).IsEquivalentTo(new[] { "SamplePlugin", "SampleSourcePlugin" });
		await Assert.That(result.Html).Contains("<li>the-walking-dead");
		await Assert.That(result.Console).Contains("Compiling source plugin").And.Contains(@"SampleSourcePlugin\lib\SampleLibrary.dll'").And.DoesNotContain("Failed");
	}

	[Test]
	public async Task Compiled_sample_registers_its_hooks_with_the_embedded_image_and_its_own_configure_action()
	{
		using var scripts = new ScriptsFolder();
		Install(scripts, "SamplePlugin");

		var loaded = scripts.Load();

		await Assert.That(loaded.Hooks("SamplePlugin")).IsEquivalentTo(new[] { PluginEngine.ScriptTypeLibrary, PluginEngine.ScriptTypeBooks, PluginEngine.ScriptTypeComicInfoHtml, PluginEngine.ScriptTypeStartup });
		Command books = loaded.Command("SamplePlugin", PluginEngine.ScriptTypeBooks);
		await Assert.That(books.CommandImage).IsNotNull();
		await Assert.That(books.Configure?.Hook).IsEqualTo(PluginEngine.ScriptTypeConfig);
		object html = loaded.Command("SamplePlugin", PluginEngine.ScriptTypeComicInfoHtml).Invoke(new object[] { new[] { new ComicBook { Series = "Saga", Number = "1" } } });
		await Assert.That((string)html).Contains("Saga");
	}

	[Test]
	public async Task Source_sample_compiles_with_its_library_and_runs()
	{
		using var scripts = new ScriptsFolder();
		Install(scripts, "SampleSourcePlugin");

		var loaded = scripts.Load();

		await Assert.That(loaded.Hooks("SampleSourcePlugin")).IsEquivalentTo(new[] { PluginEngine.ScriptTypeStartup, PluginEngine.ScriptTypeComicInfoHtml });
		object html = loaded.Command("SampleSourcePlugin", PluginEngine.ScriptTypeComicInfoHtml).Invoke(new object[] { new[] { new ComicBook { Series = "The Walking Dead", Number = "1" } } });
		await Assert.That((string)html).Contains("<li>the-walking-dead");
	}
}
