using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using cYo.Projects.ComicRack.Plugins;

namespace ComicRack.Plugins.Tests;

public class HookRegistrationTests
{
	public static IEnumerable<(string Interface, string EngineHook, string Method)> HookInterfaces() => new[]
	{
		("ILibrary", "Library", "public void Execute(ComicBook[] books) { }"),
		("IBooks", "Books", "public void Execute(ComicBook[] books) { }"),
		("INewBooks", "NewBooks", "public void Execute(ComicBook[] books) { }"),
		("IEditor", "Editor", "public void Execute(ComicBook[] books) { }"),
		("IParseComicPath", "ParseComicPath", "public void Execute(string path, ComicNameInfo info) { }"),
		("IBookOpened", "BookOpened", "public void Execute(ComicBook book) { }"),
		("ICreateBookList", "CreateBookList", "public int PCount { get; set; }\npublic IEnumerable<ComicBook> Execute(ComicBook[] books, string a, string b) { return books; }"),
		("IReaderResized", "ReaderResized", "public void Execute(int width, int height) { }"),
		("ISearch", "NetSearch", "public Dictionary<string, string> Execute(string hint, string text, int limit) { return null; }"),
		("IStartup", "Startup", "public void Execute() { }"),
		("IShutdown", "Shutdown", "public bool Execute(bool userClosing) { return true; }"),
		("IComicInfoHtml", "ComicInfoHtml", "public string Execute(ComicBook[] books) { return null; }"),
		("IQuickOpenHtml", "QuickOpenHtml", "public string Execute(ComicBook[] books) { return null; }"),
		("IComicInfoUI", "ComicInfoUI", "public UIControl Execute() { return null; }"),
		("IQuickOpenUI", "QuickOpenUI", "public UIControl Execute() { return null; }"),
		("IDrawThumbnailOverlay", "DrawThumbnailOverlay", "public void Execute(ComicBook book, System.Drawing.Graphics g, System.Drawing.Rectangle bounds, int flags) { }")
	};

	[Test]
	[MethodDataSource(nameof(HookInterfaces))]
	public async Task Hook_interface_is_registered_under_the_engine_hook_name(string hookInterface, string engineHook, string method)
	{
		using var scripts = new ScriptsFolder();
		scripts.Compile(@"Scripts\Mine", Source.Plugin("M", hookInterface, method));

		var loaded = scripts.Load();

		await Assert.That(loaded.Keys).IsEquivalentTo(new[] { "M" });
		await Assert.That(loaded.Command("M").Hook).IsEqualTo(engineHook);
	}

	[Test]
	public async Task Every_hook_the_engine_accepts_has_a_hook_interface_case()
	{
		var commandHooks = PluginEngine.ValidHooks.Keys.Where(hook => hook != PluginEngine.ScriptTypeConfig);

		await Assert.That(HookInterfaces().Select(s => s.EngineHook)).IsEquivalentTo(commandHooks);
	}
}
