﻿using CalcExpr.Expressions;

namespace CalcExpr.Context;

public class ExpressionContext
{
    private Dictionary<string, IExpression> _variables;
    private Dictionary<string, IFunction> _functions;

    public string[] Variables
        => _variables.Keys.Concat(Functions).ToArray();

    public string[] Functions
        => _functions.Keys.ToArray();

    public IExpression this[string variable]
    {
        get => _variables.ContainsKey(variable)
            ? _variables[variable]
            : _functions.ContainsKey(variable)
                ? _functions[variable]
                : Constant.UNDEFINED;
        set => SetVariable(variable, value);
    }

    public IExpression this[string function, IEnumerable<IExpression> arguments]
        => ContainsFunction(function)
            ? _functions[function].Invoke(arguments.ToArray(), this)
            : Constant.UNDEFINED;

    public ExpressionContext(Dictionary<string, IExpression>? variables = null,
        Dictionary<string, IFunction>? functions = null)
    {
        Dictionary<string, IExpression> vars = new Dictionary<string, IExpression>();
        Dictionary<string, IFunction> funcs = functions?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
            ?? new Dictionary<string, IFunction>();

        if (variables is not null)
            foreach (string var in variables.Keys)
                if (variables[var] is IFunction func)
                    funcs.Add(var, func);
                else
                    vars.Add(var, variables[var]);

        _variables = vars;
        _functions = funcs;
    }

    public ExpressionContext Clone()
    {
        Dictionary<string, IExpression> vars = new Dictionary<string, IExpression>();
        Dictionary<string, IFunction> funcs = new Dictionary<string, IFunction>();

        foreach (string var in _variables.Keys)
            vars.Add(var, _variables[var].Clone());

        foreach (string func in _functions.Keys)
            funcs.Add(func, (IFunction)_functions[func].Clone());

        return new ExpressionContext(vars, funcs);
    }

    public bool SetVariable(string name, IExpression expression)
    {
        if (expression is null)
            return RemoveVariable(name);

        if (expression is not IFunction function)
        {
            _variables[name] = expression;
            return true;
        }
        else
        {
            return SetFunction(name, function);
        }
    }

    public bool RemoveVariable(string name)
        => _variables.Remove(name) || _functions.Remove(name);

    public bool ContainsVariable(string name)
        => _variables.ContainsKey(name) || ContainsFunction(name);
    
    public bool SetFunction(string name, IFunction function)
    {
        if (function is null)
            return _functions.Remove(name);

        _functions[name] = function;
        return true;
    }

    public bool RemoveFunction(string name)
        => _functions.Remove(name);

    public bool ContainsFunction(string name)
        => _functions.ContainsKey(name);
}
