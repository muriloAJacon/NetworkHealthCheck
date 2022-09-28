using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text;
using System.Text.Json;

namespace NetworkHealthCheck {
	public class HealthCheck : BackgroundService {
		private readonly ILogger<HealthCheck> _logger;
		private readonly Settings _settings;

		private DateTime _lastStatusChange;
		private static HealthStatus? _currentStatus;

		private HealthStatus? CurrentStatus {
			get => _currentStatus;
			set {
				if (_currentStatus != value) {
					_ = HandleNewStatusAsync(_currentStatus, value, _lastStatusChange);
					_lastStatusChange = DateTime.UtcNow;
					_currentStatus = value;
				}
			}
		}

		public static HealthStatus? PublicStatus {
			get => _currentStatus;
		}

		public HealthCheck(ILogger<HealthCheck> logger, IOptions<Settings> settings) {
			_logger = logger;
			_settings = settings.Value;
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
			await Task.Delay(_settings.INITIAL_TIMEOUT, stoppingToken);
			while (!stoppingToken.IsCancellationRequested) {
				await CheckHealthAsync();

				TimeSpan delay = CurrentStatus == HealthStatus.Healthy ? _settings.HEALTHY_DELAY : _settings.UNHEALTHY_DELAY;
				await Task.Delay(delay, stoppingToken);
			}
		}

		private async Task CheckHealthAsync() {
			bool result = await MakeRequestAsync(false);

			if (!result && _settings.DNS_CACHE_ENABLED) {
				result = await MakeRequestAsync(true);
			}

			CurrentStatus = result ? HealthStatus.Healthy : HealthStatus.Unhealthy;

			_logger.LogDebug("Health Check status: {status}", CurrentStatus);
		}

		private async Task<bool> MakeRequestAsync(bool ignoreDnsCache) {
			Uri uri = await GetUriAsync(ignoreDnsCache);

			HttpClient client = new() {
				Timeout = _settings.HEALTH_CHECK_TIMEOUT
			};

			try {
				var response = await client.GetAsync(uri);

				return (uint)response.StatusCode == _settings.HEALTH_CHECK_RESPONSE_CODE;
			} catch {
				return false;
			}
		}

		private static string? lastIP;
		private static DateTime lastIPUpdate;

		private async Task<Uri> GetUriAsync(bool ignoreDnsCache) {
			try {
				var uri = new UriBuilder(_settings.HEALTH_CHECK_URL);

				if (!_settings.DNS_CACHE_ENABLED || ignoreDnsCache) {
					return uri.Uri;
				}

				string ip;
				if (lastIPUpdate.Add(_settings.DNS_CACHE_DURATION) < DateTime.UtcNow || lastIP == null) {
					ip = (await Dns.GetHostAddressesAsync(uri.Host)).First().ToString();
					lastIPUpdate = DateTime.UtcNow;
					lastIP = ip;
				} else {
					ip = lastIP;
				}

				uri.Host = ip;

				return uri.Uri;
			} catch {
				return new Uri(_settings.HEALTH_CHECK_URL);
			}
		}

		private async Task HandleNewStatusAsync(HealthStatus? previousStatus, HealthStatus? newStatus, DateTime lastStatusChange) {
			string previousStatusString = previousStatus?.ToString() ?? "null";
			string newStatusString = newStatus?.ToString() ?? "null";
			_logger.LogInformation("Health Status changed from {previousStatus} to {newStatus} at {time}", previousStatusString, newStatusString, DateTime.UtcNow.ToString("s"));

			if (previousStatus == HealthStatus.Unhealthy && newStatus == HealthStatus.Healthy) {
				if (_settings.DISCORD_WEBHOOK_URL is not null) {
					await SendNotificationDiscordAsync(lastStatusChange);
				}
			}
		}

		private async Task SendNotificationDiscordAsync(DateTime lastStatusChange) {
			try {
				string jsonContent;
				if (!string.IsNullOrWhiteSpace(_settings.DISCORD_JSON_TEMPLATE)) {
					jsonContent = _settings.DISCORD_JSON_TEMPLATE ?? "";
				} else if (!string.IsNullOrWhiteSpace(_settings.DISCORD_MESSAGE_TEMPLATE)) {
					var jsonPayload = new {
						username = "Internet Warning",
						embeds = new[] {
							new {
								title = "Internet problem detected",
								description = _settings.DISCORD_MESSAGE_TEMPLATE,
								color = 15477290
							}
						}
					};
					jsonContent = JsonSerializer.Serialize(jsonPayload);
				} else {
					_logger.LogWarning("Neither {JSON} or {MESSAGE} variables set.", nameof(_settings.DISCORD_JSON_TEMPLATE), nameof(_settings.DISCORD_MESSAGE_TEMPLATE));
					return;
				}

				string lastStatusChangeString = TimeZoneInfo.ConvertTimeFromUtc(lastStatusChange, TimeZoneInfo.FindSystemTimeZoneById(_settings.TZ)).ToString(_settings.DATE_FORMAT);
				string nowString = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById(_settings.TZ)).ToString(_settings.DATE_FORMAT);

				jsonContent = jsonContent.Replace("{FailTime}", lastStatusChangeString).Replace("{RecoverTime}", nowString);

				HttpClient client = new();
				HttpRequestMessage request = new(HttpMethod.Post, _settings.DISCORD_WEBHOOK_URL);

				request.Content = new StringContent(jsonContent, Encoding.Unicode, "application/json");

				var response = await client.SendAsync(request);

				if (!response.IsSuccessStatusCode) {
					string responseStr = await response.Content.ReadAsStringAsync();
					_logger.LogError("Failed to send notification to Discord, response: {Response}", responseStr);
				}
			} catch (Exception e) {
				_logger.LogError(e, "Failed to send notification to Discord");
			}
		}
	}
}
