using System.Globalization;

namespace Glass.Widgets.BuiltIn;

public static class CalculatorExpression
{
    public static double Evaluate(string expression)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expression);
        var parser = new Parser(expression);
        var value = parser.ParseExpression();
        parser.SkipWhitespace();
        if (!parser.AtEnd) throw new FormatException("Unexpected input.");
        if (!double.IsFinite(value)) throw new ArithmeticException("The result is not finite.");
        return value;
    }

    private sealed class Parser(string text)
    {
        private int _position;
        public bool AtEnd => _position == text.Length;

        public double ParseExpression()
        {
            var value = ParseTerm();
            while (true)
            {
                SkipWhitespace();
                if (Take('+')) value += ParseTerm();
                else if (Take('-')) value -= ParseTerm();
                else return value;
            }
        }

        public void SkipWhitespace()
        {
            while (_position < text.Length && char.IsWhiteSpace(text[_position])) _position++;
        }

        private double ParseTerm()
        {
            var value = ParseUnary();
            while (true)
            {
                SkipWhitespace();
                if (Take('*')) value *= ParseUnary();
                else if (Take('/'))
                {
                    var divisor = ParseUnary();
                    if (divisor == 0) throw new DivideByZeroException();
                    value /= divisor;
                }
                else return value;
            }
        }

        private double ParseUnary()
        {
            SkipWhitespace();
            if (Take('+')) return ParseUnary();
            if (Take('-')) return -ParseUnary();
            var value = ParsePrimary();
            SkipWhitespace();
            while (Take('%')) value /= 100d; // Percent is postfix divide-by-one-hundred.
            return value;
        }

        private double ParsePrimary()
        {
            SkipWhitespace();
            if (Take('('))
            {
                var value = ParseExpression();
                SkipWhitespace();
                if (!Take(')')) throw new FormatException("Missing closing parenthesis.");
                return value;
            }

            var start = _position;
            while (_position < text.Length &&
                (char.IsDigit(text[_position]) || text[_position] is '.' or ',')) _position++;
            if (start == _position || !double.TryParse(
                    text[start.._position],
                    NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands,
                    CultureInfo.InvariantCulture,
                    out var number))
                throw new FormatException("A number was expected.");
            return number;
        }

        private bool Take(char value)
        {
            if (_position >= text.Length || text[_position] != value) return false;
            _position++;
            return true;
        }
    }
}
