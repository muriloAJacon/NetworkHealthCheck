namespace NetworkHealthCheck {
	public class Settings {

		public TimeSpan INITIAL_TIMEOUT { get; init; }
		public TimeSpan HEALTHY_DELAY { get; init; }
		public TimeSpan UNHEALTHY_DELAY { get; init; }
		public string HEALTH_CHECK_URL { get; init; } = null!;
		public TimeSpan HEALTH_CHECK_TIMEOUT { get; init; }
		public uint HEALTH_CHECK_RESPONSE_CODE { get; init; }
		public bool DNS_CACHE_ENABLED { get; init; }
		public TimeSpan DNS_CACHE_DURATION { get; init; }
		public string? DISCORD_WEBHOOK_URL { get; init; }
		public string? DISCORD_MESSAGE_TEMPLATE { get; init; }
		public string? DISCORD_JSON_TEMPLATE { get; init; }
		public string DATE_FORMAT { get; init; } = null!;
		public string TZ { get; init; } = null!;
	}
}
