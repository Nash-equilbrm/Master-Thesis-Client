using DG.Tweening;
using Thesis.UI;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(UIAnim), useForChildren: true)]
public class UIAnimDrawer : PropertyDrawer
{
    private static readonly GUIContent[] _labels =
    {
        new GUIContent("Fade"),
        new GUIContent("Scale"),
        new GUIContent("Slide"),
    };

    private static readonly System.Type[] _types =
    {
        typeof(FadeAnim),
        typeof(ScaleAnim),
        typeof(SlideAnim),
    };

    private static int GetTypeIndex(object obj)
    {
        if (obj == null) return 0;
        var t = obj.GetType();
        for (int i = 0; i < _types.Length; i++)
            if (_types[i] == t) return i;
        return 0;
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float h     = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        var   iter  = property.Copy();
        var   end   = property.GetEndProperty();
        bool  first = true;

        while (iter.NextVisible(first) && !SerializedProperty.EqualContents(iter, end))
        {
            first = false;
            h += EditorGUI.GetPropertyHeight(iter, true) + EditorGUIUtility.standardVerticalSpacing;
        }

        return h;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        float lineH = EditorGUIUtility.singleLineHeight;
        float sp    = EditorGUIUtility.standardVerticalSpacing;
        float lw    = EditorGUIUtility.labelWidth;

        // ── Type selector ──────────────────────────────────────────────────
        int current  = GetTypeIndex(property.managedReferenceValue);
        EditorGUI.LabelField(new Rect(position.x, position.y, lw, lineH), label);
        int selected = EditorGUI.Popup(
            new Rect(position.x + lw, position.y, position.width - lw, lineH),
            current, _labels);

        if (selected != current || property.managedReferenceValue == null)
        {
            // Preserve shared base fields across type changes
            float   oldDuration = (property.managedReferenceValue as UIAnim)?.duration ?? 0.3f;
            Ease    oldEase     = (property.managedReferenceValue as UIAnim)?.ease     ?? Ease.OutCubic;
            var     newAnim     = (UIAnim)System.Activator.CreateInstance(_types[selected]);
            newAnim.duration = oldDuration;
            newAnim.ease     = oldEase;
            property.managedReferenceValue = newAnim;
        }

        // ── Child fields ───────────────────────────────────────────────────
        float y    = position.y + lineH + sp;
        var   iter = property.Copy();
        var   end  = property.GetEndProperty();
        bool  first = true;

        while (iter.NextVisible(first) && !SerializedProperty.EqualContents(iter, end))
        {
            first   = false;
            float h = EditorGUI.GetPropertyHeight(iter, true);
            EditorGUI.PropertyField(new Rect(position.x, y, position.width, h), iter, true);
            y += h + sp;
        }

        EditorGUI.EndProperty();
    }
}
