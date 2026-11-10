using System;
using System.Threading.Tasks;
using cYo.Projects.ComicRack.Engine;
using cYo.Projects.ComicRack.Plugins;

namespace ComicRack.Plugins.Tests;

public class DependencyTests
{
	private static readonly object[] NoBooks = { new ComicBook[0] };

	private static string Dependency(string text) => "namespace Dep { public static class Info { public static string Text() { return \"" + text + "\"; } } }";

	private static string PluginUsingDependency(string key) => Source.Plugin(key, "IComicInfoHtml", "public string Execute(ComicBook[] books) { return Dep.Info.Text(); }");

	private static object Run(LoadedPlugins loaded, string key) => loaded.Command(key, PluginEngine.ScriptTypeComicInfoHtml).Invoke(NoBooks);

	[Test]
	public async Task Dependency_next_to_the_plugin_dll_resolves()
	{
		using var scripts = new ScriptsFolder();
		string dependency = scripts.Compile(@"Scripts\Mine", Dependency("beside"));
		scripts.Compile(@"Scripts\Mine", PluginUsingDependency("Beside"), dependency);

		await Assert.That(Run(scripts.Load(), "Beside")).IsEqualTo("beside");
	}

	[Test]
	public async Task Dependency_in_the_lib_folder_resolves_when_the_plugin_first_needs_it_and_the_console_names_the_file()
	{
		using var scripts = new ScriptsFolder();
		string dependency = scripts.Compile(@"Scripts\Mine\lib", Dependency("from lib"));
		scripts.Compile(@"Scripts\Mine", PluginUsingDependency("InLib"), dependency);
		var loaded = scripts.Load();
		bool loadedByTheScan = ScriptsFolder.IsExecutionLoaded(dependency);

		object text = Run(loaded, "InLib");

		await Assert.That(loadedByTheScan).IsFalse();
		await Assert.That(text).IsEqualTo("from lib");
		await Assert.That(loaded.ConsoleLinesAboutThisFixture).Contains("to '" + dependency + "'");
	}

	[Test]
	public async Task Two_plugins_with_different_versions_of_a_strong_named_dependency_each_get_their_own()
	{
		using var scripts = new ScriptsFolder();
		string name = "Dep" + Guid.NewGuid().ToString("N");
		string key = scripts.CreateStrongNameKey();
		string one = scripts.CompileTo($@"Scripts\A\lib\{name}.dll", name, Dependency("one"), version: "1.0.0.0", strongNameKeyFile: key);
		string two = scripts.CompileTo($@"Scripts\B\lib\{name}.dll", name, Dependency("two"), version: "2.0.0.0", strongNameKeyFile: key);
		scripts.Compile(@"Scripts\A", PluginUsingDependency("UsesOne"), one);
		scripts.Compile(@"Scripts\B", PluginUsingDependency("UsesTwo"), two);
		var loaded = scripts.Load();

		await Assert.That(Run(loaded, "UsesOne")).IsEqualTo("one");
		await Assert.That(Run(loaded, "UsesTwo")).IsEqualTo("two");
		await Assert.That(loaded.ConsoleLinesAboutThisFixture).DoesNotContain("Warning");
	}

	[Test]
	public async Task Two_plugins_with_different_versions_of_a_dependency_without_strong_name_share_the_first_one_and_the_console_warns()
	{
		using var scripts = new ScriptsFolder();
		string name = "Dep" + Guid.NewGuid().ToString("N");
		string one = scripts.CompileTo($@"Scripts\A\lib\{name}.dll", name, Dependency("one"), version: "1.0.0.0");
		string two = scripts.CompileTo($@"Scripts\B\lib\{name}.dll", name, Dependency("two"), version: "2.0.0.0");
		scripts.Compile(@"Scripts\A", PluginUsingDependency("UsesOne"), one);
		scripts.Compile(@"Scripts\B", PluginUsingDependency("UsesTwo"), two);
		var loaded = scripts.Load();

		await Assert.That(Run(loaded, "UsesOne")).IsEqualTo("one");
		await Assert.That(Run(loaded, "UsesTwo")).IsEqualTo("one");
		await Assert.That(loaded.ConsoleLinesAboutThisFixture).Contains("Warning: plugins ship different versions").And.Contains("'" + one + "' (version 1.0.0.0)").And.Contains("'" + two + "' (version 2.0.0.0)");
	}

	[Test]
	public async Task Plugin_that_ships_another_version_than_it_was_built_against_gets_the_shipped_one_with_a_warning()
	{
		using var scripts = new ScriptsFolder();
		string name = "Dep" + Guid.NewGuid().ToString("N");
		string builtAgainst = scripts.CompileTo($@"Elsewhere\{name}.dll", name, Dependency("old"), version: "1.0.0.0");
		scripts.CompileTo($@"Scripts\Mine\lib\{name}.dll", name, Dependency("new"), version: "1.5.0.0");
		scripts.Compile(@"Scripts\Mine", PluginUsingDependency("Drifted"), builtAgainst);
		var loaded = scripts.Load();

		await Assert.That(Run(loaded, "Drifted")).IsEqualTo("new");
		await Assert.That(loaded.ConsoleLinesAboutThisFixture).Contains("version 1.0.0.0 was requested, version 1.5.0.0 was found");
	}

	[Test]
	public async Task Two_plugins_that_ship_the_same_name_and_version_share_the_copy_loaded_first()
	{
		using var scripts = new ScriptsFolder();
		string name = "Dep" + Guid.NewGuid().ToString("N");
		string first = scripts.CompileTo($@"Scripts\A\lib\{name}.dll", name, Dependency("first copy"), version: "1.0.0.0");
		string second = scripts.CompileTo($@"Scripts\B\lib\{name}.dll", name, Dependency("second copy"), version: "1.0.0.0");
		scripts.Compile(@"Scripts\A", PluginUsingDependency("First"), first);
		scripts.Compile(@"Scripts\B", PluginUsingDependency("Second"), second);
		var loaded = scripts.Load();

		await Assert.That(Run(loaded, "First")).IsEqualTo("first copy");
		await Assert.That(Run(loaded, "Second")).IsEqualTo("first copy");
		await Assert.That(ScriptsFolder.IsExecutionLoaded(second)).IsFalse();
	}
}
