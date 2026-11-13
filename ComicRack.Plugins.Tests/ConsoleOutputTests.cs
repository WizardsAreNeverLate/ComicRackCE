using System;
using System.IO;
using System.Threading.Tasks;
using cYo.Projects.ComicRack.Plugins;

namespace ComicRack.Plugins.Tests;

[NotInParallel]
public class ConsoleOutputTests
{
	[Test]
	public async Task Console_output_of_a_plugin_reaches_the_script_console_once_the_console_writer_is_installed()
	{
		using var scripts = new ScriptsFolder();
		scripts.Compile(@"Scripts\Mine", Source.Plugin("Say", "IStartup",
			"public void Execute() { Console.WriteLine(\"cs-said \" + ComicRack.CommandPath); }"));
		var loaded = scripts.Load();
		TextWriter previous = Console.Out;

		try
		{
			Console.SetOut(Command.CreateConsoleWriter());
			loaded.Command("Say").Invoke(null);
		}
		finally
		{
			Console.SetOut(previous);
		}

		await Assert.That(loaded.ConsoleLinesAboutThisFixture).Contains("cs-said ");
	}

	[Test]
	public async Task Console_writer_discards_output_when_no_script_console_exists()
	{
		try
		{
			Command.SetLogOutput(null);
			TextWriter writer = Command.CreateConsoleWriter();

			writer.WriteLine("nobody listens");

			await Assert.That(writer).IsSameReferenceAs(TextWriter.Null);
		}
		finally
		{
			PluginConsole.Install();
		}
	}
}
