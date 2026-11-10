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

		Command.LogDefault("braces {not a format item} 4f1c");

		await Assert.That(PluginConsole.LinesMentioning("4f1c")).IsEqualTo("braces {not a format item} 4f1c");
	}

	[Test]
	public async Task Console_output_of_a_plugin_reaches_the_script_console_once_the_console_writer_is_installed()
	{
		using var scripts = new ScriptsFolder();
		scripts.Compile(@"Scripts\Mine", Source.Plugin("Say", "IStartup", "public void Execute() { Console.WriteLine(\"cs-said 9b2e\"); }"));
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

		await Assert.That(PluginConsole.LinesMentioning("9b2e")).IsEqualTo("cs-said 9b2e");
	}

	[Test]
	public async Task Console_writer_discards_output_when_no_script_console_exists()
	{
		Stream previous = Command.Output;
		try
		{
			Command.Output = null;

			TextWriter writer = Command.CreateConsoleWriter();

			await Assert.That(writer).IsSameReferenceAs(TextWriter.Null);
		}
		finally
		{
			Command.Output = previous;
		}
	}
}
