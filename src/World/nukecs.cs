// ReSharper disable InconsistentNaming
// ReSharper disable IdentifierTypo
namespace Wargon.Nukecs
{
    public struct NukEcs
    {
        // framework / save-format version — int, never float (a float version cannot be
        // compared exactly and must not leak into serialization decisions)
        public const int version = 3;
        public const string name = "Nuke.cs";
        public const string author = "Wargon";
    }
}
