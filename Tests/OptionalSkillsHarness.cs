using System.Reflection;
using System.Reflection.Emit;
using Shotgun;

internal static class OptionalSkillsHarness
{
    private static void Fixture(bool complete)
    {
        var module = AssemblyBuilder.DefineDynamicAssembly(new("FiligranisSkills"), AssemblyBuilderAccess.Run).DefineDynamicModule("fixture");
        Type? definition = null;
        if (complete)
        {
            var type = module.DefineType("FiligranisSkills.SkillDefinition", TypeAttributes.Public);
            var ctor = type.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, new[] {
                typeof(string), typeof(Il2Cpp.LocalizedString), typeof(string), typeof(string), typeof(string),
                typeof(Il2Cpp.LocalizedString[]), typeof(Il2Cpp.LocalizedString[]), typeof(int[]), typeof(UnityEngine.Texture2D) });
            var il = ctor.GetILGenerator(); il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Call, typeof(object).GetConstructor(Type.EmptyTypes)!); il.Emit(OpCodes.Ret);
            definition = type.CreateType();
        }
        var api = module.DefineType("FiligranisSkills.FiligranisSkills", TypeAttributes.Public);
        var points = api.DefineField("Points", typeof(int), FieldAttributes.Public | FieldAttributes.Static);
        var registered = api.DefineField("Registered", typeof(bool), FieldAttributes.Public | FieldAttributes.Static);
        var get = api.DefineMethod("GetPointsAndTiers", MethodAttributes.Public | MethodAttributes.Static,
            typeof(ValueTuple<int, int>), new[] { typeof(string) });
        var body = get.GetILGenerator(); body.Emit(OpCodes.Ldc_I4, 31); body.Emit(OpCodes.Ldc_I4_2);
        body.Emit(OpCodes.Newobj, typeof(ValueTuple<int, int>).GetConstructor(new[] { typeof(int), typeof(int) })!); body.Emit(OpCodes.Ret);
        var increase = api.DefineMethod("IncreaseSkillPoints", MethodAttributes.Public | MethodAttributes.Static,
            typeof(void), new[] { typeof(string), typeof(int) });
        body = increase.GetILGenerator(); body.Emit(OpCodes.Ldsfld, points); body.Emit(OpCodes.Ldarg_1);
        body.Emit(OpCodes.Add); body.Emit(OpCodes.Stsfld, points); body.Emit(OpCodes.Ret);
        var register = api.DefineMethod("RegisterSkill", MethodAttributes.Public | MethodAttributes.Static,
            typeof(void), new[] { definition ?? typeof(object) });
        body = register.GetILGenerator(); body.Emit(OpCodes.Ldc_I4_1); body.Emit(OpCodes.Stsfld, registered); body.Emit(OpCodes.Ret);
        api.CreateType();
    }

    private static void Main(string[] args)
    {
        string mode = args[0];
        if (mode != "absent") Fixture(mode == "present");
        ShotgunSkill.Init();
        if (ShotgunSkill.IsAvailable != (mode == "present")) throw new Exception("Wrong optional integration availability: " + mode);
        if (mode == "absent" && MelonLoader.MelonLogger.Errors.Count != 0) throw new Exception("Missing optional mod produced errors");
        if (mode == "incomplete" && MelonLoader.MelonLogger.Errors.Count != 1) throw new Exception("Missing definition not reported");
        if (mode == "present")
        {
            if (ShotgunSkill.GetPointsAndTiers(ShotgunSkill.SkillName) != (31, 2)) throw new Exception("Progress delegate failed");
            ShotgunSkill.IncreaseSkillPoints(ShotgunSkill.SkillName, 3);
            var api = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "FiligranisSkills").GetType("FiligranisSkills.FiligranisSkills")!;
            if (!(bool)api.GetField("Registered")!.GetValue(null)! || (int)api.GetField("Points")!.GetValue(null)! != 3)
                throw new Exception("Registration/progression failed");
        }
        Console.WriteLine("PASS: production optional skills lookup, " + mode);
    }
}

// Native/UI boundaries only; the real production lookup and registration execute above.
namespace HarmonyLib
{
    public class HarmonyPatch : Attribute { public HarmonyPatch(Type type, string method) { } }
    public static class AccessTools
    {
        public static MethodInfo? FirstMethod(Type type, Func<MethodInfo, bool> predicate) => type.GetMethods().FirstOrDefault(predicate);
        public static MethodInfo? Method(Type type, string name) => type.GetMethod(name);
        public static ConstructorInfo? Constructor(Type type, Type[] parameters) => type.GetConstructor(parameters);
    }
}
namespace MelonLoader
{
    public static class MelonLogger
    {
        public static readonly List<string> Errors = new();
        public static void Msg(string text) { }
        public static void Error(string text) => Errors.Add(text);
    }
}
namespace UnityEngine
{
    public class Texture2D { public int width, height; }
    public struct Vector3 { public static Vector3 zero => new(); }
    public class Transform
    {
        public GameObject gameObject = new(); public Vector3 localPosition;
        public Transform? Find(string name) => null;
        public void SetParent(Transform parent, bool world) { }
    }
    public class GameObject
    {
        public string name; public int layer;
        public GameObject(string name = "") => this.name = name;
        public Transform transform => new();
        public void SetActive(bool active) { }
        public T AddComponent<T>() where T : new() => new();
    }
    public static class Mathf { public static int Max(int a, int b) => Math.Max(a, b); public static int RoundToInt(float value) => (int)Math.Round(value); }
}
namespace UnityEngine.AddressableAssets
{
    public static class Addressables { public static Handle<T> LoadAssetAsync<T>(string name) where T : new() => new(); }
    public class Handle<T> where T : new() { public T WaitForCompletion() => new(); }
}
namespace Il2Cpp
{
    public class LocalizedString { public string m_LocalizationID = ""; }
    public class UISprite
    {
        public string spriteName = ""; public UnityEngine.Transform transform = new(); public UnityEngine.GameObject gameObject = new();
        public int pivot, color, depth, height;
    }
    public class UITexture { public UnityEngine.Texture2D? mainTexture; public int rawPivot, color, depth, height, width; }
    public class Skill { public string name = ""; }
    public class SkillsManager { public List<Skill> m_Skills = new(); }
    public static class GameManager { public static SkillsManager GetSkillsManager() => new(); }
    public class SkillDisplay { public UISprite m_SkillIcon = new(); }
    public class Panel_Log { public List<SkillDisplay> m_SkillsDisplayList = new(); public void RefreshSkillsList() { } }
}
