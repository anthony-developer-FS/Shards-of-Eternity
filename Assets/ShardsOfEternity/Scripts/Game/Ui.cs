using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace ShardsOfEternity.Game
{
    /// <summary>Small helpers for building UI Toolkit trees in code. Styling lives in Resources/UI/Shards.uss.</summary>
    public static class Ui
    {
        public static T With<T>(this T e, params string[] classes) where T : VisualElement
        {
            foreach (var c in classes) e.AddToClassList(c);
            return e;
        }

        public static T Into<T>(this T e, VisualElement parent) where T : VisualElement
        {
            parent.Add(e);
            return e;
        }

        public static VisualElement Box(params string[] classes) => new VisualElement().With(classes);

        public static VisualElement Row(params string[] classes) => Box(classes).With("row");

        public static Label Text(string text, params string[] classes) => new Label(text).With(classes);

        /// <summary>A button that never takes keyboard focus, so Enter and Space stay free for the game.</summary>
        public static Button Btn(string text, Action onClick, params string[] classes)
        {
            var b = new Button(onClick) { text = text, focusable = false }.With("btn");
            return b.With(classes);
        }

        /// <summary>A checkbox drawn by the style sheet: a box plus a label, toggled on click.</summary>
        public static Button Check(string text, bool on, Action<bool> changed, bool enabled = true)
        {
            var b = new Button(() => changed(!on)) { focusable = false }.With("btn", "check", "row");
            b.EnableInClassList("check--on", on);
            Box("check-box").Into(b);
            Text(text, "small", "grow").Into(b);
            b.SetEnabled(enabled);
            return b;
        }

        public static void Show(this VisualElement e, bool visible) =>
            e.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

        public static void SetText(this Label l, string text)
        {
            if (l.text != text) l.text = text;
        }

        /// <summary>Sets a bar's fill fraction (0 to 1) and color.</summary>
        public static void SetFill(this VisualElement fill, float fraction, Color color)
        {
            fill.style.width = Length.Percent(Mathf.Clamp01(fraction) * 100f);
            fill.style.backgroundColor = color;
        }

        public static VisualElement Bar(out VisualElement fill, params string[] classes)
        {
            var bar = Box("bar").With(classes);
            fill = Box("bar-fill").Into(bar);
            return bar;
        }

        public static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

        public static string Colored(string text, Color c) => $"<color={Hex(c)}>{text}</color>";

        public static void Ignore(this VisualElement e) => e.pickingMode = PickingMode.Ignore;
    }
}
