#if WINDOWS
using System.Runtime.InteropServices;
#endif

namespace AvatarSpike;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		var window = new Window(new MainPage()) { Title = "AvatarSpike" };

#if WINDOWS
		window.Created += (s, e) => PositionAsDesktopMascot(window);
#endif

		return window;
	}

#if WINDOWS
	private const int GWL_STYLE = -16;
	private const int GWL_EXSTYLE = -20;
	private const long WS_POPUP = 0x80000000L;
	private const long WS_VISIBLE = 0x10000000L;
	private const long WS_EX_LAYERED = 0x00080000L;
	private const uint SWP_NOMOVE = 0x0002;
	private const uint SWP_NOSIZE = 0x0001;
	private const uint SWP_NOZORDER = 0x0004;
	private const uint SWP_FRAMECHANGED = 0x0020;

	[DllImport("user32.dll", SetLastError = true)]
	private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

	[DllImport("user32.dll", SetLastError = true)]
	private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

	[DllImport("user32.dll")]
	private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

	[StructLayout(LayoutKind.Sequential)]
	private struct Margins
	{
		public int Left, Right, Top, Bottom;
	}

	[DllImport("dwmapi.dll")]
	private static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref Margins margins);

	// Ventana estilo "mascota de escritorio": sin bordes, siempre encima,
	// chica, pegada a la esquina inferior derecha (arriba de la bandeja/reloj).
	private static void PositionAsDesktopMascot(Window window)
	{
		var nativeWindow = window.Handler?.PlatformView as Microsoft.UI.Xaml.Window;
		if (nativeWindow == null) return;

		var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(nativeWindow);
		var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
		var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

		const int width = 280;
		const int height = 360;
		const int margin = 12;

		if (appWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
		{
			presenter.IsAlwaysOnTop = true;
			presenter.IsResizable = false;
			presenter.IsMaximizable = false;
			presenter.IsMinimizable = false;
			presenter.SetBorderAndTitleBar(false, false);
		}

		// La API de alto nivel deja un borde residual (dibujado por DWM) — se
		// saca del todo convirtiendo la ventana a WS_POPUP puro a bajo nivel.
		var style = new IntPtr(WS_POPUP | WS_VISIBLE);
		SetWindowLongPtr(hwnd, GWL_STYLE, style);

		// WS_EX_LAYERED + DwmExtendFrameIntoClientArea("sheet of glass", márgenes -1)
		// es la combinación que le permite a DWM componer transparencia real por
		// píxel en toda el área cliente — necesario para que el fondo transparente
		// del WebView2 realmente muestre el escritorio detrás, no solo negro.
		var exStyle = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
		SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(exStyle | WS_EX_LAYERED));

		var glassMargins = new Margins { Left = -1, Right = -1, Top = -1, Bottom = -1 };
		DwmExtendFrameIntoClientArea(hwnd, ref glassMargins);

		SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0,
			SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED);

		appWindow.Resize(new Windows.Graphics.SizeInt32(width, height));

		var displayArea = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(
			windowId, Microsoft.UI.Windowing.DisplayAreaFallback.Primary);
		var workArea = displayArea.WorkArea; // Excluye la barra de tareas

		int x = workArea.X + workArea.Width - width - margin;
		int y = workArea.Y + workArea.Height - height - margin;
		appWindow.Move(new Windows.Graphics.PointInt32(x, y));
	}
#endif
}
