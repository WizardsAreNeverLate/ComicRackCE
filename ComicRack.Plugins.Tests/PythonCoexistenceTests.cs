using System.IO;
using System.Linq;
using System.Threading.Tasks;
using cYo.Projects.ComicRack.Engine;

namespace ComicRack.Plugins.Tests;

public class PythonCoexistenceTests
{
	[Test]
	public async Task Python_and_csharp_plugins_in_one_tree_both_run_for_their_hook()
	{
		using var scripts = new ScriptsFolder();
		File.WriteAllText(scripts.PathTo(@"Scripts\Py\stamp.py"),
			"#@Name Py Stamp\n#@Hook Books\n#@Key PyStamp\ndef PyStamp(books):\n\tfor b in books:\n\t\tb.Notes += 'py;'\n");
		scripts.Compile(@"Scripts\Cs", Source.Plugin(
			"[PluginCommand(\"Books\", Key = \"CsStamp\")] public void Stamp(ComicBook[] books) { foreach (ComicBook b in books) b.Notes += \"cs;\"; }"));
		var book = new ComicBook();

		var loaded = scripts.Load();
		loaded.Engine.Invoke("Books", new object[] { new[] { book } });

		await Assert.That(loaded.Engine.GetCommands("Books").Select(c => c.Key)).IsEquivalentTo(new[] { "PyStamp", "CsStamp" });
		await Assert.That(book.Notes.Split(';')).IsEquivalentTo(new[] { "py", "cs", "" });
	}

	[Test]
	public async Task Python_plugin_with_a_hook_list_keeps_its_hook_string_and_runs_for_each_hook()
	{
		using var scripts = new ScriptsFolder();
		File.WriteAllText(scripts.PathTo(@"Scripts\Py\multi.py"),
			"#@Name Py Multi\n#@Hook Books, Editor\n#@Key PyMulti\ndef PyMulti(books):\n\tfor b in books:\n\t\tb.Notes += 'py;'\n");
		var book = new ComicBook();

		var loaded = scripts.Load();
		loaded.Engine.Invoke("Books", new object[] { new[] { book } });
		loaded.Engine.Invoke("Editor", new object[] { new[] { book } });

		await Assert.That(loaded.Command("PyMulti").Hook).IsEqualTo("Books, Editor");
		await Assert.That(book.Notes).IsEqualTo("py;py;");
	}

	[Test]
	public async Task Python_plugin_print_output_reaches_the_script_console()
	{
		using var scripts = new ScriptsFolder();
		File.WriteAllText(scripts.PathTo(@"Scripts\Py\talk.py"),
			"#@Hook ComicInfoHtml\n#@Key PyTalk\ndef PyTalk(books):\n\tprint('py-said ' + ScriptPath)\n\treturn 'ok'\n");

		var loaded = scripts.Load();
		loaded.Command("PyTalk").Invoke(new object[] { new ComicBook[0] });

		await Assert.That(loaded.ConsoleLinesAboutThisFixture).Contains("py-said ");
	}

	[Test]
	[Arguments(false)]
	[Arguments(true)]
	public async Task Python_plugin_loads_its_own_dotnet_helper_from_the_scripts_folder(bool helperReferencesPluginApi)
	{
		using var scripts = new ScriptsFolder();
		string api = helperReferencesPluginApi ? "public static System.Type Api() { return typeof(cYo.Projects.ComicRack.Plugins.IPluginEnvironment); }" : "";
		string helper = scripts.Compile(@"Scripts\Py", "namespace PyHelper { public static class Helper { public static string Text() { return \"from helper\"; } " + api + " } }");
		File.WriteAllText(scripts.PathTo(@"Scripts\Py\helper.py"),
			"import clr\n#@Hook ComicInfoHtml\n#@Key PyHelper\ndef Html(books):\n" +
			"\tclr.AddReferenceToFileAndPath(ScriptPath + '\\\\" + Path.GetFileName(helper) + "')\n" +
			"\tfrom PyHelper import Helper\n" +
			"\treturn Helper.Text()\n");

		var command = scripts.Load().Command("PyHelper");

		await Assert.That(command.Invoke(new object[] { new ComicBook[0] })).IsEqualTo("from helper");
	}
}
