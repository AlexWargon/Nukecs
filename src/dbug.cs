using System;
using System.Runtime.CompilerServices;
using Unity.Burst;

namespace Wargon.Nukecs
{
    /// <summary>
    /// Destination of framework log output. Nukecs.Unity installs one that forwards to
    /// UnityEngine.Debug; without a host, messages go to the console.
    /// </summary>
    public interface INukecsLogger
    {
        void Log(object message);
        /// <param name="color">Host-specific color value (UnityEngine.Color, or a rich-text color name/#hex string).</param>
        void LogColored(string message, object color);
        void Warning(string message);
        void Error(string message);
    }

    public sealed class ConsoleNukecsLogger : INukecsLogger
    {
        public void Log(object message) => Console.WriteLine(message);
        public void LogColored(string message, object color) => Console.WriteLine(message);
        public void Warning(string message) => Console.WriteLine("[warning] " + message);
        public void Error(string message) => Console.Error.WriteLine(message);
    }

    public static class dbug
    {
        private static INukecsLogger _logger = new ConsoleNukecsLogger();

        public static INukecsLogger Logger
        {
            get => _logger;
            set => _logger = value ?? new ConsoleNukecsLogger();
        }

        [BurstDiscard]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void log(object massage)
        {
            _logger.Log(massage);
        }
        [BurstDiscard]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void log(string message, object color)
        {
            _logger.LogColored(message, color);
        }
        [BurstDiscard]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void log(string massage)
        {
            //CustomConsoleWindow.AddMessage(massage);
            _logger.Log(massage);
        }
        [BurstDiscard]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void error(string massage)
        {
            _logger.Error(massage);
        }
        [BurstDiscard]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void error_no_componnet<T>(Entity entity)
        {
            _logger.Error($"entity: {entity.id}, has no componnet {typeof(T).Name}" );
        }
        [BurstDiscard]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void warn(string massage)
        {
            _logger.Warning(massage);
        }
    }
}
