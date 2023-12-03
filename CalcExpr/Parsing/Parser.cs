﻿using CalcExpr.Attributes;
using CalcExpr.Exceptions;
using CalcExpr.Expressions;
using CalcExpr.Parsing.Rules;
using System.Reflection;
using System.Text.RegularExpressions;

namespace CalcExpr.Parsing;

public class Parser
{
    private readonly List<Rule> _grammar = new List<Rule>();
    private readonly Dictionary<string, IExpression> _cache = new Dictionary<string, IExpression>();

    public Rule[] Grammar
        => _grammar.ToArray();

    public string[] Cache
        => _cache.Keys.ToArray();

    /// <summary>
    /// Creates a <see cref="Parser"/> with the default grammar.
    /// </summary>
    public Parser()
    {
        _grammar = new List<Rule>()
        {
            new ReferenceRegexRule("Operand", "({Prefix}*({Variable}|{Constant}|{Number}|{Token}){Postfix}*)"),
            new ReferenceRegexRule("Token", @"\[\d+\]"),
            new Rule("FunctionCall", ParseFunctionCall, MatchFunctionCall),
            new Rule("Parentheses", ParseParentheses, MatchParentheses),
            new RegexRule("WithParentheses", @"\(|\)", RegexOptions.None, ParseWithParentheses),
            new NestedRegexRule("AssignBinOp", @"(?<={Operand})(?<!!)(=)(?={Operand})",
                RegexRuleOptions.RightToLeft | RegexRuleOptions.PadReferences, ParseAssignmentOperator),
            new NestedRegexRule("OrBinOp", @"(?<={Operand})(\|\||∨)(?={Operand})",
                RegexRuleOptions.RightToLeft | RegexRuleOptions.PadReferences, ParseBinaryOperator),
            new NestedRegexRule("XorBinOp", @"(?<={Operand})(⊕)(?={Operand})",
                RegexRuleOptions.RightToLeft | RegexRuleOptions.PadReferences, ParseBinaryOperator),
            new NestedRegexRule("AndBinOp", @"(?<={Operand})(&&|∧)(?={Operand})",
                RegexRuleOptions.RightToLeft | RegexRuleOptions.PadReferences, ParseBinaryOperator),
            new NestedRegexRule("EqBinOp", @"(?<={Operand})(==|!=|<>|≠)(?={Operand})",
                RegexRuleOptions.RightToLeft | RegexRuleOptions.PadReferences, ParseBinaryOperator),
            new NestedRegexRule("IneqBinOp", @"(?<={Operand})(>=|<=|<(?!>)|(?<!<)>|[≤≥])(?={Operand})",
                RegexRuleOptions.RightToLeft | RegexRuleOptions.PadReferences, ParseBinaryOperator),
            new NestedRegexRule("AddBinOp", @"(?<={Operand})([\+\-])(?={Operand})",
                RegexRuleOptions.RightToLeft | RegexRuleOptions.PadReferences, ParseBinaryOperator),
            new NestedRegexRule("MultBinOp", @"(?<={Operand})(%%|//|[*×/÷%])(?={Operand})",
                RegexRuleOptions.RightToLeft | RegexRuleOptions.PadReferences, ParseBinaryOperator),
            new NestedRegexRule("ExpBinOp", @"(?<={Operand})(\^)(?={Operand})",
                RegexRuleOptions.RightToLeft | RegexRuleOptions.PadReferences, ParseBinaryOperator),
            new RegexRule("Prefix", @"((\+{2})|(\-{2})|[\+\-!~¬])",
                RegexRuleOptions.Left | RegexRuleOptions.TrimLeft, ParsePrefix),
            new RegexRule("Postfix", @"((\+{2})|(\-{2})|((?<![A-Za-zΑ-Ωα-ω0-9](!!)*!)!!)|[!%#])",
                RegexRuleOptions.RightToLeft | RegexRuleOptions.Right | RegexRuleOptions.TrimRight, ParsePostfix),
            new RegexRule("Constant", "(∞|(inf(inity)?)|π|pi|τ|tau|e|true|false)",
                RegexRuleOptions.Only | RegexRuleOptions.Trim, ParseConstant),
            new RegexRule("Variable", "([A-Za-zΑ-Ωα-ω]+(_[A-Za-zΑ-Ωα-ω0-9]+)*)",
                RegexRuleOptions.Only | RegexRuleOptions.Trim, ParseVariable),
            new RegexRule("Number", @"((\d+\.?\d*)|(\d*\.?\d+))", RegexRuleOptions.Only | RegexRuleOptions.Trim,
                ParseNumber)
        };
    }

    /// <summary>
    /// Create a <see cref="Parser"/> using the specified grammar.
    /// </summary>
    /// <param name="grammar">
    /// The specified <see cref="IEnumerable{Rule}"/> to be used as the grammar of the <see cref="Parser"/>.
    /// </param>
    public Parser(IEnumerable<Rule> grammar)
        => _grammar = grammar.ToList();

    /// <summary>
    /// Parses an expression <see cref="string"/> into an <see cref="IExpression"/>.
    /// </summary>
    /// <param name="input">The expression <see cref="string"/> to parse.</param>
    /// <returns>An <see cref="IExpression"/> parsed from the specified expression <see cref="string"/>.</returns>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="Exception"></exception>
    public IExpression Parse(string input)
    {
        if (input is null)
            throw new ArgumentNullException(nameof(input));

        if (ContainsCache(input))
            return _cache[CleanExpressionString(input)].Clone();

        foreach (Rule rule in _grammar)
        {
            if (rule.GetType().GetCustomAttribute<ReferenceRuleAttribute>() is not null)
                continue;

            Token? match = rule.Match(input, Grammar);

            if (match.HasValue)
            {
                IExpression expression = rule.Parse.Invoke(input, match.Value, this);

                AddCache(input, expression);
                return expression;
            }
        }

        throw new Exception($"The input was not in the correct format: '{input}'");
    }

    private static string CleanExpressionString(string expression)
        => Regex.Replace(expression, @"\s+", "");

    private static string TokenizeInput(string input, out Token[] tokens)
    {
        input = Regex.Replace(input, @"[\[\]\\]", match => @$"\{match.Value}");

        List<Token> toks = new List<Token>();
        string output = String.Empty;
        int start = -1;
        int depth = 0;

        for (int i = 0; i < input.Length; i++)
        {
            char current = input[i];

            if (current == '(')
            {
                if (start == -1)
                {
                    start = i;
                    depth = 1;
                }
                else
                {
                    depth++;
                }
            }
            else if (current == ')')
            {
                if (start < 0)
                {
                    throw new UnbalancedParenthesesException(input);
                }
                else if (depth > 1)
                {
                    depth--;
                }
                else
                {
                    output += $"[{toks.Count}]";
                    toks.Add(new Token(input[start..(i + 1)], start));
                    start = -1;
                    depth = 0;
                }
            }
            else
            {
                if (start < 0)
                    output += current;
                else if (i == input.Length - 1)
                    throw new UnbalancedParenthesesException(input);
            }
        }

        tokens = toks.ToArray();
        return output;
    }

    private static int DetokenizeIndex(int index, string tokenized_string, Token[] tokens)
    {
        Match[] matches = Regex.Matches(tokenized_string[..index], @"((?<=^|([^\\]([\\][\\])*))\[\d+\])|(\\[\[\]\\])")
            .ToArray();

        foreach (Match match in matches)
            if (match.Value.StartsWith("\\"))
                index -= 1;
            else
                index += tokens[Convert.ToInt32(match.Value[1..^1])].Length - 3;

        return index;
    }

    /// <summary>
    /// Determines whether the cache of the <see cref="Parser"/> contains a specified expression <see cref="string"/>.
    /// </summary>
    /// <param name="expression">The expression to locate in the cache.</param>
    /// <returns>
    /// <see langword="true"/> if the cache contains an entry with the specified expression; otherwise, 
    /// <see langword="false"/>.
    /// </returns>
    public bool ContainsCache(string expression)
        => _cache.ContainsKey(CleanExpressionString(expression));

    /// <summary>
    /// Add expression <see cref="string"/> to the cache of the <see cref="Parser"/>.
    /// </summary>
    /// <param name="key">The expression <see cref="string"/> of the cached <see cref="IExpression"/>.</param>
    /// <param name="value">The cached <see cref="IExpression"/>.</param>
    /// <returns>
    /// <see langword="true"/> if the <see cref="IExpression"/> was successfully added to the cache; otherwise, 
    /// <see langword="false"/>.
    /// </returns>
    public bool AddCache(string key, IExpression value)
    {
        try
        {
            _cache[CleanExpressionString(key)] = value.Clone();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Removes a cached <see cref="IExpression"/> based on the specified expression <see cref="string"/>.
    /// </summary>
    /// <param name="expression">The expression <see cref="string"/> of the cached <see cref="IExpression"/>.</param>
    /// <returns>
    /// <see langword="true"/> if the <see cref="IExpression"/> was successfully removed from the cache; otherwise,
    /// <see langword="false"/>.
    /// </returns>
    public bool RemoveCache(string expression)
    {
        string clean_expression = CleanExpressionString(expression);

        return _cache.ContainsKey(clean_expression) && _cache.Remove(clean_expression);
    }

    /// <summary>
    /// Add <see cref="Rule"/> to the grammar of the <see cref="Parser"/>.
    /// </summary>
    /// <param name="rule">The <see cref="Rule"/> to be added to the grammar.</param>
    /// <param name="index">The index to put the <see cref="Rule"/> in the grammar.</param>
    /// <returns>
    /// <see langword="true"/> if the <see cref="Rule"/> was successfully added to the grammar; otherwise, 
    /// <see langword="false"/>.
    /// </returns>
    public bool AddGrammarRule(Rule rule, int index = -1)
    {
        if (index < 0)
            index += _grammar.Count + 1;

        try
        {
            if (index <= 0)
                _grammar.Insert(0, rule);
            else if (index >= _grammar.Count)
                _grammar.Insert(_grammar.Count, rule);
            else
                _grammar.Insert(index, rule);

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Removes the <see cref="Rule"/> with the specified name from the grammar of the <see cref="Parser"/>.
    /// </summary>
    /// <param name="name">The name of the <see cref="Rule"/> to be removed.</param>
    /// <returns>
    /// <see langword="true"/> if the <see cref="Rule"/> was successfully removed; otherwise, <see langword="false"/>.
    /// </returns>
    public bool RemoveGrammarRule(string name)
    {
        for (int i = 0; i < _grammar.Count; i++)
            if (_grammar[i].Name == name)
                return RemoveGrammarRuleAt(i);

        return false;
    }

    /// <summary>
    /// Removes the <see cref="Rule"/> at the specified index from the grammar of the <see cref="Parser"/>.
    /// </summary>
    /// <param name="index">The index for the <see cref="Rule"/> to be removed.</param>
    /// <returns>
    /// <see langword="true"/> if the <see cref="Rule"/> was successfully removed; otherwise, <see langword="false"/>.
    /// </returns>
    public bool RemoveGrammarRuleAt(int index)
    {
        try
        {
            _grammar.RemoveAt(index);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Determines whether a rule with the specified name is in the grammar of the <see cref="Parser"/>.
    /// </summary>
    /// <param name="name">The name of the <see cref="Rule"/> to find.</param>
    /// <returns>
    /// <see langword="true"/> if the <see cref="Rule"/> was successfully found; otherwise, <see langword="false"/>.
    /// </returns>
    public bool GrammarContains(string name)
    {
        foreach (Rule rule in _grammar)
            if (rule.Name == name)
                return true;

        return false;
    }

    private Token? MatchParentheses(string input, IEnumerable<Rule> rules)
    {
        input = input.Trim();

        if (String.IsNullOrWhiteSpace(input))
            return null;

        if (input[0] == '(' && input[^1] == ')')
        {
            int depth = 0;

            for (int i = 1; i < input.Length - 1; i++)
            {
                char current = input[i];

                if (current == '(')
                {
                    depth++;
                }
                else if (current == ')')
                {
                    if (depth == 0)
                        return null;

                    depth--;
                }
            }

            if (depth == 0)
                return new Token(input[1..^1], 1);
        }

        return null;
    }

    private Token? MatchFunctionCall(string input, IEnumerable<Rule> rules)
    {
        input = input.Trim();

        if (String.IsNullOrWhiteSpace(input))
            return null;

        Match function_name = Regex.Match(input, @"^([A-Za-zΑ-Ωα-ω]+(_[A-Za-zΑ-Ωα-ω0-9]+)*)");

        if (function_name.Success)
        {
            Token? parentheses = MatchParentheses(input[function_name.Length..], rules);

            if (parentheses is not null)
                return new Token(
                    input[..(function_name.Length + parentheses.Value.Index + parentheses.Value.Length + 1)],
                    0);
        }

        return null;
    }

    private IExpression ParseFunctionCall(string input, Token token, Parser parser)
    {
        Match function_name = Regex.Match(input, "^([A-Za-zΑ-Ωα-ω]+(_[A-Za-zΑ-Ωα-ω0-9]+)*)");
        string tokenized_args = TokenizeInput(token.Value[(function_name.Length + 1)..^1], out Token[] tokens);

        string[] args = tokenized_args
            .Split(",")
            .Select(arg => !arg.Contains('[')
                ? arg
                : Regex.Replace(arg, @"\[\d+\]", match => tokens[Convert.ToInt32(match.Value[1..^1])]))
            .ToArray();

        return new FunctionCall(function_name.Value, args.Select(arg => parser.Parse(arg)));
    }

    private IExpression ParseParentheses(string input, Token token, Parser parser)
        => new Parentheses(Parse(token));

    private IExpression ParseWithParentheses(string input, Token _, Parser parser)
    {
        string tokenized_input = TokenizeInput(input, out Token[] tokens);

        foreach (Rule rule in parser.Grammar)
        {
            if (rule.GetType().GetCustomAttribute<ReferenceRuleAttribute>() is not null)
                continue;

            Token? match = rule.Match(tokenized_input, Grammar);

            if (match.HasValue)
                return rule.Parse.Invoke(input,
                    new Token(match.Value, DetokenizeIndex(match.Value.Index, tokenized_input, tokens)),
                    this);
        }

        throw new Exception($"The input was not in the correct format: '{input}'");
    }

    private IExpression ParseAssignmentOperator(string input, Token match, Parser parser)
        => new AssignmentOperator((Parse(input[..match.Index]) as Variable)!,
            Parse(input[(match.Index + match.Length)..]));

    private IExpression ParseBinaryOperator(string input, Token match, Parser parser)
        => new BinaryOperator(match.Value, Parse(input[..match.Index]), Parse(input[(match.Index + match.Length)..]));

    private IExpression ParsePrefix(string input, Token match, Parser parser)
        => new UnaryOperator(match.Value, true, Parse(input[(match.Index + match.Length)..]));

    private IExpression ParsePostfix(string input, Token match, Parser parser)
        => new UnaryOperator(match.Value, false, Parse(input[..match.Index]));

    private IExpression ParseConstant(string input, Token match, Parser parser)
        => new Constant(match.Value);

    private IExpression ParseVariable(string input, Token match, Parser parser)
        => new Variable(match.Value);

    private static IExpression ParseNumber(string input, Token match, Parser parser)
        => new Number(Convert.ToDouble(match.Value));
}
