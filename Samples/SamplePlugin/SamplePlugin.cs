using System;
using System.Diagnostics;
using System.IO;
using cYo.Projects.ComicRack.Engine;
using cYo.Projects.ComicRack.Plugins;

namespace SamplePlugin
{
	public class SamplePlugin
	{
		private readonly IPluginEnvironment comicRack;

		public SamplePlugin(IPluginEnvironment comicRack)
		{
			this.comicRack = comicRack;
		}

		[PluginCommand(PluginEngine.ScriptTypeStartup, Name = "Sample Startup")]
		public void Startup()
		{
			Trace.WriteLine("SamplePlugin started from " + comicRack.CommandPath);
		}

		[PluginCommand(PluginEngine.ScriptTypeBooks + "," + PluginEngine.ScriptTypeEditor, Name = "Stamp Notes", Description = "Adds a line to the notes of each selected book")]
		public void StampNotes(ComicBook[] books)
		{
			string stamp = "Stamped by " + Path.GetFileName(comicRack.CommandPath);
			foreach (ComicBook book in books)
			{
				book.Notes = string.IsNullOrEmpty(book.Notes) ? stamp : book.Notes + Environment.NewLine + stamp;
			}
		}
	}
}
