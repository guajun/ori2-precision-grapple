namespace UnityEngine
{
    public class Object
    {
        public static void DontDestroyOnLoad(Object target)
        {
        }
    }

    public class Component : Object
    {
        public bool enabled { get; set; } = true;
    }

    public class Transform : Component
    {
        public Vector3 position { get; set; }

        public void SetParent(Transform parent, bool worldPositionStays)
        {
        }
    }

    public sealed class RectTransform : Transform
    {
        public Vector2 anchorMin { get; set; }
        public Vector2 anchorMax { get; set; }
        public Vector2 pivot { get; set; }
        public Vector2 anchoredPosition { get; set; }
        public Vector2 sizeDelta { get; set; }
    }

    public sealed class GameObject : Object
    {
        public GameObject(string name)
        {
            this.name = name;
        }

        public string name { get; }

        public Transform transform { get; } = new();

        public T AddComponent<T>() where T : new() => new();

        public void SetActive(bool active)
        {
        }
    }

    public sealed class Font : Object
    {
    }

    public static class Resources
    {
        public static T GetBuiltinResource<T>(string path) where T : new() => new();
    }

    public enum TextAnchor
    {
        UpperLeft,
        MiddleCenter,
    }

    public enum RenderMode
    {
        ScreenSpaceOverlay,
    }

    public enum HorizontalWrapMode
    {
        Wrap,
        Overflow,
    }

    public enum VerticalWrapMode
    {
        Truncate,
        Overflow,
    }

    public sealed class Canvas : Component
    {
        public RenderMode renderMode { get; set; }
        public bool overrideSorting { get; set; }
        public int sortingOrder { get; set; }
        public bool pixelPerfect { get; set; }
    }

    public sealed class GUIText : Component
    {
        public GUIText()
        {
            Instances.Add(this);
        }

        public static List<GUIText> Instances { get; } = new();

        public string text { get; set; } = string.Empty;
        public int fontSize { get; set; }
        public Font? font { get; set; }
        public TextAnchor anchor { get; set; }
        public Color color { get; set; }
        public Vector2 pixelOffset { get; set; }
        public bool richText { get; set; }
        public float lineSpacing { get; set; }
    }

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

namespace UnityEngine.UI
{
    public class Graphic : UnityEngine.Component
    {
        public UnityEngine.RectTransform rectTransform { get; } = new();
        public UnityEngine.Color color { get; set; }
        public bool raycastTarget { get; set; }
    }

    public sealed class Image : Graphic
    {
    }

    public sealed class Text : Graphic
    {
        public Text()
        {
            Instances.Add(this);
        }

        public static List<Text> Instances { get; } = new();

        public UnityEngine.Font? font { get; set; }
        public string text { get; set; } = string.Empty;
        public int fontSize { get; set; }
        public UnityEngine.TextAnchor alignment { get; set; }
        public bool supportRichText { get; set; }
        public float lineSpacing { get; set; }
        public UnityEngine.HorizontalWrapMode horizontalOverflow { get; set; }
        public UnityEngine.VerticalWrapMode verticalOverflow { get; set; }
    }
}

public sealed class GameController
{
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public void Update()
    {
    }

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
