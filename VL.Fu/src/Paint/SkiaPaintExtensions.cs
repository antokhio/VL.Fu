using System.Diagnostics;
using System.Reflection;
using System.Text;
using SkiaSharp;

namespace VL.Fu.Pain
{
    public static class SkiaPaintExtensions
    {
        // A flag to ensure we only print it once, so we don't spam the console every frame
        private static bool _hasDumped = false;

        public static void DumpPaintInfo(object? input)
        {
            if (_hasDumped)
                return;
            _hasDumped = true;

            if (input == null)
            {
                Console.WriteLine("--- DUMPING PAINT INFO: Object is NULL ---");
                return;
            }

            Type type = input.GetType();
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"\n\n--- DUMPING TYPE INFO FOR: {type.FullName} ---");

            sb.AppendLine("\n[INTERFACES]");
            foreach (var iface in type.GetInterfaces())
            {
                sb.AppendLine($"  - {iface.FullName}");
            }

            sb.AppendLine("\n[FIELDS]");
            foreach (
                var field in type.GetFields(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
                )
            )
            {
                sb.AppendLine($"  - {field.FieldType.Name} {field.Name}");
            }

            sb.AppendLine("\n[PROPERTIES]");
            foreach (
                var prop in type.GetProperties(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
                )
            )
            {
                sb.AppendLine($"  - {prop.PropertyType.Name} {prop.Name}");
            }

            sb.AppendLine("\n[METHODS]");
            // Using DeclaredOnly to avoid printing all the standard Object methods like GetHashCode/ToString
            foreach (
                var method in type.GetMethods(
                    BindingFlags.Public
                        | BindingFlags.NonPublic
                        | BindingFlags.Instance
                        | BindingFlags.DeclaredOnly
                )
            )
            {
                var paramStrings = method
                    .GetParameters()
                    .Select(p => $"{p.ParameterType.Name} {p.Name}");
                sb.AppendLine(
                    $"  - {method.ReturnType.Name} {method.Name}({string.Join(", ", paramStrings)})"
                );
            }

            sb.AppendLine("--------------------------------------------------\n\n");

            // Print to standard console and debug output
            Console.WriteLine(sb.ToString());
            Debug.WriteLine(sb.ToString());
        }

        public static SKPaint? FromSkiaPaint(this object? input)
        {
            if (input == null)
                return null;

            Type type = input.GetType();

            // 1. Look for the SKPaint(out SKPaint Paint_Out) method
            MethodInfo? skPaintMethod = type.GetMethod(
                "SKPaint",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
            );

            if (skPaintMethod != null)
            {
                var parameters = skPaintMethod.GetParameters();
                // Ensure it takes exactly one parameter of type `ref/out SKPaint`
                if (
                    parameters.Length == 1
                    && parameters[0].ParameterType == typeof(SKPaint).MakeByRefType()
                )
                {
                    // Create an array to hold the 'out' parameter
                    object?[] args = new object?[] { null };

                    // Invoke the method. The 'out' result will be written into args[0]
                    skPaintMethod.Invoke(input, args);

                    return args[0] as SKPaint;
                }
            }

            // 2. Fallback to the direct field if the method isn't found
            FieldInfo? paintField = type.GetFields(
                    BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public
                )
                .FirstOrDefault(f => f.FieldType == typeof(SKPaint));

            if (paintField != null)
            {
                return (SKPaint?)paintField.GetValue(input);
            }

            return null;
        }
    }
}
