using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using cYo.Projects.ComicRack.Plugins;

namespace ComicRack.Plugins.Tests;

public class HookSignatureTests
{
	public static IEnumerable<(string Hook, string Method)> CommandHookSignatures() => new[]
	{
		("Library", "public void M(ComicBook[] books) { }"),
		("Books", "public void M(ComicBook[] books) { }"),
		("NewBooks", "public void M(ComicBook[] books) { }"),
		("Editor", "public void M(ComicBook[] books) { }"),
		("ParseComicPath", "public void M(string path, ComicNameInfo info) { }"),
		("BookOpened", "public void M(ComicBook book) { }"),
		("CreateBookList", "public IEnumerable<ComicBook> M(ComicBook[] books, string a, string b) { return books; }"),
		("ReaderResized", "public void M(int width, int height) { }"),
		("NetSearch", "public Dictionary<string, string> M(string a, string b, int c) { return null; }"),
		("Startup", "public void M() { }"),
		("Shutdown", "public bool M(bool userClosing) { return true; }"),
		("ComicInfoHtml", "public string M(ComicBook[] books) { return null; }"),
		("QuickOpenHtml", "public string M(ComicBook[] books) { return null; }"),
		("ComicInfoUI", "public System.Windows.Forms.Control M() { return null; }"),
		("QuickOpenUI", "public System.Windows.Forms.Control M() { return null; }"),
		("DrawThumbnailOverlay", "public void M(ComicBook book, System.Drawing.Graphics g, System.Drawing.Rectangle bounds, int flags) { }")
	};

	[Test]
	[MethodDataSource(nameof(CommandHookSignatures))]
	public async Task Method_with_the_hook_signature_is_registered_for_that_hook(string hook, string method)
	{
		using var scripts = new ScriptsFolder();
		scripts.Compile(@"Scripts\Mine", Source.Plugin("[PluginCommand(\"" + hook + "\", Key = \"M\")] " + method));

		var loaded = scripts.Load();

		await Assert.That(loaded.Keys).IsEquivalentTo(new[] { "M" });
		await Assert.That(loaded.Command("M").Hook).IsEqualTo(hook);
	}

	[Test]
	public async Task Every_hook_the_engine_accepts_has_a_signature_case()
	{
		var commandHooks = PluginEngine.ValidHooks.Keys.Where(hook => hook != PluginEngine.ScriptTypeConfig);

		await Assert.That(CommandHookSignatures().Select(s => s.Hook)).IsEquivalentTo(commandHooks);
	}
}
