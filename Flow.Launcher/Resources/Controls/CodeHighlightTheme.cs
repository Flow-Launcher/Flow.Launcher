using System.Windows.Media;

namespace Flow.Launcher.Resources.Controls;

internal sealed record CodeHighlightTheme(
    string Name,
    Color DefaultForeground,
    Color Comment,
    Color String,
    Color Number,
    Color Keyword,
    Color ControlFlow,
    Color Type,
    Color Function,
    Color Variable,
    Color BuiltIn,
    Color Punctuation,
    Color Decorator,
    Color Error)
{
    public bool TryGetColor(string highlightName, out Color color)
    {
        color = highlightName switch
        {
            "Comment" or "Comments" or "LineComment" or "BlockComment"
                or "DocComment" or "DocString" or "DocText" => Comment,

            "String" or "Strings" or "StringDQ" or "StringSQ" or "RawString"
                or "TripleQuotedString" or "InterpolatedString" or "MultilineString"
                or "Char" or "Character" => String,

            "Number" or "Numbers" or "NumberLiteral" or "Digits"
                or "HexNumber" or "HexLiteral" or "OctalNumber" or "BinaryNumber"
                or "FloatNumber" or "DecimalNumber" => Number,

            "Keyword" or "Keywords" or "AccessKeywords" or "TypeKeywords"
                or "OperatorKeywords" or "ContextKeywords" or "MethodKeywords"
                or "ReturnKeyword" or "ImportKeyword" or "StorageType" or "Modifier"
                or "Modifiers" or "Visibility" or "NamespaceKeywords"
                or "GetSetAddRemove" or "ParameterModifiers" or "CheckedKeyword"
                or "UnsafeKeywords" or "SemanticKeywords"
                or "JavaScriptKeyWords" or "JavaScriptKeywords"
                or "Storage" or "ReservedWord" => Keyword,

            "ControlKeyword" or "ConditionalSelectionKeywords" or "LoopKeywords"
                or "ExceptionKeywords" or "ExceptionHandlingKeywords" or "GotoKeywords"
                or "FlowControl" or "ControlFlow" => ControlFlow,

            "Boolean" or "BooleanConstants" or "NullValue" or "Builtin" or "Builtins"
                or "BuiltinFunction" or "BuiltinType" or "BuiltInTypes" or "Self"
                or "This" or "True" or "False" or "None"
                or "ThisOrBaseReference" or "NullOrValueKeywords" or "TrueFalse"
                or "JavaScriptLiterals" or "JavaScriptIntrinsics" => BuiltIn,

            "Type" or "Types" or "TypeName" or "Class" or "ClassName"
                or "ReferenceTypes" or "ValueTypes" or "ValueTypeKeywords"
                or "ReferenceTypeKeywords" or "Interface" or "Enum"
                or "Struct" or "Trait" or "Generic" => Type,

            "MethodCall" or "Method" or "MethodName" or "Function" or "Functions"
                or "FunctionName" or "FunctionDeclaration" or "FunctionDefinition"
                or "MethodDeclaration" or "MethodDefinition"
                or "JavaScriptGlobalFunctions" => Function,

            "Variable" or "Variables" or "Parameter" or "Parameters"
                or "Property" or "Field" or "Identifier" or "Argument" => Variable,

            "Decorator" or "Annotation" or "Preprocessor" or "PreprocessorDirective"
                or "Attribute" or "Directive" or "Pragma" or "StringInterpolation"
                or "Regex" => Decorator,

            "Operator" or "Operators" or "Punctuation" or "Bracket" or "Brackets"
                or "Semicolon" or "Comma" or "Dot" => Punctuation,

            "Error" or "BraceMismatch" or "InvalidIdentifier" or "Invalid" => Error,

            _ => default,
        };
        return color != default;
    }

    public static CodeHighlightTheme VSCodeDarkPlus { get; } = new(
        Name: "VS Code Dark+",
        DefaultForeground: Color.FromRgb(0xD4, 0xD4, 0xD4),
        Comment:           Color.FromRgb(0x6A, 0x99, 0x55),
        String:            Color.FromRgb(0xCE, 0x91, 0x78),
        Number:            Color.FromRgb(0xB5, 0xCE, 0xA8),
        Keyword:           Color.FromRgb(0x56, 0x9C, 0xD6),
        ControlFlow:       Color.FromRgb(0xC5, 0x86, 0xC0),
        Type:              Color.FromRgb(0x4E, 0xC9, 0xB0),
        Function:          Color.FromRgb(0xDC, 0xDC, 0xAA),
        Variable:          Color.FromRgb(0x9C, 0xDC, 0xFE),
        BuiltIn:           Color.FromRgb(0x56, 0x9C, 0xD6),
        Punctuation:       Color.FromRgb(0xD4, 0xD4, 0xD4),
        Decorator:         Color.FromRgb(0xDC, 0xDC, 0xAA),
        Error:             Color.FromRgb(0xF4, 0x47, 0x47));

    public static CodeHighlightTheme CatppuccinMacchiato { get; } = new(
        Name: "Catppuccin Macchiato",
        DefaultForeground: Color.FromRgb(0xCA, 0xD3, 0xF5),
        Comment:           Color.FromRgb(0x6E, 0x73, 0x8D),
        String:            Color.FromRgb(0xA6, 0xDA, 0x95),
        Number:            Color.FromRgb(0xF5, 0xA9, 0x7F),
        Keyword:           Color.FromRgb(0xC6, 0xA0, 0xF6),
        ControlFlow:       Color.FromRgb(0xC6, 0xA0, 0xF6),
        Type:              Color.FromRgb(0xEE, 0xD4, 0x9F),
        Function:          Color.FromRgb(0x8A, 0xAD, 0xF4),
        Variable:          Color.FromRgb(0xCA, 0xD3, 0xF5),
        BuiltIn:           Color.FromRgb(0x91, 0xD7, 0xE3),
        Punctuation:       Color.FromRgb(0xB8, 0xC0, 0xE0),
        Decorator:         Color.FromRgb(0xC6, 0xA0, 0xF6),
        Error:             Color.FromRgb(0xED, 0x87, 0x96));

    public static CodeHighlightTheme OneDark { get; } = new(
        Name: "One Dark",
        DefaultForeground: Color.FromRgb(0xAB, 0xB2, 0xBF),
        Comment:           Color.FromRgb(0x7F, 0x84, 0x8E),
        String:            Color.FromRgb(0x98, 0xC3, 0x79),
        Number:            Color.FromRgb(0xD1, 0x9A, 0x66),
        Keyword:           Color.FromRgb(0xC6, 0x78, 0xDD),
        ControlFlow:       Color.FromRgb(0xC6, 0x78, 0xDD),
        Type:              Color.FromRgb(0xE5, 0xC0, 0x7B),
        Function:          Color.FromRgb(0x61, 0xAF, 0xEF),
        Variable:          Color.FromRgb(0xAB, 0xB2, 0xBF),
        BuiltIn:           Color.FromRgb(0x56, 0xB6, 0xC2),
        Punctuation:       Color.FromRgb(0xAB, 0xB2, 0xBF),
        Decorator:         Color.FromRgb(0xC6, 0x78, 0xDD),
        Error:             Color.FromRgb(0xE0, 0x6C, 0x75));
}
