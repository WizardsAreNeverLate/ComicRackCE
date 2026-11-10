using System;
using System.IO;
using System.Threading.Tasks;
using cYo.Projects.ComicRack.Plugins;

namespace ComicRack.Plugins.Tests;

[NotInParallel(nameof(PluginConsole))]
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

	[Test]
	public async Task Console_output_of_a_plugin_reaches_the_script_console_once_the_console_writer_is_installed()
	{
		using var scripts = new ScriptsFolder();
		scripts.Compile(@"Scripts\Mine", Source.Plugin("Say", "IStartup", "public void Execute() { Console.WriteLine(\"cs-said zq-console\"); }"));
		var loaded = scripts.Load();
		TextWriter previous = Console.Out;

		try
		{
			Console.SetOut(Command.CreateConsoleWriter());
			loaded.Command("Say", PluginEngine.ScriptTypeStartup).Invoke(null);
		}
		finally
		{
			Console.SetOut(previous);
		}

		await Assert.That(PluginConsole.LinesMentioning("zq-console")).IsEqualTo("cs-said zq-console");
	}
}
