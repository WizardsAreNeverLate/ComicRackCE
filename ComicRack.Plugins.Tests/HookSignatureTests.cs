using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using cYo.Projects.ComicRack.Plugins;

namespace ComicRack.Plugins.Tests;

public class HookSignatureTests
{
	public static IEnumerable<(PluginHook Hook, string EngineHook, string Method)> CommandHookSignatures() => new[]
	{
		(PluginHook.Library, "Library", "public void M(ComicBook[] books) { }"),
		(PluginHook.Books, "Books", "public void M(ComicBook[] books) { }"),
		(PluginHook.NewBooks, "NewBooks", "public void M(ComicBook[] books) { }"),
		(PluginHook.Editor, "Editor", "public void M(ComicBook[] books) { }"),
		(PluginHook.ParseComicPath, "ParseComicPath", "public void M(string path, ComicNameInfo info) { }"),
		(PluginHook.BookOpened, "BookOpened", "public void M(ComicBook book) { }"),
		(PluginHook.CreateBookList, "CreateBookList", "public IEnumerable<ComicBook> M(ComicBook[] books, string a, string b) { return books; }"),
		(PluginHook.ReaderResized, "ReaderResized", "public void M(int width, int height) { }"),
		(PluginHook.Search, "NetSearch", "public Dictionary<string, string> M(string a, string b, int c) { return null; }"),
		(PluginHook.Startup, "Startup", "public void M() { }"),
		(PluginHook.Shutdown, "Shutdown", "public bool M(bool userClosing) { return true; }"),
		(PluginHook.ComicInfoHtml, "ComicInfoHtml", "public string M(ComicBook[] books) { return null; }"),
		(PluginHook.QuickOpenHtml, "QuickOpenHtml", "public string M(ComicBook[] books) { return null; }"),
		(PluginHook.ComicInfoUI, "ComicInfoUI", "public System.Windows.Forms.Control M() { return null; }"),
		(PluginHook.QuickOpenUI, "QuickOpenUI", "public System.Windows.Forms.Control M() { return null; }"),
		(PluginHook.DrawThumbnailOverlay, "DrawThumbnailOverlay", "public void M(ComicBook book, System.Drawing.Graphics g, System.Drawing.Rectangle bounds, int flags) { }")
	};

	[Test]
	[MethodDataSource(nameof(CommandHookSignatures))]
	public async Task Method_with_the_hook_signature_is_registered_under_the_engine_hook_name(PluginHook hook, string engineHook, string method)
	{
		using var scripts = new ScriptsFolder();
		scripts.Compile(@"Scripts\Mine", Source.Plugin("[PluginCommand(PluginHook." + hook + ", Key = \"M\")] " + method));

		var loaded = scripts.Load();

		await Assert.That(loaded.Keys).IsEquivalentTo(new[] { "M" });
		await Assert.That(loaded.Command("M").Hook).IsEqualTo(engineHook);
	}

	[Test]
	public async Task Every_hook_the_engine_accepts_has_a_signature_case()
	{
		var commandHooks = PluginEngine.ValidHooks.Keys.Where(hook => hook != PluginEngine.ScriptTypeConfig);

		await Assert.That(CommandHookSignatures().Select(s => s.EngineHook)).IsEquivalentTo(commandHooks);
	}

	[Test]
	public async Task Every_PluginHook_value_has_a_signature_case()
	{
		var commandValues = Enum.GetValues(typeof(PluginHook)).Cast<PluginHook>().Where(hook => hook != PluginHook.None && hook != PluginHook.Config);

		await Assert.That(CommandHookSignatures().Select(s => s.Hook)).IsEquivalentTo(commandValues);
	}
}
