using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
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

		[PluginCommand(PluginEngine.ScriptTypeBooks, Name = "Edit Field...", Description = "Opens a dialog that sets a field on the selected books")]
		public void EditFieldDialog(ComicBook[] books)
		{
			using (Form dialog = new Form())
			{
				dialog.Text = "Edit Field (Sample C# Plugin)";
				dialog.StartPosition = FormStartPosition.CenterParent;
				dialog.ClientSize = new Size(460, 340);
				dialog.MinimizeBox = false;
				dialog.ShowInTaskbar = false;
				BookFieldEditor editor = new BookFieldEditor { Dock = DockStyle.Fill };
				editor.ShowInfo(books);
				Button close = new Button { Text = "Close", DialogResult = DialogResult.Cancel, AutoSize = true };
				FlowLayoutPanel buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(6) };
				buttons.Controls.Add(close);
				dialog.CancelButton = close;
				dialog.Controls.Add(editor);
				dialog.Controls.Add(buttons);
				comicRack.Theme.ApplyTheme(dialog);
				dialog.ShowDialog(comicRack.MainWindow);
			}
		}

		[PluginCommand(PluginEngine.ScriptTypeComicInfoUI, Name = "Field Editor", Description = "A sidebar panel that sets a field on the selected books")]
		public Control FieldEditorPanel()
		{
			BookFieldEditor editor = new BookFieldEditor();
			comicRack.Theme.ApplyTheme(editor);
			return editor;
		}
	}
}
