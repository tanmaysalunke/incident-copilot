namespace IncidentCopilot.Services;

using IncidentCopilot.Models;

/// <summary>
/// Generates realistic multi-service incident log data for testing and demos.
///
/// The scenario: Cascading Payment Failure
/// - 3:05am: PostgresDB connection pool starts exhausting (slow query from bad deployment)
/// - 3:08am: AuthService times out waiting for DB connections
/// - 3:12am: PaymentService fails because Auth is down, circuit breaker opens
/// - 3:15am: APIGateway health check fails, traffic routes to backup region
///
/// This is based on a real-world pattern: a single database issue cascading
/// through a microservice dependency chain.
/// </summary>
public static class LogGenerator
{
    // Base time for the incident: March 28, 2026 at 3:00am UTC
    private static readonly DateTime BaseTime = new(2026, 3, 28, 3, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Generate the complete incident scenario across all four services.
    /// Returns a dictionary mapping service name to its log entries.
    /// </summary>
    public static Dictionary<string, List<LogEntry>> GenerateIncidentScenario()
    {
        return new Dictionary<string, List<LogEntry>>
        {
            ["PostgresDB"] = GeneratePostgresLogs(),
            ["AuthService"] = GenerateAuthLogs(),
            ["PaymentService"] = GeneratePaymentLogs(),
            ["APIGateway"] = GenerateGatewayLogs()
        };
    }

    /// <summary>
    /// Generate the service dependency graph for the scenario.
    /// </summary>
    public static List<ServiceNode> GenerateServiceGraph()
    {
        return new List<ServiceNode>
        {
            new ServiceNode
            {
                Id = "APIGateway",
                UpstreamServices = new List<string>(),
                DownstreamServices = new List<string> { "PaymentService", "AuthService" },
                HealthCheckUrl = "http://api-gateway:8080/health"
            },
            new ServiceNode
            {
                Id = "PaymentService",
                UpstreamServices = new List<string> { "APIGateway" },
                DownstreamServices = new List<string> { "AuthService" },
                HealthCheckUrl = "http://payment:8081/health"
            },
            new ServiceNode
            {
                Id = "AuthService",
                UpstreamServices = new List<string> { "APIGateway", "PaymentService" },
                DownstreamServices = new List<string> { "PostgresDB" },
                HealthCheckUrl = "http://auth:8082/health"
            },
            new ServiceNode
            {
                Id = "PostgresDB",
                UpstreamServices = new List<string> { "AuthService" },
                DownstreamServices = new List<string>(),
                HealthCheckUrl = "http://postgres:5432/health"
            }
        };
    }

    private static List<LogEntry> GeneratePostgresLogs()
    {
        var logs = new List<LogEntry>();
        var service = "PostgresDB";

        // 3:00 - 3:04: Normal operations
        logs.Add(MakeEntry(0, 12, service, "INFO", "Connection pool stats: 12/50 active connections, avg query time 45ms"));
        logs.Add(MakeEntry(1, 30, service, "INFO", "Routine vacuum completed on table: orders"));
        logs.Add(MakeEntry(2, 15, service, "INFO", "Connection pool stats: 15/50 active connections, avg query time 52ms"));
        logs.Add(MakeEntry(3, 45, service, "DEBUG", "Checkpoint completed: wrote 234 buffers"));

        // 3:05 - 3:07: Problems begin (bad deployment introduced slow query)
        logs.Add(MakeEntry(5, 0, service, "INFO", "New deployment detected: orders-service v2.3.1 connected"));
        logs.Add(MakeEntry(5, 12, service, "WARN", "Connection pool utilization at 85% (42/50 connections active)"));
        logs.Add(MakeEntry(5, 30, service, "WARN", "Slow query detected: SELECT * FROM orders WHERE created_at > '2026-01-01' AND status IN ('pending','processing') ORDER BY priority DESC took 8.2s"));
        logs.Add(MakeEntry(5, 45, service, "WARN", "Connection pool utilization at 90% (45/50 connections active)"));
        logs.Add(MakeEntry(6, 10, service, "ERROR", "Connection pool utilization at 96% (48/50 connections active)"));
        logs.Add(MakeEntry(6, 30, service, "ERROR", "Lock wait timeout exceeded for transaction 0x7fa3b2c1; table: orders"));
        logs.Add(MakeEntry(6, 45, service, "ERROR", "Slow query detected: SELECT * FROM orders WHERE created_at > '2026-01-01' AND status IN ('pending','processing') ORDER BY priority DESC took 12.4s"));
        logs.Add(MakeEntry(7, 0, service, "ERROR", "Connection pool EXHAUSTED: 50/50 connections active, 12 requests queued"));
        logs.Add(MakeEntry(7, 15, service, "ERROR", "Connection acquire timeout: waited 5000ms, pool exhausted"));
        logs.Add(MakeEntry(7, 30, service, "FATAL", "Multiple connection acquire timeouts detected: 15 failures in last 60 seconds"));
        logs.Add(MakeEntry(7, 45, service, "ERROR", "Deadlock detected between transactions 0x7fa3b2c1 and 0x7fa3b2c8"));

        // 3:08 - 3:10: Continued degradation
        logs.Add(MakeEntry(8, 0, service, "ERROR", "Connection acquire timeout: waited 5000ms, pool exhausted"));
        logs.Add(MakeEntry(8, 30, service, "ERROR", "Query cancellation: 8 queries killed after exceeding 30s timeout"));
        logs.Add(MakeEntry(9, 0, service, "ERROR", "Connection pool stats: 50/50 active, 23 queued, avg wait 4200ms"));
        logs.Add(MakeEntry(9, 30, service, "WARN", "Autovacuum worker terminated: could not acquire lock on table orders"));

        // 3:10 - 3:15: Partial recovery attempts
        logs.Add(MakeEntry(10, 0, service, "INFO", "Killing long-running queries older than 30 seconds: terminated 5 backends"));
        logs.Add(MakeEntry(10, 30, service, "WARN", "Connection pool utilization at 88% (44/50) after forced termination"));
        logs.Add(MakeEntry(11, 0, service, "ERROR", "Pool re-exhausted: 50/50 connections active again"));
        logs.Add(MakeEntry(12, 0, service, "ERROR", "Slow query continues: same query pattern consuming 3 connections simultaneously"));

        return logs;
    }

    private static List<LogEntry> GenerateAuthLogs()
    {
        var logs = new List<LogEntry>();
        var service = "AuthService";

        // 3:00 - 3:07: Normal operations
        logs.Add(MakeEntry(0, 5, service, "INFO", "Token validation request from PaymentService: user_id=u-4521, latency=23ms"));
        logs.Add(MakeEntry(1, 12, service, "INFO", "Token refresh for session s-8832, new expiry: 2026-03-28T04:00:00Z"));
        logs.Add(MakeEntry(2, 30, service, "INFO", "Token validation request from APIGateway: user_id=u-1190, latency=18ms"));
        logs.Add(MakeEntry(3, 20, service, "DEBUG", "Cache hit rate: 94.2%, total cached tokens: 15,234"));
        logs.Add(MakeEntry(4, 45, service, "INFO", "Token validation request from PaymentService: user_id=u-7788, latency=21ms"));
        logs.Add(MakeEntry(5, 30, service, "INFO", "Token validation request from APIGateway: user_id=u-3344, latency=35ms"));
        logs.Add(MakeEntry(6, 15, service, "WARN", "Token validation latency increasing: P95 latency 450ms (threshold: 200ms)"));
        logs.Add(MakeEntry(7, 0, service, "WARN", "Token validation latency critical: P95 latency 2100ms (threshold: 200ms)"));

        // 3:08 - 3:10: Auth starts failing
        logs.Add(MakeEntry(8, 1, service, "ERROR", "Timeout calling PostgresDB: connection acquire timeout after 5000ms"));
        logs.Add(MakeEntry(8, 5, service, "ERROR", "Token validation failed for user_id=u-4521: upstream dependency unavailable"));
        logs.Add(MakeEntry(8, 15, service, "ERROR", "Token validation failed for user_id=u-9012: upstream dependency unavailable"));
        logs.Add(MakeEntry(8, 20, service, "ERROR", "Circuit breaker monitoring: 5 failures in 30 seconds for PostgresDB connection"));
        logs.Add(MakeEntry(8, 30, service, "ERROR", "Timeout calling PostgresDB: connection acquire timeout after 5000ms"));
        logs.Add(MakeEntry(8, 45, service, "FATAL", "Circuit breaker OPEN for PostgresDB: too many consecutive failures (threshold: 10)"));
        logs.Add(MakeEntry(9, 0, service, "ERROR", "All token validations failing: circuit breaker open for database dependency"));
        logs.Add(MakeEntry(9, 15, service, "ERROR", "Fallback: serving cached tokens only, new validations rejected"));
        logs.Add(MakeEntry(9, 30, service, "WARN", "Cache fallback active: 234 requests served from cache, 89 rejected"));

        // 3:10 - 3:15: Continued failures
        logs.Add(MakeEntry(10, 0, service, "ERROR", "Circuit breaker half-open test failed: PostgresDB still unavailable"));
        logs.Add(MakeEntry(11, 0, service, "ERROR", "Circuit breaker half-open test failed: PostgresDB still unavailable"));
        logs.Add(MakeEntry(12, 0, service, "ERROR", "Circuit breaker half-open test failed: connection timeout 5000ms"));
        logs.Add(MakeEntry(13, 0, service, "WARN", "Cache entries expiring: only 45% of tokens still cached"));
        logs.Add(MakeEntry(14, 0, service, "ERROR", "Token validation failure rate: 78% (cache misses increasing)"));

        return logs;
    }

    private static List<LogEntry> GeneratePaymentLogs()
    {
        var logs = new List<LogEntry>();
        var service = "PaymentService";

        // 3:00 - 3:11: Normal operations, then latency increases
        logs.Add(MakeEntry(0, 30, service, "INFO", "Payment processed: order_id=ord-11234, amount=$45.99, gateway=stripe, latency=234ms"));
        logs.Add(MakeEntry(1, 45, service, "INFO", "Payment processed: order_id=ord-11235, amount=$129.00, gateway=stripe, latency=198ms"));
        logs.Add(MakeEntry(3, 10, service, "INFO", "Payment processed: order_id=ord-11236, amount=$67.50, gateway=stripe, latency=210ms"));
        logs.Add(MakeEntry(5, 0, service, "INFO", "Payment processed: order_id=ord-11237, amount=$23.99, gateway=stripe, latency=245ms"));
        logs.Add(MakeEntry(7, 30, service, "WARN", "Payment processing latency increasing: avg 890ms (threshold: 500ms)"));
        logs.Add(MakeEntry(8, 30, service, "WARN", "Auth token validation slow: 2300ms for order ord-11238"));
        logs.Add(MakeEntry(9, 0, service, "ERROR", "Payment failed: order_id=ord-11239, reason=auth_timeout, amount=$55.00"));
        logs.Add(MakeEntry(9, 30, service, "ERROR", "Payment failed: order_id=ord-11240, reason=auth_service_unavailable"));
        logs.Add(MakeEntry(10, 0, service, "ERROR", "Retry attempt 1/3 for order ord-11241: AuthService returned HTTP 503"));
        logs.Add(MakeEntry(10, 15, service, "ERROR", "Retry attempt 2/3 for order ord-11241: AuthService returned HTTP 503"));
        logs.Add(MakeEntry(10, 30, service, "ERROR", "Retry attempt 3/3 for order ord-11241: AuthService returned HTTP 503"));
        logs.Add(MakeEntry(10, 31, service, "ERROR", "Payment failed after all retries: order_id=ord-11241, amount=$88.00"));

        // 3:11 - 3:14: Circuit breaker opens
        logs.Add(MakeEntry(11, 0, service, "ERROR", "Circuit breaker monitoring: 15 failures in 60 seconds for AuthService"));
        logs.Add(MakeEntry(11, 30, service, "ERROR", "HTTP 500 from AuthService on /validate-token: connection refused"));
        logs.Add(MakeEntry(12, 3, service, "ERROR", "HTTP 500 from AuthService on /validate-token (attempt 3/3)"));
        logs.Add(MakeEntry(12, 4, service, "FATAL", "Payment processing halted: circuit breaker OPEN for AuthService"));
        logs.Add(MakeEntry(12, 5, service, "FATAL", "All incoming payment requests rejected: service degraded"));
        logs.Add(MakeEntry(12, 30, service, "ERROR", "Queue depth critical: 234 unprocessed payment requests"));
        logs.Add(MakeEntry(13, 0, service, "ERROR", "Circuit breaker half-open test: AuthService still returning HTTP 503"));
        logs.Add(MakeEntry(13, 30, service, "ERROR", "Payment queue overflow: dropping oldest requests, 45 payments lost"));
        logs.Add(MakeEntry(14, 0, service, "ERROR", "Circuit breaker still OPEN: 0 successful auth calls in last 5 minutes"));

        return logs;
    }

    private static List<LogEntry> GenerateGatewayLogs()
    {
        var logs = new List<LogEntry>();
        var service = "APIGateway";

        // 3:00 - 3:11: Normal traffic
        logs.Add(MakeEntry(0, 0, service, "INFO", "Traffic stats: 1,234 req/min, P99 latency: 89ms, error rate: 0.1%"));
        logs.Add(MakeEntry(2, 0, service, "INFO", "Traffic stats: 1,189 req/min, P99 latency: 92ms, error rate: 0.1%"));
        logs.Add(MakeEntry(4, 0, service, "INFO", "Traffic stats: 1,301 req/min, P99 latency: 95ms, error rate: 0.2%"));
        logs.Add(MakeEntry(6, 0, service, "INFO", "Traffic stats: 1,245 req/min, P99 latency: 134ms, error rate: 0.3%"));
        logs.Add(MakeEntry(8, 0, service, "WARN", "Traffic stats: 1,198 req/min, P99 latency: 890ms, error rate: 2.1%"));
        logs.Add(MakeEntry(9, 0, service, "WARN", "Upstream error rate increasing: PaymentService returning 5xx for 15% of requests"));
        logs.Add(MakeEntry(10, 0, service, "ERROR", "Traffic stats: 1,102 req/min, P99 latency: 3200ms, error rate: 12.5%"));
        logs.Add(MakeEntry(11, 0, service, "ERROR", "Rate limiting activated: throttling requests to PaymentService (max 100 req/min)"));

        // 3:12 - 3:16: Health check failures and failover
        logs.Add(MakeEntry(12, 0, service, "ERROR", "Traffic stats: 987 req/min, P99 latency: 5100ms, error rate: 34.2%"));
        logs.Add(MakeEntry(12, 30, service, "ERROR", "Health check WARNING for PaymentService: 3 consecutive failures"));
        logs.Add(MakeEntry(13, 0, service, "ERROR", "Health check WARNING for AuthService: 2 consecutive failures"));
        logs.Add(MakeEntry(13, 30, service, "ERROR", "Multiple downstream services unhealthy: PaymentService, AuthService"));
        logs.Add(MakeEntry(14, 0, service, "FATAL", "Health check CRITICAL for PaymentService: 5 consecutive failures, removing from pool"));
        logs.Add(MakeEntry(14, 30, service, "FATAL", "Service degradation: 2/4 downstream services removed from load balancer pool"));
        logs.Add(MakeEntry(15, 0, service, "FATAL", "Health check failed for PaymentService. Initiating failover to backup region us-west-2b"));
        logs.Add(MakeEntry(15, 5, service, "INFO", "Failover initiated: routing traffic to backup region us-west-2b"));
        logs.Add(MakeEntry(15, 30, service, "INFO", "Failover progress: 50% of traffic now routed to us-west-2b"));
        logs.Add(MakeEntry(16, 0, service, "INFO", "Failover complete: 100% of traffic routed to us-west-2b, primary region drained"));

        return logs;
    }

    /// <summary>
    /// Helper to create a LogEntry at a specific minute:second offset from the base time.
    /// MakeEntry(5, 12, ...) = 3:05:12am
    /// </summary>
    private static LogEntry MakeEntry(int minuteOffset, int second, string service, string severity, string message)
    {
        return new LogEntry
        {
            Timestamp = BaseTime.AddMinutes(minuteOffset).AddSeconds(second),
            Service = service,
            Severity = severity,
            Message = message,
            TraceId = $"trace-{Guid.NewGuid().ToString()[..8]}",
            SpanId = $"span-{Guid.NewGuid().ToString()[..8]}"
        };
    }
}