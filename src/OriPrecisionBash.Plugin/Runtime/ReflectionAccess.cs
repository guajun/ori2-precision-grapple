using System.Globalization;
using System.Reflection;

namespace OriPrecisionBash.Runtime;

internal static class ReflectionAccess
{
    private static readonly BindingFlags AllMembers =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    public static object? Get(object instance, params string[] names) => Get(instance.GetType(), instance, names);

    public static object? GetStatic(Type type, params string[] names) => Get(type, null, names);

    public static void SetStatic(Type type, object value, params string[] names) => Set(type, null, value, names);

    public static bool GetBoolean(object instance, bool fallback, params string[] names)
    {
        var value = Get(instance, names);
        return value is bool result ? result : fallback;
    }

    public static object? Invoke(object instance, string methodName, params object?[] arguments) =>
        InvokeCore(instance.GetType(), instance, methodName, arguments);

    public static object? InvokeStatic(Type type, string methodName, params object?[] arguments) =>
        InvokeCore(type, null, methodName, arguments);

    public static double ReadNumber(object instance, params string[] names)
    {
        var value = Get(instance, names);
        return value is null ? double.NaN : Convert.ToDouble(value, CultureInfo.InvariantCulture);
    }

    public static object CreateVector(Type type, double x, double y, double z)
    {
        var constructor = type.GetConstructor(new[] { typeof(float), typeof(float), typeof(float) });
        if (constructor is not null)
        {
            return constructor.Invoke(new object[] { (float)x, (float)y, (float)z });
        }

        constructor = type.GetConstructor(new[] { typeof(float), typeof(float) });
        if (constructor is not null)
        {
            return constructor.Invoke(new object[] { (float)x, (float)y });
        }

        var vector = Activator.CreateInstance(type)
            ?? throw new InvalidOperationException($"Could not construct {type.FullName}.");
        Set(vector, (float)x, "x", "X");
        Set(vector, (float)y, "y", "Y");
        TrySet(vector, (float)z, "z", "Z");
        return vector;
    }

    public static bool SameNativeObject(object? left, object? right)
    {
        if (left is null || right is null)
        {
            return false;
        }

        if (ReferenceEquals(left, right))
        {
            return true;
        }

        var leftPointer = Get(left, "Pointer", "m_CachedPtr");
        var rightPointer = Get(right, "Pointer", "m_CachedPtr");
        return leftPointer is not null && Equals(leftPointer, rightPointer);
    }

    private static object? Get(Type type, object? instance, IEnumerable<string> names)
    {
        foreach (var name in names)
        {
            var property = type.GetProperty(name, AllMembers);
            if (property is not null)
            {
                return property.GetValue(instance);
            }

            var field = type.GetField(name, AllMembers);
            if (field is not null)
            {
                return field.GetValue(instance);
            }

            var getter = type.GetMethod($"get_{name}", AllMembers, null, Type.EmptyTypes, null);
            if (getter is not null)
            {
                return getter.Invoke(instance, null);
            }
        }

        return null;
    }

    private static void Set(object instance, object value, params string[] names) =>
        Set(instance.GetType(), instance, value, names);

    private static void Set(Type type, object? instance, object value, params string[] names)
    {
        foreach (var name in names)
        {
            var property = type.GetProperty(name, AllMembers);
            if (property?.CanWrite == true)
            {
                property.SetValue(instance, value);
                return;
            }

            var field = type.GetField(name, AllMembers);
            if (field is not null)
            {
                field.SetValue(instance, value);
                return;
            }
        }

        throw new MissingMemberException(type.FullName, string.Join("/", names));
    }

    private static bool TrySet(object instance, object value, params string[] names)
    {
        try
        {
            Set(instance, value, names);
            return true;
        }
        catch (MissingMemberException)
        {
            return false;
        }
    }

    private static object? InvokeCore(
        Type type,
        object? instance,
        string methodName,
        IReadOnlyCollection<object?> arguments)
    {
        var method = type.GetMethods(AllMembers)
            .FirstOrDefault(candidate =>
                candidate.Name == methodName && candidate.GetParameters().Length == arguments.Count);

        if (method is null)
        {
            throw new MissingMethodException(type.FullName, methodName);
        }

        return method.Invoke(instance, arguments.ToArray());
    }
}
