namespace gateway.RabbitMq
{
    public class CircuitBreakerOptions
    {
        public int FailureThreshold { get; set; } = 2; 
        public int TimeoutSeconds { get; set; } = 20; 
    }
    public class CircuitBreaker
    {
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
