using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using cYo.Projects.ComicRack.Engine;

namespace ComicRack.Plugins.Tests;

public class LifetimeTests
{
	private static readonly object[] NoBooks = { new ComicBook[0] };

	[Test]
	public async Task Plugin_class_is_created_once_on_first_invoke_and_shared_by_its_commands()
	{
		using var scripts = new ScriptsFolder();
		scripts.Compile(@"Scripts\Mine", Source.Plugin(
			"private static int created;\n" +
			"private readonly string id;\n" +
			"public Plugin() { created++; id = \"instance \" + created; }\n" +
			"[PluginCommand(PluginHook.ComicInfoHtml, Key = \"Created\")] public static string Created(ComicBook[] books) { return created.ToString(); }\n" +
			"[PluginCommand(PluginHook.ComicInfoHtml, Key = \"First\")] public string First(ComicBook[] books) { return id; }\n" +
			"[PluginCommand(PluginHook.QuickOpenHtml, Key = \"Second\")] public string Second(ComicBook[] books) { return id; }"));

		var loaded = scripts.Load();

		await Assert.That(loaded.Command("Created").Invoke(NoBooks)).IsEqualTo("0");
		await Assert.That(loaded.Command("First").Invoke(NoBooks)).IsEqualTo("instance 1");
		await Assert.That(loaded.Command("Second").Invoke(NoBooks)).IsEqualTo("instance 1");
		await Assert.That(loaded.Command("First").Invoke(NoBooks)).IsEqualTo("instance 1");
		await Assert.That(loaded.Command("Created").Invoke(NoBooks)).IsEqualTo("1");
	}

	[Test]
	public async Task Constructor_receives_an_environment_whose_CommandPath_is_the_plugin_folder()
	{
		using var scripts = new ScriptsFolder();
		Func<string, string> source = key => Source.Plugin(
			"private readonly IPluginEnvironment env;\n" +
			"public Plugin(IPluginEnvironment env) { this.env = env; }\n" +
			"[PluginCommand(PluginHook.ComicInfoHtml, Key = \"" + key + "\")] public string Where(ComicBook[] books) { return env.CommandPath; }");
		string one = Path.GetDirectoryName(scripts.Compile(@"Scripts\One", source("One")));
		string two = Path.GetDirectoryName(scripts.Compile(@"Scripts\Two", source("Two")));

		var loaded = scripts.Load();

		await Assert.That(loaded.Command("One").Invoke(NoBooks)).IsEqualTo(one);
		await Assert.That(loaded.Command("Two").Invoke(NoBooks)).IsEqualTo(two);
	}

	[Test]
	public async Task Return_values_flow_back_to_the_caller()
	{
		using var scripts = new ScriptsFolder();
		scripts.Compile(@"Scripts\Mine", Source.Plugin("[PluginCommand(PluginHook.Shutdown, Key = \"Quit\")] public bool Quit(bool userClosing) { return !userClosing; }"));

		var quit = scripts.Load().Command("Quit");

		await Assert.That((bool)quit.Invoke(new object[] { true })).IsFalse();
		await Assert.That((bool)quit.Invoke(new object[] { false })).IsTrue();
	}

	[Test]
	public async Task Plugin_exception_reaches_the_caller_unwrapped()
	{
		using var scripts = new ScriptsFolder();
		scripts.Compile(@"Scripts\Mine", Source.Plugin("[PluginCommand(PluginHook.Books, Key = \"Throws\")] public void Throws(ComicBook[] books) { throw new InvalidOperationException(\"boom\"); }"));

		var throws = scripts.Load().Command("Throws");

		var thrown = Assert.ThrowsExactly<InvalidOperationException>(() => throws.Invoke(NoBooks));
		await Assert.That(thrown.Message).IsEqualTo("boom");
	}

	[Test]
	public async Task Invoke_with_catchErrors_returns_null_when_the_plugin_throws()
	{
		using var scripts = new ScriptsFolder();
		scripts.Compile(@"Scripts\Mine", Source.Plugin("[PluginCommand(PluginHook.ComicInfoHtml, Key = \"Throws\")] public string Throws(ComicBook[] books) { throw new InvalidOperationException(\"boom\"); }"));

		var throws = scripts.Load().Command("Throws");

		await Assert.That(throws.Invoke(NoBooks, catchErrors: true)).IsNull();
	}

	[Test]
	public async Task Disposing_the_engine_disposes_a_plugin_instance_shared_by_two_commands_exactly_once()
	{
		using var scripts = new ScriptsFolder();
		scripts.Compile(@"Scripts\Mine", Source.Plugin(
			"public static int Created, Disposed;\n" +
			"public Plugin() { Created++; }\n" +
			"public void Dispose() { Disposed++; }\n" +
			"[PluginCommand(PluginHook.ComicInfoHtml, Key = \"Count\")] public static string Count(ComicBook[] books) { return Created + \"/\" + Disposed; }\n" +
			"[PluginCommand(PluginHook.ComicInfoHtml, Key = \"A\")] public string A(ComicBook[] books) { return \"\"; }\n" +
			"[PluginCommand(PluginHook.QuickOpenHtml, Key = \"B\")] public string B(ComicBook[] books) { return \"\"; }").Replace("class Plugin", "class Plugin : IDisposable"));
		var loaded = scripts.Load();
		loaded.Command("A").Invoke(NoBooks);
		loaded.Command("B").Invoke(NoBooks);

		loaded.Engine.Dispose();
		loaded.Engine.Dispose();

		await Assert.That(loaded.Command("Count").Invoke(NoBooks)).IsEqualTo("1/1");
	}

	[Test]
	public async Task Disposing_the_engine_does_not_construct_a_plugin_that_was_never_used()
	{
		using var scripts = new ScriptsFolder();
		scripts.Compile(@"Scripts\Mine", Source.Plugin(
			"public static int Created, Disposed;\n" +
			"public Plugin() { Created++; }\n" +
			"public void Dispose() { Disposed++; }\n" +
			"[PluginCommand(PluginHook.ComicInfoHtml, Key = \"Count\")] public static string Count(ComicBook[] books) { return Created + \"/\" + Disposed; }\n" +
			"[PluginCommand(PluginHook.ComicInfoHtml, Key = \"A\")] public string A(ComicBook[] books) { return \"\"; }").Replace("class Plugin", "class Plugin : IDisposable"));
		var loaded = scripts.Load();

		loaded.Engine.Dispose();

		await Assert.That(loaded.Command("Count").Invoke(NoBooks)).IsEqualTo("0/0");
	}

	[Test]
	public async Task Plugin_whose_Dispose_throws_is_logged_and_does_not_stop_other_plugins_being_disposed()
	{
		using var scripts = new ScriptsFolder();
		scripts.Compile(@"Scripts\Mine", Source.Types(
			"public class Thrower : IDisposable {\n" +
			"  public void Dispose() { throw new InvalidOperationException(\"dispose-boom\"); }\n" +
			"  [PluginCommand(PluginHook.ComicInfoHtml, Key = \"Thrower\")] public string Run(ComicBook[] books) { return \"\"; }\n" +
			"}\n" +
			"public class Survivor : IDisposable {\n" +
			"  public static bool Disposed;\n" +
			"  public void Dispose() { Disposed = true; }\n" +
			"  [PluginCommand(PluginHook.ComicInfoHtml, Key = \"Survivor\")] public string Run(ComicBook[] books) { return \"\"; }\n" +
			"  [PluginCommand(PluginHook.QuickOpenHtml, Key = \"Report\")] public static string Report(ComicBook[] books) { return Disposed.ToString(); }\n" +
			"}"));
		var loaded = scripts.Load();
		loaded.Command("Thrower").Invoke(NoBooks);
		loaded.Command("Survivor").Invoke(NoBooks);

		loaded.Engine.Dispose();

		await Assert.That(loaded.Command("Report").Invoke(NoBooks)).IsEqualTo("True");
		await Assert.That(PluginConsole.LinesMentioning("'Thrower'")).Contains("dispose-boom");
	}

	[Test]
	public async Task Saved_panel_config_round_trips_and_is_read_back_after_the_plugins_are_loaded_again()
	{
		using var scripts = new ScriptsFolder();
		scripts.Compile(@"Scripts\Mine", Source.Plugin("[PluginCommand(PluginHook.ComicInfoHtml, Key = \"Panel\")] public string Panel(ComicBook[] books) { return \"\"; }"));
		var panel = scripts.Load().Command("Panel");

		await Assert.That(panel.LoadConfig()).IsEqualTo("");
		await Assert.That(panel.SaveConfig("<settings a=\"1\"/>")).IsTrue();

		await Assert.That(panel.LoadConfig()).IsEqualTo("<settings a=\"1\"/>");
		await Assert.That(scripts.Load().Command("Panel").LoadConfig()).IsEqualTo("<settings a=\"1\"/>");
	}

	[Test]
	public async Task Config_of_two_commands_in_one_plugin_is_kept_apart()
	{
		using var scripts = new ScriptsFolder();
		scripts.Compile(@"Scripts\Mine", Source.Plugin(
			"[PluginCommand(PluginHook.ComicInfoHtml, Key = \"One\")] public string One(ComicBook[] books) { return \"\"; }\n" +
			"[PluginCommand(PluginHook.QuickOpenHtml, Key = \"Two\")] public string Two(ComicBook[] books) { return \"\"; }"));
		var loaded = scripts.Load();

		loaded.Command("One").SaveConfig("first");

		await Assert.That(loaded.Command("Two").LoadConfig()).IsEqualTo("");
	}
}
