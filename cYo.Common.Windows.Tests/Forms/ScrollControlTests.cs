using System;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using cYo.Common.Windows.Forms;

namespace cYo.Common.Windows.Tests.Forms;

// Regression for maforget/ComicRackCE#114: a scroll-down message reaching a scroll bar that
// ScrollControl had hidden because the content fits crashed with ArgumentOutOfRangeException
// ('-2147483648' is not valid for 'Value').
public class ScrollControlTests
{
	private const int WM_HSCROLL = 0x0114;
	private const int WM_VSCROLL = 0x0115;
	private const int SB_LINEDOWN = 1;
	private const int SB_PAGEDOWN = 3;

	[DllImport("user32.dll")]
	private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

	[Test]
	[Arguments(WM_VSCROLL, SB_LINEDOWN)]
	[Arguments(WM_VSCROLL, SB_PAGEDOWN)]
	[Arguments(WM_HSCROLL, SB_LINEDOWN)]
	[Arguments(WM_HSCROLL, SB_PAGEDOWN)]
	public async Task Scrolling_down_a_scroll_bar_hidden_because_the_content_fits_does_not_throw(int message, int scrollCode)
	{
		var thrown = await OnStaThread(() =>
		{
			Exception caught = null;
			ThreadExceptionEventHandler handler = (s, e) => caught = e.Exception;
			Application.ThreadException += handler;
			try
			{
				using var control = new ScrollControl { Size = new Size(200, 200) };
				control.CreateControl();
				control.VirtualSize = new Size(1000, 1000);
				control.VirtualSize = new Size(50, 50);

				ScrollBar bar = message == WM_VSCROLL
					? control.Controls.OfType<VScrollBar>().Single()
					: control.Controls.OfType<HScrollBar>().Single();
				SendMessage(control.Handle, message, (IntPtr)scrollCode, bar.Handle);

				return caught;
			}
			finally
			{
				Application.ThreadException -= handler;
			}
		});

		await Assert.That(thrown).IsNull();
	}

	private static Task<T> OnStaThread<T>(Func<T> body)
	{
		var result = new TaskCompletionSource<T>();
		var thread = new Thread(() =>
		{
			try
			{
				result.SetResult(body());
			}
			catch (Exception e)
			{
				result.SetException(e);
			}
		});
		thread.SetApartmentState(ApartmentState.STA);
		thread.Start();
		return result.Task;
	}
}
