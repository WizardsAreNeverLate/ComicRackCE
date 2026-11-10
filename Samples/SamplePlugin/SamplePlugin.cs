using System;
using System.Linq;
using System.Net;
using System.Windows.Forms;
using cYo.Projects.ComicRack.Engine;
using cYo.Projects.ComicRack.Plugins;

namespace SamplePlugin
{
	// A compiled plugin. Build the project and copy SamplePlugin.dll into a folder below the Scripts folder of ComicRack.
	//
	// The class gets one command for each hook interface it implements. All commands share this one instance.
	public class SamplePlugin : DotNetPlugin, IBooks, ILibrary, IComicInfoHtml, IConfig, IDisposable
	{
		private string loadedAt;

		public override string Key { get; set; } = "SamplePlugin";

		// ComicRack creates the class when it scans the script folders, also when the plugin is disabled.
		// Only describe the plugin here. ComicRack and ScriptPath are not set yet.
		public SamplePlugin()
		{
			Name = "[Code Sample] Count Books (C#)";
			Description = "Shows how many books a command gets";
			Image = "SamplePlugin.png";
		}

		// Runs once, before the first hook. ComicRack and ScriptPath are set from here on.
		protected override void OnLoad()
		{
			loadedAt = DateTime.Now.ToShortTimeString();
			Console.WriteLine("SamplePlugin loaded from " + ScriptPath);
		}

		// IBooks and ILibrary have the same Execute signature. A public Execute(ComicBook[]) method would serve both hooks.
		// Explicit implementations give each hook its own code.
		void IBooks.Execute(ComicBook[] books)
		{
			Show(books.Length + " selected books, " + books.Sum(book => book.PageCount) + " pages");
		}

		void ILibrary.Execute(ComicBook[] books)
		{
			Show(books.Length + " books in the library");
		}

		string IComicInfoHtml.Execute(ComicBook[] books)
		{
			string items = string.Concat(books.Select(book => "<li>" + WebUtility.HtmlEncode(book.Caption) + "</li>"));
			return "<html><body><p>Sample plugin, loaded at " + loadedAt + "</p><ul>" + items + "</ul></body></html>";
		}

		// The Configure button of the plugin in the Preferences, and the arrow of its toolbar button.
		void IConfig.Execute()
		{
			Show("The plugin is in " + ScriptPath);
		}

		// Runs when ComicRack closes.
		public void Dispose()
		{
			Console.WriteLine("SamplePlugin disposed");
		}

		private void Show(string text)
		{
			MessageBox.Show(ComicRack.MainWindow, text, Name);
		}
	}
}
