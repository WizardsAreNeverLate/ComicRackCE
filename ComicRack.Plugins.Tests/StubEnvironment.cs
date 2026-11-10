using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using cYo.Projects.ComicRack.Engine.Display;
using cYo.Projects.ComicRack.Plugins;
using cYo.Projects.ComicRack.Plugins.Automation;
using cYo.Projects.ComicRack.Plugins.Theme;

namespace ComicRack.Plugins.Tests;

internal sealed class StubEnvironment : IPluginEnvironment
{
	public IWin32Window MainWindow => null;
	public IApplication App => null;
	public IOpenBooksManager OpenBooks => null;
	public IBrowser Browser => null;
	public IComicDisplay ComicDisplay => null;
	public string CommandPath { get; set; }
	public IThemePlugin Theme { get; set; } = ThemePlugin.Default;
	public IEnumerable<string> LibraryPaths => Enumerable.Empty<string>();
	public string Localize(string resourceKey, string elementKey, string text) => text;
	public object Clone() => MemberwiseClone();
}
