using System.IO;
using System.Threading.Tasks;
using cYo.Projects.ComicRack.Plugins;

namespace ComicRack.Plugins.Tests;

public class LoggingTests
{
	[Test]
	public async Task Invoking_a_hook_is_logged_to_the_script_console()
	{
		using var scripts = new ScriptsFolder();
		scripts.Compile(@"Scripts\Mine", Source.Plugin("LoggedKey", "IStartup", "public void Execute() { }"));
		var loaded = scripts.Load();

		loaded.Command("LoggedKey", PluginEngine.ScriptTypeStartup).Invoke(null);

		await Assert.That(PluginConsole.LinesMentioning("LoggedKey")).Contains("Calling 'LoggedKey.Startup'");
	}

	[Test]
	public async Task Log_text_with_braces_is_written_as_it_is()
	{
		PluginConsole.Install();

		Command.LogDefault("braces {not a format item} zq-braces");

		await Assert.That(PluginConsole.LinesMentioning("zq-braces")).IsEqualTo("braces {not a format item} zq-braces");
	}

	// ComicRack installs this writer as Console.Out when it shows the script console, so Console.WriteLine of a plugin ends up there.
	[Test]
	public async Task Console_writer_writes_each_line_to_the_script_console_at_once()
	{
		PluginConsole.Install();
		TextWriter writer = Command.CreateConsoleWriter();

		writer.WriteLine("plugin said zq-console");

		await Assert.That(PluginConsole.LinesMentioning("zq-console")).IsEqualTo("plugin said zq-console");
	}
}
