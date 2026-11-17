using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace ContainerGenerator {
    // A statement of an installer copied into the harvest: where its text stands in the copy and the original syntax.
    // A text copied as it is maps every position back; a qualified one maps to the whole statement.
    internal sealed class HarvestSegment {
        private readonly int _start;
        private readonly int _length;
        private readonly SyntaxNode _original;
        private readonly bool _verbatim;

        public HarvestSegment(int start, int length, SyntaxNode original, bool verbatim) {
            _start = start;
            _length = length;
            _original = original;
            _verbatim = verbatim;
        }

        public bool Contains(int position) {
            return position >= _start && position < _start + _length;
        }

        public Location Map(TextSpan span) {
            if (_verbatim == false || span.End > _start + _length)
                return _original.GetLocation();

            var start = _original.SpanStart + span.Start - _start;
            return Location.Create(_original.SyntaxTree, new TextSpan(start, span.Length));
        }
    }
}
