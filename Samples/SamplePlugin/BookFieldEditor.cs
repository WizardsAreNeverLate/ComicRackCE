using System;
using System.Windows.Forms;
using cYo.Projects.ComicRack.Engine;
using cYo.Projects.ComicRack.Plugins;

namespace SamplePlugin
{
	public class BookFieldEditor : UIControl
	{
		private sealed class BookField
		{
			public BookField(string name, string separator, Func<ComicBook, string> get, Action<ComicBook, string> set)
			{
				Name = name;
				Separator = separator;
				Get = get;
				Set = set;
			}

			public string Name { get; }

			public string Separator { get; }

			public Func<ComicBook, string> Get { get; }

			public Action<ComicBook, string> Set { get; }

			public override string ToString() => Name;
		}

		private static readonly BookField[] fields =
		{
			new BookField("Notes", Environment.NewLine, b => b.Notes, (b, v) => b.Notes = v),
			new BookField("Tags", ", ", b => b.Tags, (b, v) => b.Tags = v),
			new BookField("Genre", ", ", b => b.Genre, (b, v) => b.Genre = v),
			new BookField("Summary", Environment.NewLine, b => b.Summary, (b, v) => b.Summary = v)
		};

		private readonly Label selection = new Label { AutoSize = true, Margin = new Padding(3, 3, 3, 8) };

		private readonly ComboBox fieldPicker = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };

		private readonly TextBox value = new TextBox { Dock = DockStyle.Fill };

		private readonly CheckBox append = new CheckBox { Text = "Append to the current value", AutoSize = true };

		private readonly Button apply = new Button { Text = "Apply", AutoSize = true, Anchor = AnchorStyles.Right };

		private readonly ListBox current = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };

		private ComicBook[] books = new ComicBook[0];

		public BookFieldEditor()
		{
			fieldPicker.Items.AddRange(fields);
			fieldPicker.SelectedIndex = 0;
			fieldPicker.SelectedIndexChanged += (s, e) => RefreshCurrent();
			value.TextChanged += (s, e) => UpdateApply();
			append.CheckedChanged += (s, e) => UpdateApply();
			apply.Click += (s, e) => Apply();

			TableLayoutPanel layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 6, Padding = new Padding(6) };
			layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
			layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
			for (int row = 0; row < 5; row++)
			{
				layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
			}
			layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
			layout.Controls.Add(selection, 0, 0);
			layout.SetColumnSpan(selection, 2);
			layout.Controls.Add(new Label { Text = "Field", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
			layout.Controls.Add(fieldPicker, 1, 1);
			layout.Controls.Add(new Label { Text = "Value", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
			layout.Controls.Add(value, 1, 2);
			layout.Controls.Add(append, 1, 3);
			layout.Controls.Add(apply, 1, 4);
			layout.Controls.Add(current, 0, 5);
			layout.SetColumnSpan(current, 2);
			Controls.Add(layout);
			ShowInfo(books);
		}

		private BookField Field => (BookField)fieldPicker.SelectedItem;

		public override void ShowInfo(ComicBook[] books)
		{
			this.books = books ?? new ComicBook[0];
			selection.Text = this.books.Length == 1 ? "1 book selected" : this.books.Length + " books selected";
			RefreshCurrent();
		}

		private void RefreshCurrent()
		{
			current.BeginUpdate();
			current.Items.Clear();
			foreach (ComicBook book in books)
			{
				string text = Field.Get(book) ?? string.Empty;
				current.Items.Add(book.Caption + ": " + text.Replace(Environment.NewLine, " | "));
			}
			current.EndUpdate();
			UpdateApply();
		}

		private void UpdateApply()
		{
			apply.Enabled = books.Length > 0 && (value.Text.Length > 0 || !append.Checked);
		}

		private void Apply()
		{
			foreach (ComicBook book in books)
			{
				string old = Field.Get(book);
				Field.Set(book, append.Checked && !string.IsNullOrEmpty(old) ? old + Field.Separator + value.Text : value.Text);
			}
			RefreshCurrent();
		}
	}
}
