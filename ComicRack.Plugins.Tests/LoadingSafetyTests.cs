using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using cYo.Projects.ComicRack.Engine;
using Microsoft.Win32.SafeHandles;

namespace ComicRack.Plugins.Tests;

public class LoadingSafetyTests
{
	private const string StartupPlugin = "[PluginCommand(PluginHook.Startup, Key = \"Survivor\")] public static void Run() { }";

	[Test]
	public async Task Dll_that_does_not_reference_the_plugin_api_is_never_execution_loaded()
	{
		using var scripts = new ScriptsFolder();
		string library = scripts.Compile(@"Scripts\Mine", Source.Types("public class Library { public static int Answer() { return 42; } }"));
		string plugin = scripts.Compile(@"Scripts\Mine", Source.Plugin(StartupPlugin));

		var loaded = scripts.Load();

		await Assert.That(loaded.Keys).IsEquivalentTo(new[] { "Survivor" });
		await Assert.That(ScriptsFolder.IsExecutionLoaded(plugin)).IsTrue();
		await Assert.That(ScriptsFolder.IsExecutionLoaded(library)).IsFalse();
	}

	[Test]
	[Arguments("garbage")]
	[Arguments("empty")]
	public async Task Non_assembly_dll_is_skipped_silently(string kind)
	{
		using var scripts = new ScriptsFolder();
		string junk = scripts.PathTo(@"Scripts\Mine\junk.dll");
		switch (kind)
		{
			case "garbage":
				File.WriteAllBytes(junk, Encoding.ASCII.GetBytes("MZ this is not a PE image"));
				break;
			case "empty":
				File.WriteAllBytes(junk, new byte[0]);
				break;
		}
		scripts.Compile(@"Scripts\Mine", Source.Plugin(StartupPlugin));

		var loaded = scripts.Load();

		await Assert.That(loaded.Keys).IsEquivalentTo(new[] { "Survivor" });
		await Assert.That(loaded.ConsoleLinesAboutThisFixture).DoesNotContain("junk.dll");
	}

	[Test]
	public async Task Type_with_a_base_class_in_a_missing_assembly_is_skipped_and_the_rest_of_the_dll_loads()
	{
		using var scripts = new ScriptsFolder();
		string missing = scripts.Compile("Elsewhere", "namespace Missing { public class Base { } }");
		scripts.Compile(@"Scripts\Mine", Source.Types(
			"public class Broken : Missing.Base { [PluginCommand(PluginHook.Startup, Key = \"Broken\")] public void Run() { } }\n" +
			"public class Plugin { " + StartupPlugin + " }"), missing);

		var loaded = scripts.Load();

		await Assert.That(loaded.Keys).IsEquivalentTo(new[] { "Survivor" });
		await Assert.That(loaded.ConsoleLinesAboutThisFixture).Contains("could not be loaded");
		await Assert.That(() => loaded.Command("Survivor").Invoke(null)).ThrowsNothing();
	}

	[Test]
	public async Task Plugin_dependency_next_to_the_plugin_resolves_at_invoke()
	{
		using var scripts = new ScriptsFolder();
		string helper = scripts.Compile(@"Scripts\Mine", "namespace Helper { public static class Stamp { public static string Text() { return \"from helper\"; } } }");
		scripts.Compile(@"Scripts\Mine", Source.Plugin("[PluginCommand(PluginHook.ComicInfoHtml, Key = \"M\")] public string M(ComicBook[] books) { return Helper.Stamp.Text(); }"), helper);

		var command = scripts.Load().Command("M");

		await Assert.That(command.Invoke(new object[] { new ComicBook[0] })).IsEqualTo("from helper");
	}

	[Test]
	public async Task Second_copy_of_a_loaded_plugin_adds_no_commands_and_is_reported()
	{
		using var scripts = new ScriptsFolder();
		string first = scripts.Compile(@"Scripts\A", Source.Plugin(StartupPlugin));
		string second = scripts.PathTo(@"Scripts\B\" + Path.GetFileName(first));
		File.Copy(first, second);

		var loaded = scripts.Load();

		await Assert.That(loaded.Keys).IsEquivalentTo(new[] { "Survivor" });
		await Assert.That(loaded.Command("Survivor").Environment.CommandPath).IsEqualTo(Path.GetDirectoryName(first));
		await Assert.That(loaded.ConsoleLinesAboutThisFixture).Contains("'" + second + "'").And.Contains("already loaded from '" + first + "'");
	}

	[Test]
	public async Task Plugin_marked_as_downloaded_from_the_internet_is_blocked_and_the_console_says_how_to_unblock()
	{
		using var scripts = new ScriptsFolder();
		string plugin = scripts.Compile(@"Scripts\Mine", Source.Plugin(StartupPlugin));
		MarkAsDownloaded(plugin);

		var loaded = scripts.Load();

		await Assert.That(loaded.Keys).IsEmpty();
		await Assert.That(ScriptsFolder.IsExecutionLoaded(plugin)).IsFalse();
		await Assert.That(loaded.ConsoleLinesAboutThisFixture).Contains("blocked").And.Contains("Unblock");
	}

	// .NET Framework rejects a path with a stream name, so the Zone.Identifier stream is written through CreateFileW.
	private static void MarkAsDownloaded(string file)
	{
		using (SafeFileHandle handle = CreateFileW(file + ":Zone.Identifier", GenericWrite, 0, IntPtr.Zero, CreateAlways, 0, IntPtr.Zero))
		{
			if (handle.IsInvalid)
			{
				throw new Win32Exception();
			}
			using (var stream = new FileStream(handle, FileAccess.Write))
			{
				byte[] mark = Encoding.ASCII.GetBytes("[ZoneTransfer]\r\nZoneId=3\r\n");
				stream.Write(mark, 0, mark.Length);
			}
		}
	}

	private const uint GenericWrite = 0x40000000;

	private const uint CreateAlways = 2;

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern SafeFileHandle CreateFileW(string name, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);
}
