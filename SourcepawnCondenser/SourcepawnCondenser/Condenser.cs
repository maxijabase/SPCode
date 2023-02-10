using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using SourcepawnCondenser.SourcemodDefinition;
using SourcepawnCondenser.Tokenizer;

namespace SourcepawnCondenser;

public partial class Condenser
{
    private static readonly char[] CommentTrimSymbols = { '/', '*', ' ', '\t' };
    private static readonly char[] SpaceTrimChars = { ' ', '\t' };
    private static readonly char[] LineSplitChars = { '\r', '\n' };

    private readonly SMDefinition _def;

    private readonly string _fileName;
    private readonly int _length;
    private readonly string _source;

    private readonly List<Token> _tokens;
    private int _position;

    private static readonly ThreadLocal<StringBuilder> ResultStringBuilder = new(() => new StringBuilder(700));

    public Condenser(string sourceCode, string fileName)
    {
        _tokens = Tokenizer.Tokenizer.TokenizeString(sourceCode, true);
        _position = 0;
        _length = _tokens.Count;
        _def = new SMDefinition();
        _source = sourceCode;
        if (fileName.EndsWith(".inc", StringComparison.InvariantCultureIgnoreCase))
        {
            fileName = fileName[..^4];
        }

        _fileName = fileName;
    }

    public SMDefinition Condense()
    {
        Token ct;
        while ((ct = _tokens[_position]).Kind != TokenKind.EOF)
        {
            var index = ct.Kind switch
            {
                TokenKind.FunctionIndicator => ConsumeSMFunction(),
                TokenKind.EnumStruct => ConsumeSMEnumStruct(),
                TokenKind.Enum => ConsumeSMEnum(),
                TokenKind.Struct => ConsumeSMStruct(),
                TokenKind.PreprocessorDirective => ConsumeSMPPDirective(),
                TokenKind.Constant => ConsumeSMConstant(),
                TokenKind.MethodMap => ConsumeSMMethodmap(),
                TokenKind.TypeSet => ConsumeSMTypeset(),
                TokenKind.TypeDef => ConsumeSMTypedef(),
                TokenKind.Identifier => ConsumeSMIdentifier(),
                _ => -1,
            };
            if (index != -1)
            {
                _position = index + 1;
                continue;
            }

            ++_position;
        }

        _def.Sort();
        return _def;
    }

    private int BacktraceTestForToken(int startPosition, TokenKind testKind, bool ignoreEol, bool ignoreOtherTokens)
    {
        for (var i = startPosition; i >= 0; --i)
        {
            if (_tokens[i].Kind == testKind)
            {
                return i;
            }

            if (ignoreOtherTokens)
            {
                continue;
            }

            if (_tokens[i].Kind == TokenKind.EOL && ignoreEol)
            {
                continue;
            }

            return -1;
        }

        return -1;
    }

    private int FortraceTestForToken(int startPosition, TokenKind testKind, bool ignoreEol, bool ignoreOtherTokens)
    {
        for (var i = startPosition; i < _length; ++i)
        {
            if (_tokens[i].Kind == testKind)
            {
                return i;
            }

            if (ignoreOtherTokens)
            {
                continue;
            }

            if (_tokens[i].Kind == TokenKind.EOL && ignoreEol)
            {
                continue;
            }

            return -1;
        }

        return -1;
    }

    private static string TrimComments(ReadOnlySpan<char> comment)
    {
        if (comment.Length == 0) return string.Empty;

        var outString = ResultStringBuilder.Value!;
        outString.Clear();
        var lines = comment.Split(LineSplitChars);
        int i = 0;
        foreach (var lineSplitEntry in lines)
        {
            var line = lineSplitEntry.Line.Trim().TrimStart(CommentTrimSymbols);
            if (!line.IsWhiteSpace())
            {
                if (i > 0)
                {
                    outString.AppendLine();
                }

                if (line.StartsWith("@param"))
                {
                    AppendParamLine(outString, line);
                }
                else
                {
                    outString.Append(line);
                }
            }

            i++;
        }

        return outString.ToString();
    }

    private static string TrimFullname(ReadOnlySpan<char> name)
    {
        var outString = ResultStringBuilder.Value!;
        outString.Clear();
        int i = 0;

        foreach (var lineEntry in name.Split(LineSplitChars))
        {
            var line = lineEntry.Line;
            if (!line.IsWhiteSpace())
            {
                if (i > 0)
                {
                    outString.Append(' ');
                }

                outString.Append(line.Trim(SpaceTrimChars));
            }

            i++;
        }

        return outString.ToString();
    }


    private static void AppendParamLine(StringBuilder builder, ReadOnlySpan<char> line)
    {
        var firstSpace = line.IndexOf(' ');

        if (firstSpace == -1) return;

        var paramInfo = line[(firstSpace + 1)..];
        var secondSpace = paramInfo.IndexOf(' ');
        if (secondSpace == -1) return;

        var paramName = paramInfo[..secondSpace];
        var paramDesc = paramInfo[secondSpace..].Trim(SpaceTrimChars);
        const string param = "@param";

        var index = 0;
        var leftSideLength = param.Length + paramName.Length + 1;
        if (leftSideLength < 24) leftSideLength = 24;
        Span<char> result = stackalloc char[leftSideLength + paramDesc.Length];
        foreach (var t in param)
        {
            result[index++] = t;
        }

        result[index++] = ' ';
        foreach (var t in paramName)
        {
            result[index++] = t;
        }

        while (index < 24)
        {
            result[index++] = ' ';
        }

        foreach (var t in paramDesc)
        {
            result[index++] = t;
        }

        builder.Append(result);
    }

    private int ConsumeSMIdentifier()
    {
        var index = ConsumeSMVariable();
        return index == -1 ? ConsumeSMFunction() : index;
    }
}