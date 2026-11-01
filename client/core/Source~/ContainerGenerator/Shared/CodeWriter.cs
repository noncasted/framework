using System.Text;

namespace ContainerGenerator {
    internal sealed class CodeWriter {
        private readonly StringBuilder _buffer = new StringBuilder();

        public void AppendLine(string? value = null) {
            if (string.IsNullOrEmpty(value)) {
                _buffer.AppendLine();
                return;
            }

            _buffer.AppendLine(value);
        }

        public override string ToString() {
            return _buffer.ToString();
        }
    }
}
