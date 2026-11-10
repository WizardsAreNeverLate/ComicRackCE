using System;
using System.Linq;
using System.Net;
using cYo.Projects.ComicRack.Engine;
using cYo.Projects.ComicRack.Plugins;
using SampleLibrary;

namespace SampleSourcePlugin
{
	// A source plugin. Copy this folder into the Scripts folder of ComicRack; there is nothing to build.
	// It uses SampleLibrary.dll from the lib folder, which SampleSourcePlugin.csplugin lists as a reference.
	public class SampleSourcePlugin : DotNetPlugin, IComicInfoHtml, IStartup
	{
		public override string Key { get; set; } = "SampleSourcePlugin";

		public SampleSourcePlugin()
		{
			Name = "[Code Sample] Slugs (C# source)";
			Description = "Shows a URL-friendly name for the selected books";
		}

		void IStartup.Execute()
		{
			Console.WriteLine("SampleSourcePlugin runs from " + ScriptPath);
		}

		string IComicInfoHtml.Execute(ComicBook[] books)
		{
			string items = string.Concat(books.Select(book => "<li>" + WebUtility.HtmlEncode(Slug.From(book.Caption)) + "</li>"));
			return "<html><body><ul>" + items + "</ul></body></html>";
		}
	}
}
