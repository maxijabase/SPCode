namespace SourcepawnCondenser.Tokenizer
{
    public sealed class Token
    {
        public Token(string value, TokenKind kind, int index)
        {
            Value = value;
            Kind = kind;
            Index = index;
            Length = value.Length;
        }
        public Token(char value, TokenKind kind, int index)
        {
            Value = value.ToString();
            Kind = kind;
            Index = index;
            Length = 1;
        }
        public readonly string Value;
        public readonly TokenKind Kind;
        public readonly int Index;
        public readonly int Length;
    }
}