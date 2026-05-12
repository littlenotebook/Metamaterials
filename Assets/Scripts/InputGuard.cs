namespace Microstructure
{
    /// <summary>
    /// Prevents a single left-click from being consumed by both
    /// camera drag and scene raycasts in the same frame.
    /// </summary>
    public static class InputGuard
    {
        private static int _clickConsumedFrame = -1;

        public static void ConsumeClick(int frame) => _clickConsumedFrame = frame;
        public static bool IsClickConsumed(int frame) => _clickConsumedFrame == frame;
    }
}