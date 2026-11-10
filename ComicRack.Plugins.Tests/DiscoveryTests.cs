using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using cYo.Projects.ComicRack.Plugins;

namespace ComicRack.Plugins.Tests;

public class DiscoveryTests
{
	[Test]
	public async Task Plugin_class_yields_one_command_per_implemented_hook_interface()
	{
		using var scripts = new ScriptsFolder();
		scripts.Compile(@"Scripts\Mine", Source.Plugin("Multi", "IBookOpened, IShutdown, ISearch", """
			public void Execute(ComicBook book) { }
			public bool Execute(bool userIsClosing) { return true; }
			public Dictionary<string, string> Execute(string hint, string text, int limit) { return null; }
			"""));

		var loaded = scripts.Load();

		await Assert.That(loaded.Hooks("Multi")).IsEquivalentTo(new[] { PluginEngine.ScriptTypeBookOpened, PluginEngine.ScriptTypeShutdown, PluginEngine.ScriptTypeSearch });
	}

	[Test]
	public async Task Command_metadata_comes_from_the_plugin_attribute()
	{
		using var scripts = new ScriptsFolder();
		scripts.Compile(@"Scripts\Mine", Source.Plugin("Described", "ICreateBookList",
			"public IEnumerable<ComicBook> Execute(ComicBook[] books, string a, string b) { return books; }",
			attribute: "Name = \"Shown name\", Description = \"What it does\", Image = \"Icon.png\", Enabled = false, PCount = 2"));

		Command command = scripts.Load().Command("Described", PluginEngine.ScriptTypeCreateBookList);

		await Assert.That(command.Name).IsEqualTo("Shown name");
		await Assert.That(command.Description).IsEqualTo("What it does");
		await Assert.That(command.Image).IsEqualTo("Icon.png");
		await Assert.That(command.Enabled).IsFalse();
		await Assert.That(command.PCount).IsEqualTo(2);
	}

	[Test]
	public async Task Name_defaults_to_the_key()
	{
		using var scripts = new ScriptsFolder();
		scripts.Compile(@"Scripts\Mine", Source.Plugin("OnlyKey", "IStartup", "public void Execute() { }"));

		Command command = scripts.Load().Command("OnlyKey", PluginEngine.ScriptTypeStartup);

		await Assert.That(command.Name).IsEqualTo("OnlyKey");
		await Assert.That(command.Enabled).IsTrue();
	}

	[Test]
	public async Task Hook_return_value_flows_back_to_the_caller()
	{
		using var scripts = new ScriptsFolder();
		scripts.Compile(@"Scripts\Mine", Source.Plugin("Quit", "IShutdown", "public bool Execute(bool userIsClosing) { return !userIsClosing; }"));

		Command quit = scripts.Load().Command("Quit", PluginEngine.ScriptTypeShutdown);

		await Assert.That((bool)quit.Invoke(new object[] { true })).IsFalse();
		await Assert.That((bool)quit.Invoke(new object[] { false })).IsTrue();
	}

	[Test]
	public async Task Config_is_saved_next_to_the_plugin_and_read_back()
	{
		using var scripts = new ScriptsFolder();
		string dll = scripts.Compile(@"Scripts\Mine", Source.Plugin("Cfg", "IQuickOpenHtml", "public string Execute(ComicBook[] books) { return null; }"));
		Command command = scripts.Load().Command("Cfg", PluginEngine.ScriptTypeQuickOpenHtml);

		bool saved = command.SaveConfig("remember me");

		await Assert.That(saved).IsTrue();
		await Assert.That(command.LoadConfig()).IsEqualTo("remember me");
		await Assert.That(Directory.GetFiles(Path.GetDirectoryName(dll), "*.config").Length).IsEqualTo(1);
	}

	[Test]
	public async Task Assembly_without_plugin_classes_yields_no_commands()
	{
		using var scripts = new ScriptsFolder();
		scripts.Compile(@"Scripts\Mine", "public class JustALibrary { public int Answer() { return 42; } }");

		var loaded = scripts.Load();

		await Assert.That(loaded.Engine.GetAllCommands().Count()).IsEqualTo(0);
	}
}
