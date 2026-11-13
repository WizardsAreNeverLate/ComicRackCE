using System.Linq;
using System.Threading.Tasks;
using cYo.Projects.ComicRack.Engine;

namespace ComicRack.Plugins.Tests;

public class DiscoveryTests
{
	[Test]
	public async Task Plugin_properties_become_the_command_metadata()
	{
		using var scripts = new ScriptsFolder();
		scripts.Compile(@"Scripts\Mine", Source.Plugin("My.Stamp", "IBooks",
			"public override string Name { get { return \"Stamp\"; } set { } }\n" +
			"public override string Description { get { return \"Stamps books\"; } set { } }\n" +
			"public override bool Enabled { get { return false; } set { } }\n" +
			"public void Execute(ComicBook[] books) { }"));

		var command = scripts.Load().Command("My.Stamp");

		await Assert.That(command).IsNotNull();
		using (Assert.Multiple())
		{
			await Assert.That(command.Hook).IsEqualTo("Books");
			await Assert.That(command.Name).IsEqualTo("Stamp");
			await Assert.That(command.Description).IsEqualTo("Stamps books");
			await Assert.That(command.Enabled).IsFalse();
		}
	}

	[Test]
	public async Task Name_defaults_to_the_key()
	{
		using var scripts = new ScriptsFolder();
		scripts.Compile(@"Scripts\Mine", Source.Plugin("Fixture.Plugin", "IStartup", "public void Execute() { }"));

		var command = scripts.Load().Command("Fixture.Plugin");

		await Assert.That(command.Name).IsEqualTo("Fixture.Plugin");
	}

	[Test]
	public async Task PCount_of_a_smart_list_plugin_becomes_the_command_PCount()
	{
		using var scripts = new ScriptsFolder();
		scripts.Compile(@"Scripts\Mine", Source.Plugin("Smart", "ICreateBookList",
			"public int PCount { get { return 2; } set { } }\n" +
			"public IEnumerable<ComicBook> Execute(ComicBook[] books, string a, string b) { return books; }"));

		var command = scripts.Load().Command("Smart");

		await Assert.That(command.Hook).IsEqualTo("CreateBookList");
		await Assert.That(command.PCount).IsEqualTo(2);
	}

	[Test]
	[Arguments("IBooks, IEditor", "Books,Editor")]
	[Arguments("ILibrary, IBooks", "Library,Books")]
	[Arguments("IBooks, IStartup", "Books,Startup")]
	public async Task Plugin_gets_one_command_for_each_hook_interface_it_implements(string interfaces, string engineHooks)
	{
		using var scripts = new ScriptsFolder();
		string members = interfaces.Contains("IStartup")
			? "public void Execute(ComicBook[] books) { }\npublic void Execute() { }"
			: "public void Execute(ComicBook[] books) { }";
		scripts.Compile(@"Scripts\Mine", Source.Plugin("M", interfaces, members));

		var loaded = scripts.Load();

		await Assert.That(loaded.Engine.GetAllCommands().Select(c => c.Hook)).IsEquivalentTo(engineHooks.Split(','));
		foreach (string hook in engineHooks.Split(','))
		{
			await Assert.That(loaded.Engine.GetCommands(hook).Select(c => c.Key)).Contains("M");
		}
	}

	[Test]
	public async Task IConfig_of_a_plugin_becomes_the_Configure_action_of_its_other_commands_on_the_same_instance()
	{
		using var scripts = new ScriptsFolder();
		scripts.Compile(@"Scripts\Mine", Source.Plugin("Tool", "IBooks, IConfig",
			"private int configured;\n" +
			"public void Execute(ComicBook[] books) { books[0].Notes = \"configured \" + configured; }\n" +
			"public void Execute() { configured++; }"));
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
	[Arguments("public Plugin(int x) { }")]
	[Arguments("private Plugin() { }")]
	public async Task Plugin_class_without_a_public_parameterless_constructor_is_skipped_and_the_rest_of_the_dll_loads(string constructor)
	{
		using var scripts = new ScriptsFolder();
		scripts.Compile(@"Scripts\Mine", Source.Types(
			"public class Plugin : DotNetPlugin, IStartup {\n" + Source.PluginKey("Broken") + "\n" + constructor + "\npublic void Execute() { }\n}\n" +
			"public class Other : DotNetPlugin, IStartup {\n" + Source.PluginKey("Other") + "\npublic void Execute() { }\n}"));

		var loaded = scripts.Load();

		await Assert.That(loaded.Keys).IsEquivalentTo(new[] { "Other" });
		await Assert.That(loaded.ConsoleLinesAboutThisFixture).Contains("'Fixture.Plugin'");
	}

	[Test]
	public async Task Abstract_class_is_a_base_for_other_plugins_and_not_a_plugin()
	{
		using var scripts = new ScriptsFolder();
		scripts.Compile(@"Scripts\Mine", Source.Types(
			"public abstract class Base : DotNetPlugin, IStartup { public void Execute() { } }\n" +
			"public class Derived : Base {\n" + Source.PluginKey("Derived") + "\n}"));

		var loaded = scripts.Load();

		await Assert.That(loaded.Keys).IsEquivalentTo(new[] { "Derived" });
	}

	[Test]
	public async Task Plugin_with_an_empty_key_is_skipped_and_the_console_says_so()
	{
		using var scripts = new ScriptsFolder();
		scripts.Compile(@"Scripts\Mine", Source.Plugin("", "IStartup", "public void Execute() { }"));

		var loaded = scripts.Load();

		await Assert.That(loaded.Keys).IsEmpty();
		await Assert.That(loaded.ConsoleLinesAboutThisFixture).Contains("the Key is empty");
	}

	[Test]
	public async Task Second_plugin_with_a_key_that_is_already_taken_is_skipped_and_the_console_says_so()
	{
		using var scripts = new ScriptsFolder();
		string members = "public static int Loaded;\nprotected override void OnLoad() { Loaded++; }\npublic void Execute(ComicBook[] books) { }";
		scripts.Compile(@"Scripts\A", Source.Plugin("Same", "IBooks", members));
		scripts.Compile(@"Scripts\B", Source.Plugin("Same", "IBooks", members));

		var loaded = scripts.Load();

		await Assert.That(loaded.Engine.GetAllCommands().Count()).IsEqualTo(1);
		await Assert.That(PluginConsole.LinesMentioning("'Same.Books'")).Contains("already used by another plugin");
	}
}
