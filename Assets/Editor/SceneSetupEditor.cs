using Thesis.Managers;
using Thesis.Patterns;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class SceneSetupEditor
{
    private const string MANAGERS = "========= Managers =========";
    private const string WORLD    = "========= World =========";
    private const string UI       = "========= UI =========";
    private const string SETUP    = "========= Set up =========";

    [MenuItem("Tools/Setup Scene", false, 0)]
    public static void SetupScene()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("[SceneSetup] Cannot run in Play Mode.");
            return;
        }

        GameObject managers = GetOrCreate(MANAGERS);
        GameObject world    = GetOrCreate(WORLD);
        GameObject ui       = GetOrCreate(UI);
        GameObject setup    = GetOrCreate(SETUP);

        managers.transform.SetSiblingIndex(0);
        world.transform.SetSiblingIndex(1);
        ui.transform.SetSiblingIndex(2);
        setup.transform.SetSiblingIndex(3);

        SetupManagers(managers);
        SetupUICanvas(ui);
        WireUIManager(managers, ui);
        SetupProjectSetup(setup);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[SceneSetup] Done.");
    }

    private static void SetupManagers(GameObject managers)
    {
        EnsureComponent<PersistAcrossScenes>(managers);

        EnsureComponent<AppManager>(EnsureChild(managers, "AppManager"));
        EnsureComponent<RoomClient>(EnsureChild(managers, "RoomClient"));

        GameObject viewerManagers = EnsureChild(managers, "ViewerManagers");
        EnsureComponent<ClientManager>(EnsureChild(viewerManagers, "ClientManager"));
        EnsureComponent<LiveKitManager>(EnsureChild(viewerManagers, "LiveKitManager"));
        EnsureComponent<ViewerTokenClient>(EnsureChild(viewerManagers, "ViewerTokenClient"));

        GameObject cameraManagers = EnsureChild(managers, "CameraManagers");
        EnsureComponent<CameraClientManager>(EnsureChild(cameraManagers, "CameraClientManager"));
        EnsureComponent<RegistrationClient>(EnsureChild(cameraManagers, "RegistrationClient"));
        EnsureComponent<LiveKitCameraPublisher>(EnsureChild(cameraManagers, "LiveKitCameraPublisher"));

        EnsureComponent<UIManager>(EnsureChild(managers, "UIManager"));
        EnsureComponent<DevCommandMenu>(EnsureChild(managers, "DevCommandMenu"));
        EnsureComponent<PubSub>(EnsureChild(managers, "PubSub"));
        EnsureComponent<ObjectPooling>(EnsureChild(managers, "ObjectPooling"));

        AppRoleManager roleManager = EnsureComponent<AppRoleManager>(EnsureChild(managers, "AppRoleManager"));
        WireAppRoleManager(roleManager, viewerManagers, cameraManagers);

        viewerManagers.SetActive(false);
        cameraManagers.SetActive(false);
    }

    private static void WireAppRoleManager(AppRoleManager roleManager, GameObject viewerManagers, GameObject cameraManagers)
    {
        SerializedObject so = new SerializedObject(roleManager);
        so.FindProperty("_viewerManagers").objectReferenceValue = viewerManagers;
        so.FindProperty("_cameraManagers").objectReferenceValue = cameraManagers;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetupUICanvas(GameObject uiRoot)
    {
        Canvas canvas = EnsureComponent<Canvas>(uiRoot);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = EnsureComponent<CanvasScaler>(uiRoot);
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        EnsureComponent<GraphicRaycaster>(uiRoot);

        SetFullStretch(EnsureChild(uiRoot, "Screen"),  0);
        SetFullStretch(EnsureChild(uiRoot, "Popup"),   1);
        SetFullStretch(EnsureChild(uiRoot, "Notify"),  2);
        SetFullStretch(EnsureChild(uiRoot, "Overlap"), 3);
    }

    private static void WireUIManager(GameObject managers, GameObject uiRoot)
    {
        UIManager uiManager = managers.GetComponentInChildren<UIManager>();
        if (uiManager == null) return;

        SerializedObject so = new SerializedObject(uiManager);
        so.FindProperty("cScreen").objectReferenceValue  = uiRoot.transform.Find("Screen")?.gameObject;
        so.FindProperty("cPopup").objectReferenceValue   = uiRoot.transform.Find("Popup")?.gameObject;
        so.FindProperty("cNotify").objectReferenceValue  = uiRoot.transform.Find("Notify")?.gameObject;
        so.FindProperty("cOverlap").objectReferenceValue = uiRoot.transform.Find("Overlap")?.gameObject;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetupProjectSetup(GameObject setupRoot)
    {
        if (Object.FindAnyObjectByType<EventSystem>() == null)
        {
            GameObject es = EnsureChild(setupRoot, "EventSystem");
            EnsureComponent<EventSystem>(es);
            EnsureComponent<StandaloneInputModule>(es);
        }

        Light[] lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
        foreach (Light light in lights)
            if (light.type == LightType.Directional && light.transform.parent == null)
                light.transform.SetParent(setupRoot.transform, true);
    }

    private static GameObject GetOrCreate(string name)
    {
        GameObject go = GameObject.Find(name);
        if (go == null) go = new GameObject(name);
        return go;
    }

    private static GameObject EnsureChild(GameObject parent, string childName)
    {
        Transform t = parent.transform.Find(childName);
        if (t != null) return t.gameObject;
        GameObject child = new GameObject(childName);
        child.transform.SetParent(parent.transform, false);
        return child;
    }

    private static T EnsureComponent<T>(GameObject go) where T : Component
    {
        T comp = go.GetComponent<T>();
        if (comp == null) comp = go.AddComponent<T>();
        return comp;
    }

    private static void SetFullStretch(GameObject go, int siblingIndex)
    {
        go.transform.SetSiblingIndex(siblingIndex);
        RectTransform rt = go.GetComponent<RectTransform>();
        if (rt == null) rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
