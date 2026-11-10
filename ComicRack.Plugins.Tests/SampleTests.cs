using System;
using System.IO;
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

	[Test]
	public async Task Compiled_sample_registers_its_hooks_with_the_embedded_image_and_its_own_configure_action()
	{
		using var scripts = new ScriptsFolder();
		Install(scripts, "SamplePlugin");

		var loaded = scripts.Load();

		await Assert.That(loaded.Hooks("SamplePlugin")).IsEquivalentTo(new[] { PluginEngine.ScriptTypeLibrary, PluginEngine.ScriptTypeBooks, PluginEngine.ScriptTypeComicInfoHtml });
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
