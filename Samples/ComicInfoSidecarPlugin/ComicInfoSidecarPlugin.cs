using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using cYo.Projects.ComicRack.Engine;
using cYo.Projects.ComicRack.Plugins;

namespace ComicInfoSidecarPlugin
{
	public enum SidecarResult
	{
		Written,
		Unchanged,
		Skipped
	}

	// Writes book.pdf.xml next to each selected book. ComicRack reads that file back as the book's ComicInfo:
	// ComicProvider.Load falls back to ComicInfo.LoadFromSidecar, which looks for "<file>.xml" first.
	public class ComicInfoSidecarPlugin : DotNetPlugin, IBooks
	{
		// The formats this plugin writes a sidecar for. Archives such as .cbz can hold ComicInfo.xml inside the file instead.
		private static readonly string[] Extensions = { ".pdf" };

		public override string Key { get; set; } = "ComicInfoSidecar";

		public override string Name { get; set; } = "Write ComicInfo Sidecar";

		public override string Description { get; set; } = "Writes the metadata of the selected PDF books to book.pdf.xml next to each book";

		public void Execute(ComicBook[] books)
		{
			int written = 0, unchanged = 0, skipped = 0, failed = 0;
			foreach (ComicBook book in books)
			{
				try
				{
					switch (WriteSidecar(book))
					{
					case SidecarResult.Written:
						written++;
						break;
					case SidecarResult.Unchanged:
						unchanged++;
						break;
					default:
						skipped++;
						break;
					}
				}
				catch (Exception e)
				{
					failed++;
					Log($"Failed to write the sidecar of '{book.FilePath}': {e.Message}");
				}
			}
			string summary = $"Sidecars written: {written}\nAlready up to date: {unchanged}\nSkipped (not a PDF, or no file): {skipped}\nFailed: {failed}";
			Log(summary.Replace("\n", ", "));
			MessageBox.Show(ComicRack.MainWindow, summary, Name, MessageBoxButtons.OK, failed > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
		}

		public static string SidecarPath(string bookFile) => bookFile + ".xml";

		public static SidecarResult WriteSidecar(ComicBook book)
		{
			string file = book.FilePath;
			if (!book.IsLinked || string.IsNullOrEmpty(file) || !File.Exists(file) || !Extensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
			{
				return SidecarResult.Skipped;
			}

			byte[] content;
			using (var stream = new MemoryStream())
			{
				// ComicInfo, not ComicBook: ComicRack reads the sidecar's ComicInfo with XmlSerializer<ComicInfo>, which needs the <ComicInfo> root.
				new ComicInfo(book).Serialize(stream);
				content = stream.ToArray();
			}

			string sidecar = SidecarPath(file);
			if (File.Exists(sidecar) && File.ReadAllBytes(sidecar).SequenceEqual(content))
			{
				return SidecarResult.Unchanged;
			}

			// Write a temporary file and move it into place, so a failed write never leaves a half-written sidecar.
			string temporary = sidecar + ".tmp";
			File.WriteAllBytes(temporary, content);
			if (File.Exists(sidecar))
			{
				File.Replace(temporary, sidecar, null);
			}
			else
			{
				File.Move(temporary, sidecar);
			}
			return SidecarResult.Written;
		}
	}
}
