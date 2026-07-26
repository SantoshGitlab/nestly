namespace backend.shared.Application.Domain
{
    public class Slot : Entity<Guid>
    {
        public DateTime StartTime { get; private set; }
        public DateTime EndTime { get; private set; }
        public int Capacity { get; private set; }
        public bool IsBlackout { get; private set; }

        public void SetStartTime(DateTime startTime) => StartTime = startTime;
        public void SetEndTime(DateTime endTime) => EndTime = endTime;
        public void SetCapacity(int capacity) => Capacity = capacity > 0 ? capacity : throw new ArgumentOutOfRangeException(nameof(capacity));
        public void SetBlackout(bool isBlackout) => IsBlackout = isBlackout;
    }
}
