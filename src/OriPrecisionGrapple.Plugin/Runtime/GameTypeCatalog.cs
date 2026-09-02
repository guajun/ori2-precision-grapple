using System.Reflection;
using BepInEx;

namespace OriPrecisionGrapple.Runtime;

internal sealed class GameTypeCatalog
{
    private static readonly BindingFlags AllMembers =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    public Type CompoundButtonInput { get; private init; } = null!;
    public Type? CachedButtonInput { get; private init; }
    public Type PlayerInput { get; private init; } = null!;
    public Type CoreInput { get; private init; } = null!;
    public Type SpiritLeash { get; private init; } = null!;
    public Type SeinCharacter { get; private init; } = null!;
    public Type Characters { get; private init; } = null!;
    public Type MoonInput { get; private init; } = null!;
    public Type Camera { get; private init; } = null!;
    public Type Screen { get; private init; } = null!;
    public Type? GameController { get; private init; }
    public Type? Gui { get; private init; }
    public Type? Rect { get; private init; }
    public Type? Color { get; private init; }
    public Type? Matrix4x4 { get; private init; }
    public Type? GuiStyle { get; private init; }
    public Type? Texture2D { get; private init; }
    public Type? BashAttack { get; private init; }

    public static bool TryCreate(out GameTypeCatalog? catalog, out string error)
    {
        catalog = null;
        EnsureInteropAssembliesLoaded();

        var compound = FindType("SmartInput.CompoundButtonInput", "CompoundButtonInput");
        var playerInput = FindType("PlayerInput");
        var coreInput = FindType("Core.Input");
        var spiritLeash = FindType("SeinSpiritLeashAbility");
        var seinCharacter = FindType("SeinCharacter");
        var characters = FindType("Game.Characters", "Characters");
        var moonInput = FindType("MoonInput");
        var camera = FindType("UnityEngine.Camera");
        var screen = FindType("UnityEngine.Screen");

        var missing = new List<string>();
        AddMissing(missing, compound, "SmartInput.CompoundButtonInput");
        AddMissing(missing, playerInput, "PlayerInput");
        AddMissing(missing, coreInput, "Core.Input");
        AddMissing(missing, spiritLeash, "SeinSpiritLeashAbility");
        AddMissing(missing, seinCharacter, "SeinCharacter");
        AddMissing(missing, characters, "Game.Characters");
        AddMissing(missing, moonInput, "MoonInput");
        AddMissing(missing, camera, "UnityEngine.Camera");
        AddMissing(missing, screen, "UnityEngine.Screen");

        if (missing.Count > 0)
        {
            error = $"Missing interop types: {string.Join(", ", missing)}";
            return false;
        }

        catalog = new GameTypeCatalog
        {
            CompoundButtonInput = compound!,
            CachedButtonInput = FindType("SmartInput.CachedButtonInput", "CachedButtonInput"),
            PlayerInput = playerInput!,
            CoreInput = coreInput!,
            SpiritLeash = spiritLeash!,
            SeinCharacter = seinCharacter!,
            Characters = characters!,
            MoonInput = moonInput!,
            Camera = camera!,
            Screen = screen!,
            GameController = FindType("GameController"),
            Gui = FindType("UnityEngine.GUI"),
            Rect = FindType("UnityEngine.Rect"),
            Color = FindType("UnityEngine.Color"),
            Matrix4x4 = FindType("UnityEngine.Matrix4x4"),
            GuiStyle = FindType("UnityEngine.GUIStyle"),
            Texture2D = FindType("UnityEngine.Texture2D"),
            BashAttack = FindType("SeinBashAttack"),
        };
        error = string.Empty;
        return true;
    }

    public static MethodInfo? FindMethod(Type type, string name, int parameterCount) =>
        type.GetMethods(AllMembers)
            .FirstOrDefault(method => method.Name == name && method.GetParameters().Length == parameterCount);

    private static Type? FindType(params string[] fullNames)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            foreach (var fullName in fullNames)
            {
                var type = assembly.GetType(fullName, throwOnError: false, ignoreCase: false);
                if (type is not null)
                {
                    return type;
                }
            }
        }

        return null;
    }

    private static void EnsureInteropAssembliesLoaded()
    {
        TryLoadAssembly("__mainWisp");
        TryLoadAssembly("Assembly-CSharp");
        TryLoadAssembly("UnityEngine.CoreModule");
        TryLoadAssembly("UnityEngine.IMGUIModule");
    }

    private static void TryLoadAssembly(string assemblyName)
    {
        if (AppDomain.CurrentDomain.GetAssemblies().Any(assembly => assembly.GetName().Name == assemblyName))
        {
            return;
        }

        try
        {
            Assembly.Load(new AssemblyName(assemblyName));
        }
        catch (FileNotFoundException)
        {
            TryLoadAssemblyFromInteropDirectory(assemblyName);
        }
        catch (FileLoadException)
        {
            // Type discovery below reports the actionable missing-type list.
        }
    }

    private static void TryLoadAssemblyFromInteropDirectory(string assemblyName)
    {
        if (string.IsNullOrWhiteSpace(Paths.BepInExRootPath))
        {
            return;
        }

        try
        {
            var assemblyPath = Path.Combine(Paths.BepInExRootPath, "interop", $"{assemblyName}.dll");
            if (File.Exists(assemblyPath))
            {
                Assembly.LoadFrom(assemblyPath);
            }
        }
        catch (FileLoadException)
        {
            // Type discovery below reports the actionable missing-type list.
        }
    }

    private static void AddMissing(ICollection<string> missing, Type? type, string name)
    {
        if (type is null)
        {
            missing.Add(name);
        }
    }
}
