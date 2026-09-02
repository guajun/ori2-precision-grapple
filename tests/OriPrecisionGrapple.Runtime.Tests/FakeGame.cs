namespace UnityEngine
{
    public struct Rect
    {
        public Rect(float x, float y, float width, float height)
        {
            this.x = x;
            this.y = y;
            this.width = width;
            this.height = height;
        }

        public float x;
        public float y;
        public float width;
        public float height;
    }

    public struct Color
    {
        public Color(float red, float green, float blue, float alpha)
        {
            r = red;
            g = green;
            b = blue;
            a = alpha;
        }

        public float r;
        public float g;
        public float b;
        public float a;
    }

    public class Texture
    {
    }

    public sealed class Texture2D : Texture
    {
        public static Texture2D whiteTexture { get; } = new();
    }

    public sealed class GUIStyleState
    {
        public Color textColor { get; set; }
    }

    public sealed class GUIStyle
    {
        public int fontSize { get; set; }
        public bool wordWrap { get; set; }
        public bool richText { get; set; }
        public GUIStyleState normal { get; } = new();
    }

    public static class GUI
    {
        public static Color color { get; set; } = new(1, 1, 1, 1);
        public static int BoxCalls { get; private set; }
        public static int DrawTextureCalls { get; private set; }
        public static int LabelCalls { get; private set; }

        public static void Box(Rect position, string text) => BoxCalls++;

        public static void Label(Rect position, string text) => LabelCalls++;

        public static void Label(Rect position, string text, GUIStyle style) => LabelCalls++;

        public static void DrawTexture(Rect position, Texture texture) => DrawTextureCalls++;
    }

    public struct Vector2
    {
        public Vector2(float x, float y)
        {
            this.x = x;
            this.y = y;
        }

        public float x;
        public float y;
    }

    public struct Vector3
    {
        public Vector3(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public float x;
        public float y;
        public float z;
    }

    public sealed class Camera
    {
        public static Camera main { get; } = new();

        public Vector3 WorldToScreenPoint(Vector3 position) => position;
    }

    public static class Screen
    {
        public static int width => 1920;
        public static int height => 1080;
    }
}

public sealed class GameController
{
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public void OnGUI()
    {
    }
}

namespace Core
{
    public static class Input
    {
        public static float Horizontal { get; set; }
        public static float Vertical { get; set; } = 1;

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public static UnityEngine.Vector2 get_Axis() => new(Horizontal, Vertical);
    }
}

namespace SmartInput
{
    public class CompoundButtonInput
    {
        public bool OriginalValue { get; set; }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public virtual bool GetValue() => OriginalValue;
    }

    public sealed class CachedButtonInput : CompoundButtonInput
    {
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public bool GetButton() => OriginalValue;
    }
}

public sealed class PlayerInput
{
    public static PlayerInput Instance { get; } = new();

    public bool Active { get; set; } = true;
    public SmartInput.CompoundButtonInput Bash { get; } = new();
    public SmartInput.CompoundButtonInput Grapple { get; } = new();

    public void ClearControls()
    {
    }
}

public sealed class SeinCharacter
{
    public UnityEngine.Vector3 Position { get; set; } = new(100, 100, 1);

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public bool get_FaceLeft() => true;
}

namespace Game
{
    public static class Characters
    {
        public static bool HasSein => true;
        public static SeinCharacter m_sein { get; } = new();
    }
}

public static class MoonInput
{
    public static UnityEngine.Vector3 MousePosition { get; set; } = new(124, 100, 1);

    public static UnityEngine.Vector3 get_mousePosition() => MousePosition;
}

public struct FakeLeashableInfo
{
    public UnityEngine.Vector3 SurfaceWorldPos;

    public UnityEngine.Vector3 GetAttackablePosition() => SurfaceWorldPos;
}

public sealed class SeinSpiritLeashAbility
{
    public bool HasTarget { get; set; } = true;
    public bool CanLeash { get; set; } = true;
    public FakeLeashableInfo m_targetLeash = new()
    {
        SurfaceWorldPos = new UnityEngine.Vector3(124, 100, 1),
    };

    public UnityEngine.Vector3 LastInputDirection { get; private set; }
    public bool FaceLeftDuringSearch { get; private set; }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public void FindClosestAttackHandler()
    {
        var angle = 0.0f;
        var axis = Core.Input.get_Axis();
        IsInputTowardsTarget(
            new UnityEngine.Vector3(1, 0, 0),
            new UnityEngine.Vector3(axis.x, axis.y, 0),
            false,
            ref angle);
        FaceLeftDuringSearch = Game.Characters.m_sein.get_FaceLeft();
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public bool IsInputTowardsTarget(
        UnityEngine.Vector3 targetDirection,
        UnityEngine.Vector3 inputDirection,
        bool isCurrentTarget,
        ref float angleDifference)
    {
        LastInputDirection = inputDirection;
        return true;
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public bool ShouldShowMark() => true;
}
