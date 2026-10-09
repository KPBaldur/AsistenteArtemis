using AsistenteArtemis.Client.Maui.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Plugin.Maui.Audio;

namespace AsistenteArtemis.Client.Maui;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
			});

		builder.Services.AddMauiBlazorWebView();

		builder.Services.AddSingleton<TokenStore>();
		builder.Services.AddSingleton<AuthState>();
		builder.Services.AddSingleton<AlarmNotificationService>();
		builder.Services.AddTransient<AuthDelegatingHandler>();

		// AuthApiClient carries no bearer token (login/register/refresh don't need one), so no
		// AuthDelegatingHandler here — see AuthApiClient.cs for why.
		builder.Services.AddHttpClient<IAuthApiClient, AuthApiClient>(
			c => c.BaseAddress = new Uri(ApiConfig.BaseUrl));

		builder.Services.AddHttpClient<ITaskApiClient, TaskApiClient>(
				c => c.BaseAddress = new Uri(ApiConfig.BaseUrl))
			.AddHttpMessageHandler<AuthDelegatingHandler>();

		builder.Services.AddHttpClient<IAlarmApiClient, AlarmApiClient>(
				c => c.BaseAddress = new Uri(ApiConfig.BaseUrl))
			.AddHttpMessageHandler<AuthDelegatingHandler>();

		builder.Services.AddHttpClient<INoteApiClient, NoteApiClient>(
				c => c.BaseAddress = new Uri(ApiConfig.BaseUrl))
			.AddHttpMessageHandler<AuthDelegatingHandler>();

		builder.Services.AddHttpClient<IAssistantIdentityApiClient, AssistantIdentityApiClient>(
				c => c.BaseAddress = new Uri(ApiConfig.BaseUrl))
			.AddHttpMessageHandler<AuthDelegatingHandler>();

		builder.Services.AddHttpClient<IMonitorApiClient, MonitorApiClient>(
				c => c.BaseAddress = new Uri(ApiConfig.BaseUrl))
			.AddHttpMessageHandler<AuthDelegatingHandler>();

		builder.Services.AddHttpClient<IMediaApiClient, MediaApiClient>(
				c => c.BaseAddress = new Uri(ApiConfig.BaseUrl))
			.AddHttpMessageHandler<AuthDelegatingHandler>();

		builder.Services.AddHttpClient<IDeviceTokenApiClient, DeviceTokenApiClient>(
				c => c.BaseAddress = new Uri(ApiConfig.BaseUrl))
			.AddHttpMessageHandler<AuthDelegatingHandler>();

		builder.Services.AddSingleton(AudioManager.Current);

		// Longer timeout: SendMessage calls the Brain synchronously, which calls Gemini.
		builder.Services.AddHttpClient<IAiChatApiClient, AiChatApiClient>(c =>
			{
				c.BaseAddress = new Uri(ApiConfig.BaseUrl);
				c.Timeout = TimeSpan.FromSeconds(60);
			})
			.AddHttpMessageHandler<AuthDelegatingHandler>();

#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
#endif

		var app = builder.Build();

		// AlarmNotificationService es Singleton pero nadie lo pide por constructor en ningún
		// componente — sin esto, el contenedor de DI nunca lo instancia y su conexión de
		// SignalR (que existe para mandar el toast nativo desde cualquier página) jamás arranca.
		app.Services.GetRequiredService<AlarmNotificationService>();

		return app;
	}
}
