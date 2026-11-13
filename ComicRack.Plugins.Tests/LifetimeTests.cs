using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using cYo.Projects.ComicRack.Engine;

namespace ComicRack.Plugins.Tests;

public class LifetimeTests
{
	private static readonly object[] NoBooks = { new ComicBook[0] };

	// A second plugin class in the fixture that reports a static of the first one, so a test can read it through a command.
	private static string Reporter(string key, string expression) =>
		"public class Reporter : DotNetPlugin, IComicInfoHtml {\n" + Source.PluginKey(key) + "\npublic string Execute(ComicBook[] books) { return " + expression + "; }\n}";

	[Test]
	public async Task Plugin_class_is_created_once_at_scan_time_and_shared_by_its_commands()
	{
		using var scripts = new ScriptsFolder();
		scripts.Compile(@"Scripts\Mine", Source.Types(
			"public class Plugin : DotNetPlugin, IComicInfoHtml, IQuickOpenHtml {\n" +
			"public static int Created;\n" +
			"private readonly string id;\n" +
			Source.PluginKey("Shared") + "\n" +
			"public Plugin() { Created++; id = \"instance \" + Created; }\n" +
			"string IComicInfoHtml.Execute(ComicBook[] books) { return id; }\n" +
			"string IQuickOpenHtml.Execute(ComicBook[] books) { return id; }\n" +
			"}\n" + Reporter("Created", "Plugin.Created.ToString()")));

		var loaded = scripts.Load();
		var info = loaded.Engine.GetCommands("ComicInfoHtml").Single(c => c.Key == "Shared");
		var quickOpen = loaded.Engine.GetCommands("QuickOpenHtml").Single(c => c.Key == "Shared");

		await Assert.That(loaded.Command("Created").Invoke(NoBooks)).IsEqualTo("1");
		await Assert.That(info.Invoke(NoBooks)).IsEqualTo("instance 1");
		await Assert.That(quickOpen.Invoke(NoBooks)).IsEqualTo("instance 1");
		await Assert.That(info.Invoke(NoBooks)).IsEqualTo("instance 1");
		await Assert.That(loaded.Command("Created").Invoke(NoBooks)).IsEqualTo("1");
	}

	[Test]
	public async Task OnLoad_sees_an_environment_whose_CommandPath_and_ScriptPath_are_the_plugin_folder()
	{
		using var scripts = new ScriptsFolder();
		Func<string, string> source = key => Source.Plugin(key, "IComicInfoHtml",
			"private string where;\n" +
			"protected override void OnLoad() { where = ComicRack.CommandPath + \"|\" + ScriptPath; }\n" +
			"public string Execute(ComicBook[] books) { return where; }");
		string one = Path.GetDirectoryName(scripts.Compile(@"Scripts\One", source("One")));
		string two = Path.GetDirectoryName(scripts.Compile(@"Scripts\Two", source("Two")));

		var loaded = scripts.Load();

		await Assert.That(loaded.Command("One").Invoke(NoBooks)).IsEqualTo(one + "|" + one);
		await Assert.That(loaded.Command("Two").Invoke(NoBooks)).IsEqualTo(two + "|" + two);
	}

	[Test]
	public async Task Plugin_whose_OnLoad_throws_registers_none_of_its_hooks_and_the_console_says_why()
	{
		using var scripts = new ScriptsFolder();
		scripts.Compile(@"Scripts\Mine", Source.Plugin("Fails", "IBooks, IEditor",
			"protected override void OnLoad() { throw new InvalidOperationException(\"load-boom\"); }\n" +
			"public void Execute(ComicBook[] books) { }"));

		var loaded = scripts.Load();

		await Assert.That(loaded.Keys).IsEmpty();
		await Assert.That(loaded.ConsoleLinesAboutThisFixture).Contains("OnLoad failed: load-boom");
	}

	[Test]
	public async Task Return_values_flow_back_to_the_caller()
	{
		using var scripts = new ScriptsFolder();
		scripts.Compile(@"Scripts\Mine", Source.Plugin("Quit", "IShutdown", "public bool Execute(bool userClosing) { return !userClosing; }"));

		var quit = scripts.Load().Command("Quit");

		await Assert.That((bool)quit.Invoke(new object[] { true })).IsFalse();
		await Assert.That((bool)quit.Invoke(new object[] { false })).IsTrue();
	}

	[Test]
	public async Task Plugin_exception_reaches_the_caller_unwrapped()
	{
		using var scripts = new ScriptsFolder();
		scripts.Compile(@"Scripts\Mine", Source.Plugin("Throws", "IBooks", "public void Execute(ComicBook[] books) { throw new InvalidOperationException(\"boom\"); }"));

		var throws = scripts.Load().Command("Throws");

		var thrown = Assert.ThrowsExactly<InvalidOperationException>(() => throws.Invoke(NoBooks));
		await Assert.That(thrown.Message).IsEqualTo("boom");
	}

	[Test]
	public async Task Invoke_with_catchErrors_returns_null_when_the_plugin_throws()
	{
		using var scripts = new ScriptsFolder();
		scripts.Compile(@"Scripts\Mine", Source.Plugin("Throws", "IComicInfoHtml", "public string Execute(ComicBook[] books) { throw new InvalidOperationException(\"boom\"); }"));

		var throws = scripts.Load().Command("Throws");

		await Assert.That(throws.Invoke(NoBooks, catchErrors: true)).IsNull();
	}

	[Test]
	public async Task Disposing_the_engine_disposes_a_plugin_instance_shared_by_two_commands_exactly_once()
	{
		using var scripts = new ScriptsFolder();
		scripts.Compile(@"Scripts\Mine", Source.Types(
			"public class Plugin : DotNetPlugin, IComicInfoHtml, IQuickOpenHtml, IDisposable {\n" +
			"public static int Created, Disposed;\n" +
			Source.PluginKey("Shared") + "\n" +
			"public Plugin() { Created++; }\n" +
			"public void Dispose() { Disposed++; }\n" +
			"string IComicInfoHtml.Execute(ComicBook[] books) { return \"\"; }\n" +
			"string IQuickOpenHtml.Execute(ComicBook[] books) { return \"\"; }\n" +
			"}\n" + Reporter("Count", "Plugin.Created + \"/\" + Plugin.Disposed")));
		var loaded = scripts.Load();

		loaded.Engine.Dispose();

		await Assert.That(loaded.Command("Count").Invoke(NoBooks)).IsEqualTo("1/1");
	}

	[Test]
	public async Task Plugin_whose_Dispose_throws_is_logged_and_does_not_stop_other_plugins_being_disposed()
	{
		using var scripts = new ScriptsFolder();
		scripts.Compile(@"Scripts\Mine", Source.Types(
			"public class Thrower : DotNetPlugin, IComicInfoHtml, IDisposable {\n" + Source.PluginKey("Thrower") + "\n" +
			"  public void Dispose() { throw new InvalidOperationException(\"dispose-boom\"); }\n" +
			"  public string Execute(ComicBook[] books) { return \"\"; }\n" +
			"}\n" +
			"public class Survivor : DotNetPlugin, IComicInfoHtml, IDisposable {\n" + Source.PluginKey("Survivor") + "\n" +
			"  public static bool Disposed;\n" +
			"  public void Dispose() { Disposed = true; }\n" +
			"  public string Execute(ComicBook[] books) { return \"\"; }\n" +
			"}\n" + Reporter("Report", "Survivor.Disposed.ToString()")));
		var loaded = scripts.Load();

		loaded.Engine.Dispose();

		await Assert.That(loaded.Command("Report").Invoke(NoBooks)).IsEqualTo("True");
		await Assert.That(PluginConsole.LinesMentioning("'Thrower'")).Contains("dispose-boom");
	}

	[Test]
	public async Task Saved_panel_config_round_trips_and_is_read_back_after_the_plugins_are_loaded_again()
	{
		using var scripts = new ScriptsFolder();
		scripts.Compile(@"Scripts\Mine", Source.Plugin("Panel", "IComicInfoHtml", "public string Execute(ComicBook[] books) { return \"\"; }"));
		var panel = scripts.Load().Command("Panel");

		await Assert.That(panel.LoadConfig()).IsEqualTo("");
		await Assert.That(panel.SaveConfig("<settings a=\"1\"/>")).IsTrue();

		await Assert.That(panel.LoadConfig()).IsEqualTo("<settings a=\"1\"/>");
		await Assert.That(scripts.Load().Command("Panel").LoadConfig()).IsEqualTo("<settings a=\"1\"/>");
	}

	[Test]
	public async Task Config_of_two_plugins_in_one_dll_is_kept_apart()
	{
		using var scripts = new ScriptsFolder();
		scripts.Compile(@"Scripts\Mine", Source.Types(
			"public class One : DotNetPlugin, IComicInfoHtml {\n" + Source.PluginKey("One") + "\npublic string Execute(ComicBook[] books) { return \"\"; }\n}\n" +
			"public class Two : DotNetPlugin, IComicInfoHtml {\n" + Source.PluginKey("Two") + "\npublic string Execute(ComicBook[] books) { return \"\"; }\n}"));
		var loaded = scripts.Load();

		loaded.Command("One").SaveConfig("first");

		await Assert.That(loaded.Command("Two").LoadConfig()).IsEqualTo("");
	}

	[Test]
	public async Task Config_is_shared_by_the_hook_commands_of_one_plugin_class()
	{
		using var scripts = new ScriptsFolder();
		scripts.Compile(@"Scripts\Mine", Source.Plugin("Both", "IComicInfoHtml, IQuickOpenHtml",
			"string IComicInfoHtml.Execute(ComicBook[] books) { return \"\"; }\nstring IQuickOpenHtml.Execute(ComicBook[] books) { return \"\"; }"));
		var loaded = scripts.Load();

		loaded.Engine.GetCommands("ComicInfoHtml").Single().SaveConfig("shared");

		await Assert.That(loaded.Engine.GetCommands("QuickOpenHtml").Single().LoadConfig()).IsEqualTo("shared");
	}
}
