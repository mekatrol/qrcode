using System.Reflection;

namespace Mekatrol.QRCode.TestRunner;

internal static class Program
{
    private static int Main(string[] args)
    {
        var filter = args.Length == 0 ? null : args[0];
        var tests = typeof(Test.QRCodeGeneratorTests).Assembly
            .GetTypes()
            .Where(type => HasAttribute(type, "TestClassAttribute"))
            .SelectMany(type => type.GetMethods()
                .Where(method => HasAttribute(method, "TestMethodAttribute"))
                .Select(method => new TestCase(type, method)))
            .Where(test => filter is null
                || test.Method.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || test.Type.FullName?.Contains(filter, StringComparison.OrdinalIgnoreCase) == true)
            .ToArray();

        if (tests.Length == 0)
        {
            Console.Error.WriteLine($"No tests matched '{filter}'.");
            return 1;
        }

        var failed = 0;
        foreach (var test in tests)
        {
            var instance = Activator.CreateInstance(test.Type);
            try
            {
                test.Method.Invoke(instance, null);
                Console.WriteLine($"PASS {test.Type.Name}.{test.Method.Name}");
            }
            catch (TargetInvocationException ex) when (ex.InnerException?.GetType().Name == "AssertInconclusiveException")
            {
                Console.WriteLine($"SKIP {test.Type.Name}.{test.Method.Name}: {ex.InnerException.Message}");
            }
            catch (TargetInvocationException ex)
            {
                failed++;
                Console.Error.WriteLine($"FAIL {test.Type.Name}.{test.Method.Name}");
                Console.Error.WriteLine(ex.InnerException);
            }
        }

        return failed == 0 ? 0 : 1;
    }

    private static bool HasAttribute(MemberInfo member, string attributeTypeName)
    {
        return member.GetCustomAttributes()
            .Any(attribute => attribute.GetType().Name == attributeTypeName);
    }

    private sealed record TestCase(Type Type, MethodInfo Method);
}
