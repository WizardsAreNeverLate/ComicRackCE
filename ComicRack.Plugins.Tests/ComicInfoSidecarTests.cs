using System;
using System.IO;
using System.Threading.Tasks;
using ComicInfoSidecarPlugin;
using cYo.Projects.ComicRack.Engine;
using cYo.Projects.ComicRack.Engine.IO.Provider;

namespace ComicRack.Plugins.Tests;

public class ComicInfoSidecarTests
{
	[Test]
	public async Task ComicRack_reads_back_the_sidecar_written_for_a_pdf()
	{
		using var folder = new TempFolder();
		string pdf = folder.File("Saga 001.pdf", "%PDF-1.4\n");
		var book = new ComicBook
		{
			FilePath = pdf,
			Series = "Saga",
			Number = "1",
			Volume = 2012,
			Writer = "Brian K. Vaughan",
			Penciller = "Fiona Staples",
			Summary = "Line one\nLine two",
			CommunityRating = 4.5f
		};

		SidecarResult result = ComicInfoSidecarPlugin.ComicInfoSidecarPlugin.WriteSidecar(book);
		ComicInfo read;
		using (ImageProvider provider = Providers.Readers.CreateSourceProvider(pdf))
		{
			read = ((IInfoStorage)provider).LoadInfo(InfoLoadingMethod.Complete);
		}

		await Assert.That(result).IsEqualTo(SidecarResult.Written);
		await Assert.That(File.Exists(pdf + ".xml")).IsTrue();
		await Assert.That(read).IsNotNull();
		await Assert.That(read.Series).IsEqualTo("Saga");
		await Assert.That(read.Number).IsEqualTo("1");
		await Assert.That(read.Volume).IsEqualTo(2012);
		await Assert.That(read.Writer).IsEqualTo("Brian K. Vaughan");
		await Assert.That(read.Penciller).IsEqualTo("Fiona Staples");
		await Assert.That(read.Summary).IsEqualTo("Line one\nLine two");
		await Assert.That(read.CommunityRating).IsEqualTo(4.5f);
	}

	[Test]
	public async Task Writing_the_same_metadata_again_leaves_the_sidecar_alone()
	{
		using var folder = new TempFolder();
		var book = new ComicBook { FilePath = folder.File("book.pdf", "%PDF-1.4\n"), Series = "Saga" };
		ComicInfoSidecarPlugin.ComicInfoSidecarPlugin.WriteSidecar(book);
		DateTime first = File.GetLastWriteTimeUtc(book.FilePath + ".xml");

		SidecarResult again = ComicInfoSidecarPlugin.ComicInfoSidecarPlugin.WriteSidecar(book);

		await Assert.That(again).IsEqualTo(SidecarResult.Unchanged);
		await Assert.That(File.GetLastWriteTimeUtc(book.FilePath + ".xml")).IsEqualTo(first);
	}

	[Test]
	[Arguments("book.cbz")]
	[Arguments("")]
	public async Task Books_that_are_not_linked_pdfs_are_skipped(string fileName)
	{
		using var folder = new TempFolder();
		string path = fileName.Length == 0 ? string.Empty : folder.File(fileName, "PK");
		var book = new ComicBook { FilePath = path, Series = "Saga" };

		SidecarResult result = ComicInfoSidecarPlugin.ComicInfoSidecarPlugin.WriteSidecar(book);

		await Assert.That(result).IsEqualTo(SidecarResult.Skipped);
		await Assert.That(Directory.GetFiles(folder.Path, "*.xml")).IsEmpty();
	}

	private sealed class TempFolder : IDisposable
	{
		public TempFolder()
		{
			Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ComicInfoSidecarTests", Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(Path);
		}

		public string Path { get; }

		public string File(string name, string content)
		{
			string file = System.IO.Path.Combine(Path, name);
			System.IO.File.WriteAllText(file, content);
			return file;
		}

		public void Dispose()
		{
			try
			{
				Directory.Delete(Path, recursive: true);
			}
			catch (IOException)
			{
			}
		}
	}
}
