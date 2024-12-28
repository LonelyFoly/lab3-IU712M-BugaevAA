namespace gateway.RabbitMq
{
    public class CircuitBreakerOptions
    {
        public int FailureThreshold { get; set; } = 2; 
        public int TimeoutSeconds { get; set; } = 60; 
    }
    public class CircuitBreaker
    {
        public Timer timer;
        private int _failureCount = 0;
        private readonly int _failureThreshold;
        private readonly TimeSpan _timeout;
        public DateTime _lastFailureTime = DateTime.MinValue;
        public bool _isOpen = false;

        public CircuitBreaker(int failureThreshold, TimeSpan timeout)
        {
            _failureThreshold = failureThreshold;
            _timeout = timeout;
        }

        public bool IsOpen()
        {
            if (_isOpen && DateTime.UtcNow - _lastFailureTime > _timeout)
            {
                timer.Change(TimeSpan.Zero, TimeSpan.FromDays(2));
                _isOpen = false;
                _failureCount = 0;
            }
            return _isOpen;
        }

        public void RegisterFailure()
        {
            _failureCount++;
            _lastFailureTime = DateTime.UtcNow;

            if (_failureCount >= _failureThreshold)
            {
                timer.Change(TimeSpan.Zero, TimeSpan.FromSeconds(5));
                _isOpen = true;
            }
        }

        public void RegisterSuccess()
        {
            _failureCount = 0;
            _isOpen = false;
        }
    }
}
