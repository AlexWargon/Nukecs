using System;

namespace Wargon.Nukecs
{
    public enum SystemCompilationKind
    {
        NoQuery,
        RuntimeIteration,
        PointerBatch,
        ChangedBatch
    }

    public enum BatchFallbackReason
    {
        None,
        NoQuery,
        NoBody,
        NoQueryLoop,
        MultipleQueryLoops,
        ExplicitRuntimeIterator,
        UnsupportedPattern,
        PoolComponent,
        LocalFunction,
        UnsupportedControlFlow,
        UnsupportedCapture
    }

    /// <summary>Compile-time generation metadata; it does not prove native Burst execution.</summary>
    public readonly struct SystemCompilationInfo
    {
        public readonly SystemCompilationKind Kind;
        public readonly BatchFallbackReason FallbackReason;
        public readonly bool HasSurroundingCode;
        public readonly string FallbackDetail;
        public readonly string FallbackFile;
        public readonly int FallbackLine;
        public readonly int FallbackColumn;

        public SystemCompilationInfo(SystemCompilationKind kind, BatchFallbackReason fallbackReason,
            bool hasSurroundingCode)
            : this(kind, fallbackReason, hasSurroundingCode, null, null, 0, 0)
        { }

        public SystemCompilationInfo(SystemCompilationKind kind, BatchFallbackReason fallbackReason,
            bool hasSurroundingCode, string fallbackDetail, string fallbackFile,
            int fallbackLine, int fallbackColumn)
        {
            Kind = kind;
            FallbackReason = fallbackReason;
            HasSurroundingCode = hasSurroundingCode;
            FallbackDetail = fallbackDetail;
            FallbackFile = fallbackFile;
            FallbackLine = fallbackLine;
            FallbackColumn = fallbackColumn;
        }
    }

    public interface ISystemCompilationInfoProvider
    {
        SystemCompilationInfo CompilationInfo { get; }
    }

    /// <summary>Requires source-generated pointer or Changed batch traversal (NUKECS002 on fallback).</summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class RequireBatchAttribute : Attribute { }
}
