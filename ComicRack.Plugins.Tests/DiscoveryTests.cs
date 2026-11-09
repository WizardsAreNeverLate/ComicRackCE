using System.Linq;
using System.Threading.Tasks;
using cYo.Projects.ComicRack.Engine;

namespace ComicRack.Plugins.Tests;

public class DiscoveryTests
{
	[Test]
	public async Task Attributed_method_becomes_a_command_with_the_attribute_metadata()
	{
		using var scripts = new ScriptsFolder();
		scripts.Compile(@"Scripts\Mine", Source.Plugin(
			"[PluginCommand(PluginHook.Books, Name = \"Stamp\", Key = \"My.Stamp\", Description = \"Stamps books\", Enabled = false, PCount = 2)]\n" +
			"public void Run(ComicBook[] books) { }"));

		var command = scripts.Load().Command("My.Stamp");

		await Assert.That(command).IsNotNull();
		using (Assert.Multiple())
		{
			await Assert.That(command.Hook).IsEqualTo("Books");
			await Assert.That(command.Name).IsEqualTo("Stamp");
			await Assert.That(command.Description).IsEqualTo("Stamps books");
			await Assert.That(command.Enabled).IsFalse();
			await Assert.That(command.PCount).IsEqualTo(2);
		}
	}

	[Test]
	public async Task Key_defaults_to_namespace_type_and_method_and_name_to_the_method()
	{
		using var scripts = new ScriptsFolder();
		scripts.Compile(@"Scripts\Mine", Source.Plugin("[PluginCommand(PluginHook.Startup)] public void Run() { }"));

		var command = scripts.Load().Command("Fixture.Plugin.Run");

		await Assert.That(command).IsNotNull();
		await Assert.That(command.Name).IsEqualTo("Run");
	}

	[Test]
	[Arguments("Books", "public void M(string s) { }", "expected Action<ComicBook[]>")]
	[Arguments("Books", "public void M(ComicBook[] books, int extra) { }", "expected Action<ComicBook[]>")]
	[Arguments("Startup", "public void M(int x) { }", "expected Action")]
	[Arguments("Startup", "public int M() { return 0; }", "expected Action")]
	[Arguments("Shutdown", "public void M(bool userClosing) { }", "expected Func<Boolean, Boolean>")]
	[Arguments("ComicInfoHtml", "public void M(ComicBook[] books) { }", "expected Func<ComicBook[], String>")]
	public async Task Method_whose_signature_does_not_match_its_hook_is_rejected_and_the_console_names_the_expected_signature(string hook, string method, string expected)
	{
		using var scripts = new ScriptsFolder();
		scripts.Compile(@"Scripts\Mine", Source.Plugin("[PluginCommand(PluginHook." + hook + ", Key = \"M\")] " + method));

		var loaded = scripts.Load();

		await Assert.That(loaded.Command("M")).IsNull();
		await Assert.That(loaded.ConsoleLinesAboutThisFixture).Contains("'Fixture.Plugin.M'").And.Contains(expected);
	}

	[Test]
	[Arguments("PluginHook.None", "no hook given")]
	[Arguments("(PluginHook)(1 << 30)", "unknown hook value 1073741824")]
	[Arguments("PluginHook.Startup | (PluginHook)(1 << 30)", "unknown hook value 1073741824")]
	public async Task Invalid_hook_value_is_rejected_and_the_console_gives_the_reason(string hook, string reason)
	{
		using var scripts = new ScriptsFolder();
		scripts.Compile(@"Scripts\Mine", Source.Plugin("[PluginCommand(" + hook + ", Key = \"M\")] public void M() { }"));

		var loaded = scripts.Load();

		await Assert.That(loaded.Command("M")).IsNull();
		await Assert.That(loaded.ConsoleLinesAboutThisFixture).Contains("'Fixture.Plugin.M'").And.Contains(reason);
	}

	[Test]
	[Arguments("PluginHook.Books | PluginHook.Editor", "Books,Editor")]
	[Arguments("PluginHook.Library | PluginHook.Books", "Library,Books")]
	[Arguments("PluginHook.Books | PluginHook.Startup", null)]
	public async Task Every_hook_in_a_combination_must_match_the_signature_and_the_command_answers_to_each(string hooks, string engineHook)
	{
		using var scripts = new ScriptsFolder();
		scripts.Compile(@"Scripts\Mine", Source.Plugin("[PluginCommand(" + hooks + ", Key = \"M\")] public void M(ComicBook[] books) { }"));

		var loaded = scripts.Load();

		if (engineHook == null)
		{
			await Assert.That(loaded.Keys).IsEmpty();
			return;
		}
		await Assert.That(loaded.Command("M").Hook).IsEqualTo(engineHook);
		foreach (string hook in engineHook.Split(','))
		{
			await Assert.That(loaded.Engine.GetCommands(hook).Select(c => c.Key)).Contains("M");
		}
	}

	[Test]
	public async Task Configure_method_named_by_the_attribute_becomes_the_commands_Configure_action_on_the_same_instance()
	{
		using var scripts = new ScriptsFolder();
		scripts.Compile(@"Scripts\Mine", Source.Plugin(
			"private int configured;\n" +
			"[PluginCommand(PluginHook.Books, Key = \"Tool\", Configure = \"Settings\")] public void Run(ComicBook[] books) { books[0].Notes = \"configured \" + configured; }\n" +
			"public void Settings() { configured++; }"));
		var loaded = scripts.Load();
		var tool = loaded.Command("Tool");
		var book = new ComicBook();

		tool.Configure.Invoke(null);
		tool.Invoke(new object[] { new[] { book } });

		await Assert.That(loaded.Keys).IsEquivalentTo(new[] { "Tool" });
		await Assert.That(tool.Configure.Hook).IsEqualTo("ConfigScript");
		await Assert.That(book.Notes).IsEqualTo("configured 1");
	}

	[Test]
	public async Task Configure_pairs_with_a_command_whose_key_defaults_to_the_method_name_and_may_be_static()
	{
		using var scripts = new ScriptsFolder();
		scripts.Compile(@"Scripts\Mine", Source.Plugin(
			"public static int Configured;\n" +
			"[PluginCommand(PluginHook.ComicInfoHtml, Configure = \"Settings\")] public string Run(ComicBook[] books) { return Configured.ToString(); }\n" +
			"public static void Settings() { Configured++; }"));
		var loaded = scripts.Load();

		var run = loaded.Command("Fixture.Plugin.Run");
		run.Configure.Invoke(null);

		await Assert.That(run.Invoke(new object[] { new ComicBook[0] })).IsEqualTo("1");
		await Assert.That(run.Configure.Key).IsEqualTo("Fixture.Plugin.Run");
		await Assert.That(loaded.Keys).IsEquivalentTo(new[] { "Fixture.Plugin.Run" });
	}

	[Test]
	[Arguments("[PluginCommand(PluginHook.Startup, Key = \"M\", Configure = \"Missing\")] public void M() { }", "'Missing'")]
	[Arguments("[PluginCommand(PluginHook.Startup, Key = \"M\", Configure = \"Settings\")] public void M() { }\npublic void Settings(int x) { }", "'Settings'")]
	[Arguments("[PluginCommand(PluginHook.Startup, Key = \"M\", Configure = \"Settings\")] public void M() { }\npublic int Settings() { return 0; }", "'Settings'")]
	[Arguments("[PluginCommand(PluginHook.Startup, Key = \"M\", Configure = \"Settings\")] public void M() { }\npublic void Settings() { }\npublic void Settings(int x) { }", "'Settings'")]
	[Arguments("[PluginCommand(PluginHook.Startup, Key = \"M\", Configure = \"Settings\")] public void M() { }\nprivate void Settings() { }", "'Settings'")]
	public async Task Command_with_an_unusable_configure_method_is_skipped_and_the_console_names_the_method(string members, string method)
	{
		using var scripts = new ScriptsFolder();
		scripts.Compile(@"Scripts\Mine", Source.Plugin(members));

		var loaded = scripts.Load();

		await Assert.That(loaded.Command("M")).IsNull();
		await Assert.That(loaded.ConsoleLinesAboutThisFixture).Contains("'Fixture.Plugin.M'").And.Contains(method);
	}

	[Test]
	[Arguments("Books", "public void M(IEnumerable<ComicBook> books) { }")]
	[Arguments("Books", "public void M(object books) { }")]
	[Arguments("BookOpened", "public void M(object book) { }")]
	[Arguments("CreateBookList", "public ComicBook[] M(ComicBook[] books, string a, string b) { return books; }")]
	public async Task Parameter_and_return_types_compatible_with_the_hook_are_accepted(string hook, string method)
	{
		using var scripts = new ScriptsFolder();
		scripts.Compile(@"Scripts\Mine", Source.Plugin("[PluginCommand(PluginHook." + hook + ", Key = \"M\")] " + method));

		await Assert.That(scripts.Load().Command("M")).IsNotNull();
	}

	[Test]
	[Arguments("public class Plugin { public Plugin(int x) { } ")]
	[Arguments("public class Plugin { private Plugin() { } ")]
	[Arguments("public abstract class Plugin { ")]
	public async Task Class_without_a_usable_constructor_keeps_its_static_commands_only(string classHeader)
	{
		using var scripts = new ScriptsFolder();
		scripts.Compile(@"Scripts\Mine", Source.Types(classHeader +
			"[PluginCommand(PluginHook.Startup, Key = \"Instance\")] public void Run() { }\n" +
			"[PluginCommand(PluginHook.ComicInfoHtml, Key = \"Static\")] public static string Html(ComicBook[] books) { return \"static ok\"; }\n" +
			"}"));

		var loaded = scripts.Load();

		await Assert.That(loaded.Command("Instance")).IsNull();
		await Assert.That(loaded.ConsoleLinesAboutThisFixture).Contains("'Fixture.Plugin.Run'").And.Contains("constructor");
		await Assert.That(loaded.Command("Static")?.Invoke(new object[] { new ComicBook[0] })).IsEqualTo("static ok");
	}
}
