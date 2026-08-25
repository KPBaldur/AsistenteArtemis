namespace AvatarSpike;

public partial class MainPage : ContentPage
{
	public MainPage()
	{
		InitializeComponent();

#if WINDOWS
		Background = null;
		blazorWebView.HandlerChanged += (s, e) => TryMakeWebViewTransparent();
#endif
	}

#if WINDOWS
	private void TryMakeWebViewTransparent()
	{
		if (blazorWebView.Handler?.PlatformView is Microsoft.UI.Xaml.Controls.WebView2 nativeWebView)
		{
			nativeWebView.DefaultBackgroundColor = Microsoft.UI.Colors.Transparent;
		}
	}
#endif
}
