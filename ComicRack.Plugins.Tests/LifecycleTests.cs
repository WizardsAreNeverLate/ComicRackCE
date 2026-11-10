using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using cYo.Projects.ComicRack.Engine;
using cYo.Projects.ComicRack.Plugins;

namespace ComicRack.Plugins.Tests;

public class LifecycleTests
{
	private static readonly object[] NoBooks = { new ComicBook[0] };

	[Test]
	public async Task One_instance_of_the_class_is_created_and_shared_by_all_its_hooks()
	{
		using var scripts = new ScriptsFolder();
		scripts.Compile(@"Scripts\Mine", Source.Plugin("Shared", "IComicInfoHtml, IQuickOpenHtml", """
			private static int created;
			private readonly string id;
			public Plugin() { created++; id = "instance " + created; }
			string IComicInfoHtml.Execute(ComicBook[] books) { return id; }
			string IQuickOpenHtml.Execute(ComicBook[] books) { return id; }
			"""));

		var loaded = scripts.Load();

		await Assert.That(loaded.Command("Shared", PluginEngine.ScriptTypeComicInfoHtml).Invoke(NoBooks)).IsEqualTo("instance 1");
		await Assert.That(loaded.Command("Shared", PluginEngine.ScriptTypeQuickOpenHtml).Invoke(NoBooks)).IsEqualTo("instance 1");
	}

	[Test]
	public async Task OnLoad_runs_once_with_the_environment_and_the_plugin_folder_that_the_constructor_does_not_have()
	{
		using var scripts = new ScriptsFolder();
		string dll = scripts.Compile(@"Scripts\Mine", Source.Plugin("Loaded", "IComicInfoHtml, IQuickOpenHtml, IStartup", """
			private string seen = "";
			public Plugin() { seen += "ctor:" + (ComicRack == null) + ":" + (ScriptPath == null) + ";"; }
			protected override void OnLoad() { seen += "load:" + ComicRack.CommandPath + ":" + ScriptPath + ";"; }
			string IComicInfoHtml.Execute(ComicBook[] books) { return seen; }
			string IQuickOpenHtml.Execute(ComicBook[] books) { return seen; }
			public void Execute() { }
			"""));
		string folder = Path.GetDirectoryName(dll);

		object seen = scripts.Load().Command("Loaded", PluginEngine.ScriptTypeComicInfoHtml).Invoke(NoBooks);

		await Assert.That(seen).IsEqualTo($"ctor:True:True;load:{folder}:{folder};");
	}

	[Test]
	public async Task Class_whose_OnLoad_throws_registers_no_command_and_its_sibling_still_loads()
	{
		using var scripts = new ScriptsFolder();
		scripts.Compile(@"Scripts\Mine", Source.Types("""
			public class Failing : DotNetPlugin, IStartup, IShutdown
			{
				public override string Key { get; set; } = "Failing";
				protected override void OnLoad() { throw new InvalidOperationException("no database"); }
				public void Execute() { }
				public bool Execute(bool user_is_closing) { return true; }
			}
			public class Working : DotNetPlugin, IStartup { public override string Key { get; set; } = "Working"; public void Execute() { } }
			"""));

		var loaded = scripts.Load();

		await Assert.That(loaded.Engine.GetAllCommands().Select(c => c.Key)).IsEquivalentTo(new[] { "Working" });
		await Assert.That(loaded.ConsoleLinesAboutThisFixture).Contains("OnLoad failed: no database");
	}

	[Test]
	public async Task Exception_of_a_hook_reaches_the_caller_unwrapped_and_is_logged()
	{
		using var scripts = new ScriptsFolder();
		scripts.Compile(@"Scripts\Mine", Source.Plugin("Thrower", "IStartup", "public void Execute() { throw new FormatException(\"bad issue number zq-thrown\"); }"));
		Command command = scripts.Load().Command("Thrower", PluginEngine.ScriptTypeStartup);

		var thrown = await Assert.That(() => command.Invoke(null)).ThrowsExactly<FormatException>();

		await Assert.That(thrown.Message).IsEqualTo("bad issue number zq-thrown");
		await Assert.That(thrown.StackTrace).Contains("Fixture.Plugin.Execute");
		await Assert.That(PluginConsole.LinesMentioning("zq-thrown")).IsEqualTo("Thrower.Startup failed: bad issue number zq-thrown");
	}

	[Test]
	public async Task Icon_missing_from_the_plugin_folder_is_taken_from_the_embedded_resources_of_the_plugin_dll()
	{
		using var scripts = new ScriptsFolder();
		string png = scripts.PathTo(@"Resources\icon.png");
		using (var bitmap = new Bitmap(5, 3))
		{
			bitmap.Save(png, System.Drawing.Imaging.ImageFormat.Png);
		}
		scripts.CompileTo(@"Scripts\Mine\WithIcon.dll", "WithIcon" + Guid.NewGuid().ToString("N"), Source.Plugin("Tool", "IBooks", "public Plugin() { Image = \"icon.png\"; } public void Execute(ComicBook[] books) { }"), embeddedResourceFile: png);

		Command tool = scripts.Load().Command("Tool", PluginEngine.ScriptTypeBooks);

		await Assert.That(tool.CommandImage).IsNotNull();
		await Assert.That(tool.CommandImage.Size).IsEqualTo(new Size(5, 3));
	}

	[Test]
	public async Task Icon_in_the_plugin_folder_wins_over_the_embedded_one()
	{
		using var scripts = new ScriptsFolder();
		string embedded = scripts.PathTo(@"Resources\icon.png");
		using (var bitmap = new Bitmap(5, 3))
		{
			bitmap.Save(embedded, System.Drawing.Imaging.ImageFormat.Png);
		}
		using (var bitmap = new Bitmap(9, 9))
		{
			bitmap.Save(scripts.PathTo(@"Scripts\Mine\icon.png"), System.Drawing.Imaging.ImageFormat.Png);
		}
		scripts.CompileTo(@"Scripts\Mine\WithIcon.dll", "WithIcon" + Guid.NewGuid().ToString("N"), Source.Plugin("Tool", "IBooks", "public Plugin() { Image = \"icon.png\"; } public void Execute(ComicBook[] books) { }"), embeddedResourceFile: embedded);

		Command tool = scripts.Load().Command("Tool", PluginEngine.ScriptTypeBooks);

		await Assert.That(tool.CommandImage.Size).IsEqualTo(new Size(9, 9));
	}

	[Test]
	public async Task Command_with_an_icon_that_exists_nowhere_still_loads_without_an_image()
	{
		using var scripts = new ScriptsFolder();
		scripts.Compile(@"Scripts\Mine", Source.Plugin("Tool", "IBooks", "public Plugin() { Image = \"icon.png\"; } public void Execute(ComicBook[] books) { }"));

		Command tool = scripts.Load().Command("Tool", PluginEngine.ScriptTypeBooks);

		await Assert.That(tool).IsNotNull();
		await Assert.That(tool.CommandImage).IsNull();
	}
}
