using System;

namespace cYo.Projects.ComicRack.Plugins
{
	[Flags]
	public enum PluginHook
	{
		None = 0,
		Library = 1 << 0,
		Books = 1 << 1,
		NewBooks = 1 << 2,
		Editor = 1 << 3,
		ParseComicPath = 1 << 4,
		BookOpened = 1 << 5,
		CreateBookList = 1 << 6,
		ReaderResized = 1 << 7,
		Search = 1 << 8,
		Startup = 1 << 9,
		Shutdown = 1 << 10,
		ComicInfoHtml = 1 << 11,
		QuickOpenHtml = 1 << 12,
		ComicInfoUI = 1 << 13,
		QuickOpenUI = 1 << 14,
		DrawThumbnailOverlay = 1 << 15
	}
}
