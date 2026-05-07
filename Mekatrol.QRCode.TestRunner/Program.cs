using System.Reflection;

namespace Mekatrol.QRCode.TestRunner;

internal static class Program
{
    // The first command-line argument is an optional test-name filter for quick local runs.
    private const int _filterArgumentIndex = 0;

    // Returning 0 signals success to shells and CI tools.
    private const int _successExitCode = 0;

    // Returning 1 signals a failed or empty test run to shells and CI tools.
    private const int _failureExitCode = 1;

    // MSTest marks test classes with this attribute name, and the runner avoids taking a compile-time package dependency.
    private const string _testClassAttributeName = "TestClassAttribute";

    // MSTest marks test methods with this attribute name, and the runner avoids taking a compile-time package dependency.
    private const string _testMethodAttributeName = "TestMethodAttribute";

    // MSTest reports skipped/inconclusive tests with this exception type name.
    private const string _assertInconclusiveExceptionName = "AssertInconclusiveException";

    // The pass prefix makes successful runner output easy to scan.
    private const string _passOutputPrefix = "PASS";

    // The skip prefix makes inconclusive runner output easy to scan.
    private const string _skipOutputPrefix = "SKIP";

    // The fail prefix makes failed runner output easy to scan.
    private const string _failOutputPrefix = "FAIL";

    private static int Main(string[] args)
    {
        var filter = args.Length == 0 ? null : args[_filterArgumentIndex];
        var tests = typeof(Test.QRCodeGeneratorTests).Assembly
            .GetTypes()
            .Where(type => HasAttribute(type, _testClassAttributeName))
            .SelectMany(type => type.GetMethods()
                .Where(method => HasAttribute(method, _testMethodAttributeName))
                .Select(method => new TestCase(type, method)))
            .Where(test => filter is null
                || test.Method.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || test.Type.FullName?.Contains(filter, StringComparison.OrdinalIgnoreCase) == true)
            .ToArray();

        if (tests.Length == 0)
        {
            Console.Error.WriteLine($"No tests matched '{filter}'.");
            return _failureExitCode;
        }

        var failed = 0;
        foreach (var test in tests)
        {
            var instance = Activator.CreateInstance(test.Type);
            try
            {
                test.Method.Invoke(instance, null);
                Console.WriteLine($"{_passOutputPrefix} {test.Type.Name}.{test.Method.Name}");
            }
            catch (TargetInvocationException ex) when (ex.InnerException?.GetType().Name == _assertInconclusiveExceptionName)
            {
                Console.WriteLine($"{_skipOutputPrefix} {test.Type.Name}.{test.Method.Name}: {ex.InnerException.Message}");
            }
            catch (TargetInvocationException ex)
            {
                failed++;
                Console.Error.WriteLine($"{_failOutputPrefix} {test.Type.Name}.{test.Method.Name}");
                Console.Error.WriteLine(ex.InnerException);
            }
        }

        return failed == 0 ? _successExitCode : _failureExitCode;
    }

    private static bool HasAttribute(MemberInfo member, string attributeTypeName)
    {
        return member.GetCustomAttributes()
            .Any(attribute => attribute.GetType().Name == attributeTypeName);
    }

    private sealed record TestCase(Type Type, MethodInfo Method);
}
