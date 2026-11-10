using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using cYo.Projects.ComicRack.Plugins;
using Microsoft.Win32.SafeHandles;

namespace ComicRack.Plugins.Tests;

public class LoadingSafetyTests
{
	private static string Survivor(string key) => Source.Plugin(key, "IStartup", "public void Execute() { }");

	private static string[] Keys(LoadedPlugins loaded) => loaded.Engine.GetAllCommands().Select(c => c.Key).ToArray();

	[Test]
	public async Task Dll_that_does_not_reference_the_plugin_api_is_never_execution_loaded()
	{
		using var scripts = new ScriptsFolder();
		string library = scripts.Compile(@"Scripts\Mine", "public class Library { public static int Answer() { return 42; } }");
		string plugin = scripts.Compile(@"Scripts\Mine", Survivor("Survivor"));

		var loaded = scripts.Load();

		await Assert.That(Keys(loaded)).IsEquivalentTo(new[] { "Survivor" });
		await Assert.That(ScriptsFolder.IsExecutionLoaded(plugin)).IsTrue();
		await Assert.That(ScriptsFolder.IsExecutionLoaded(library)).IsFalse();
	}

	[Test]
	[Arguments("garbage")]
	[Arguments("empty")]
	public async Task Non_assembly_dll_is_skipped_silently(string kind)
	{
		using var scripts = new ScriptsFolder();
		File.WriteAllBytes(scripts.PathTo(@"Scripts\Mine\junk.dll"), kind == "empty" ? new byte[0] : Encoding.ASCII.GetBytes("MZ this is not a PE image"));
		scripts.Compile(@"Scripts\Mine", Survivor("Survivor"));

		var loaded = scripts.Load();

		await Assert.That(Keys(loaded)).IsEquivalentTo(new[] { "Survivor" });
		await Assert.That(loaded.ConsoleLinesAboutThisFixture).DoesNotContain("junk.dll");
	}

	[Test]
	public async Task Type_with_a_base_class_in_a_missing_assembly_is_skipped_and_the_rest_of_the_dll_loads()
	{
		using var scripts = new ScriptsFolder();
		string missing = scripts.Compile("Elsewhere", "namespace Missing { public class Base { } }");
		scripts.Compile(@"Scripts\Mine", Source.Types("""
			public class Broken : Missing.Base { }
			public class Plugin : DotNetPlugin, IStartup { public override string Key { get; set; } = "Survivor"; public void Execute() { } }
			"""), missing);

		var loaded = scripts.Load();

		await Assert.That(Keys(loaded)).IsEquivalentTo(new[] { "Survivor" });
		await Assert.That(loaded.ConsoleLinesAboutThisFixture).Contains("could not be loaded");
		await Assert.That(() => loaded.Command("Survivor", PluginEngine.ScriptTypeStartup).Invoke(null)).ThrowsNothing();
	}

	[Test]
	public async Task Classes_that_cannot_be_plugins_are_skipped_and_their_siblings_load()
	{
		using var scripts = new ScriptsFolder();
		scripts.Compile(@"Scripts\Mine", Source.Types("""
			public abstract class Shared : DotNetPlugin, IStartup { public void Execute() { } }
			public class Generic<T> : Shared { public override string Key { get; set; } = "Generic"; }
			public class NoPublicConstructor : Shared { private NoPublicConstructor() { } public override string Key { get; set; } = "NoConstructor"; }
			public class ThrowingConstructor : Shared { public ThrowingConstructor() { throw new InvalidOperationException("constructor went wrong"); } public override string Key { get; set; } = "Throwing"; }
			public class NoKey : Shared { public override string Key { get; set; } }
			public class Survivor : Shared { public override string Key { get; set; } = "Survivor"; }
			"""));

		var loaded = scripts.Load();

		await Assert.That(Keys(loaded)).IsEquivalentTo(new[] { "Survivor" });
		string console = loaded.ConsoleLinesAboutThisFixture;
		await Assert.That(console).Contains("Fixture.NoPublicConstructor").And.Contains("Fixture.ThrowingConstructor").And.Contains("constructor went wrong").And.Contains("Fixture.NoKey");
		await Assert.That(console).DoesNotContain("Fixture.Shared").And.DoesNotContain("Fixture.Generic");
	}

	[Test]
	public async Task Second_copy_of_a_loaded_plugin_adds_no_commands_and_is_reported()
	{
		using var scripts = new ScriptsFolder();
		string first = scripts.Compile(@"Scripts\A", Survivor("Survivor"));
		string second = scripts.PathTo(@"Scripts\B\" + Path.GetFileName(first));
		File.Copy(first, second);

		var loaded = scripts.Load();

		await Assert.That(Keys(loaded)).IsEquivalentTo(new[] { "Survivor" });
		await Assert.That(loaded.ConsoleLinesAboutThisFixture).Contains("'" + second + "'").And.Contains("already loaded from '" + first + "'");
	}

	[Test]
	public async Task Plugin_marked_as_downloaded_from_the_internet_is_blocked_and_the_console_says_how_to_unblock()
	{
		using var scripts = new ScriptsFolder();
		string plugin = scripts.Compile(@"Scripts\Mine", Survivor("Blocked"));
		MarkAsDownloaded(plugin);

		var loaded = scripts.Load();

		await Assert.That(Keys(loaded)).IsEmpty();
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
