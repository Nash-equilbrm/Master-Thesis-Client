// using UnityEngine;
// using UnityEngine.UI;
// using UnityEditor;
// using UnityEditor.SceneManagement;
// using UnityEngine.SceneManagement;
// using TMPro;
//
// public static class SceneSetup
// {
//     [MenuItem("Tools/Setup Test Scene")]
//     public static void SetupTestScene()
//     {
//         // --- LiveKitManager ---
//         var lkGo = new GameObject("LiveKitManager");
//         Undo.RegisterCreatedObjectUndo(lkGo, "Setup Test Scene");
//         var manager = lkGo.AddComponent<LiveKitManager>();
//
//         // The connection screen drives connection now, so don't auto-connect.
//         SetAutoConnect(manager, false);
//
//         // --- Canvas ---
//         var canvasGo = new GameObject("UI Root");
//         Undo.RegisterCreatedObjectUndo(canvasGo, "Setup Test Scene");
//
//         var canvas = canvasGo.AddComponent<Canvas>();
//         canvas.renderMode = RenderMode.ScreenSpaceOverlay;
//
//         var scaler = canvasGo.AddComponent<CanvasScaler>();
//         scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
//         scaler.referenceResolution = new Vector2(1920, 1080);
//         scaler.matchWidthOrHeight = 0.5f;
//
//         canvasGo.AddComponent<GraphicRaycaster>();
//
//         // --- VideoDisplay (left 80%) ---
//         var videoGo = new GameObject("VideoDisplay");
//         videoGo.transform.SetParent(canvasGo.transform, false);
//
//         var rawImage = videoGo.AddComponent<RawImage>();
//         rawImage.color = Color.black;
//
//         var streamPlayer = videoGo.AddComponent<CameraStreamPlayer>();
//
//         var videoRt = videoGo.GetComponent<RectTransform>();
//         videoRt.anchorMin = new Vector2(0f, 0f);
//         videoRt.anchorMax = new Vector2(0.8f, 1f);
//         videoRt.offsetMin = Vector2.zero;
//         videoRt.offsetMax = Vector2.zero;
//
//         // --- CameraPanel (right 20%) ---
//         var panelGo = new GameObject("CameraPanel");
//         panelGo.transform.SetParent(canvasGo.transform, false);
//
//         var panelImg = panelGo.AddComponent<Image>();
//         panelImg.color = new Color(0.08f, 0.08f, 0.08f, 0.95f);
//
//         var panelRt = panelGo.GetComponent<RectTransform>();
//         panelRt.anchorMin = new Vector2(0.8f, 0f);
//         panelRt.anchorMax = new Vector2(1f, 1f);
//         panelRt.offsetMin = Vector2.zero;
//         panelRt.offsetMax = Vector2.zero;
//
//         // --- ButtonContainer (vertical list inside panel) ---
//         var containerGo = new GameObject("ButtonContainer", typeof(RectTransform));
//         containerGo.transform.SetParent(panelGo.transform, false);
//
//         var containerRt = containerGo.GetComponent<RectTransform>();
//         containerRt.anchorMin = new Vector2(0f, 1f);
//         containerRt.anchorMax = new Vector2(1f, 1f);
//         containerRt.pivot = new Vector2(0.5f, 1f);
//         containerRt.offsetMin = new Vector2(8f, 0f);
//         containerRt.offsetMax = new Vector2(-8f, 0f);
//
//         var vlg = containerGo.AddComponent<VerticalLayoutGroup>();
//         vlg.spacing = 6f;
//         vlg.padding = new RectOffset(0, 0, 8, 8);
//         vlg.childAlignment = TextAnchor.UpperCenter;
//         vlg.childControlWidth = true;
//         vlg.childControlHeight = false;
//         vlg.childForceExpandWidth = true;
//         vlg.childForceExpandHeight = false;
//
//         var csf = containerGo.AddComponent<ContentSizeFitter>();
//         csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
//
//         // --- CameraSwitcher on the panel ---
//         var switcher = panelGo.AddComponent<CameraSwitcher>();
//         var so = new SerializedObject(switcher);
//         so.FindProperty("streamPlayer").objectReferenceValue = streamPlayer;
//         so.FindProperty("buttonContainer").objectReferenceValue = containerGo.transform;
//         so.ApplyModifiedProperties();
//
//         EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
//
//         Debug.Log("[SceneSetup] Done. Set the URL and token on LiveKitManager, then press Play.");
//         Selection.activeGameObject = lkGo;
//     }
// }
