﻿using CalcExpr.Expressions;
using System.Linq.Expressions;
using System.Reflection;

namespace TestCalcExpr;

[TestClass]
public class TestFunction
{
    /// <summary>
    /// Tests that the values of the Function are the same as the values passed into the Function's constructor.
    /// </summary>
    [TestMethod]
    public void TestFunctionInit()
    {
        foreach (MethodInfo method in typeof(TestCases).GetMethods(BindingFlags.Static))
        {
            Delegate del = method.CreateDelegate(Expression.GetDelegateType(method.GetParameters()
                .Select(p => p.ParameterType)
                .Concat(new Type[] { method.ReturnType })
                .ToArray()));
            string[] parameters = method.GetParameters().Select(p => p.Name ?? "").ToArray();
            Function function = new Function(del);

            Assert.AreEqual(method, function.Body.Method);

            for (int i = 0; i < parameters.Length; i++)
                Assert.AreEqual(parameters[i], function.Parameters[i]);

            function = new Function(parameters, del);
            Assert.AreEqual(method, function.Body.Method);

            for (int i = 0; i < parameters.Length; i++)
                Assert.AreEqual(parameters[i], function.Parameters[i]);
        }
    }

    /// <summary>
    /// Tests that the values of the FunctionCall are the same as the values passed into the FunctionCall's constructor.
    /// </summary>
    [TestMethod]
    public void TestFunctionCallInit()
    {
        Random random = new Random();

        foreach (string function_name in TestCases.Functions.Keys)
        {
            IExpression[] args = new IExpression[TestCases.Functions[function_name].Parameters.Length]
                .Select((x) => new Number(random.NextDouble() + random.Next())).ToArray();
            FunctionCall function_call = new FunctionCall(function_name, args);
            
            Assert.AreEqual(function_name, function_call.Name);

            for (int i = 0; i < args.Length; i++)
                Assert.AreEqual(args[i], function_call.Arguments[i]);
        }
    }

    /// <summary>
    /// Tests that the FunctionCall converts to a string properly.
    /// </summary>
    [TestMethod]
    public void TestFunctionCallToString()
    {
        Dictionary<FunctionCall, string> expressions = new Dictionary<FunctionCall, string>()
        {
            { new FunctionCall("f", new IExpression[] { new Number(1) }), "f(1)" },
            { new FunctionCall("g", new IExpression[] { new Number(1), new Number(2), new Number(3) }), "g(1, 2, 3)" },
            { new FunctionCall("p", new IExpression[] { new Number(1) }), "p(1)" },
        };

        foreach (FunctionCall expression in expressions.Keys)
            Assert.AreEqual(expressions[expression], expression.ToString());
    }
}
