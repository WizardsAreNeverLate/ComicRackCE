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
}
