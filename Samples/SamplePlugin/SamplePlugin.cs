using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using cYo.Projects.ComicRack.Engine;
using cYo.Projects.ComicRack.Plugins;

namespace SamplePlugin
{
	// A DotNetPlugin class has one Key and gets one command for each hook interface it implements,
	// so every command of this sample is a class of its own.
	public class StartupPlugin : DotNetPlugin, IStartup
	{
		public override string Key { get; set; } = "SamplePlugin.SamplePlugin.Startup";

		public override string Name { get; set; } = "Sample Startup";

		public void Execute()
		{
			Console.WriteLine("SamplePlugin started from " + ComicRack.CommandPath);
		}
	}

	public class StampNotesPlugin : DotNetPlugin, IBooks, IEditor
	{
		public override string Key { get; set; } = "SamplePlugin.SamplePlugin.StampNotes";

		public override string Name { get; set; } = "Stamp Notes";

		public override string Description { get; set; } = "Adds a line to the notes of each selected book";

		public void Execute(ComicBook[] books)
		{
			string stamp = "Stamped by " + Path.GetFileName(ComicRack.CommandPath);
			foreach (ComicBook book in books)
			{
				book.Notes = string.IsNullOrEmpty(book.Notes) ? stamp : book.Notes + Environment.NewLine + stamp;
			}
		}
	}

	public class EditFieldDialogPlugin : DotNetPlugin, IBooks
	{
		public override string Key { get; set; } = "SamplePlugin.SamplePlugin.EditFieldDialog";

		public override string Name { get; set; } = "Edit Field...";

		public override string Description { get; set; } = "Opens a dialog that sets a field on the selected books";

		public void Execute(ComicBook[] books)
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
				ComicRack.Theme.ApplyTheme(dialog);
				dialog.ShowDialog(ComicRack.MainWindow);
			}
		}
	}

	public class FieldEditorPanelPlugin : DotNetPlugin, IComicInfoUI
	{
		public override string Key { get; set; } = "SamplePlugin.SamplePlugin.FieldEditorPanel";

		public override string Name { get; set; } = "Field Editor";

		public override string Description { get; set; } = "A sidebar panel that sets a field on the selected books";

		public UIControl Execute()
		{
			BookFieldEditor editor = new BookFieldEditor();
			ComicRack.Theme.ApplyTheme(editor);
			return editor;
		}
	}
}
