using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
#if Aicomi
using R3;
using R3.Triggers;
#else
using UniRx;
using UniRx.Triggers;
#endif

namespace CoastalSmell
{
    public class ChoiceList
    {
        GameObject View;
        Toggle State;
        TextMeshProUGUI Text;
        public string[] Options { get; init; }
        public ChoiceList(float width, float height, string name, params string[] values) =>
            View = UGUI.ScrollView(width, height * Math.Min(8, values.Length), name, UGUI.RootCanvas.gameObject)
                .With(UGUI.Cmp(UGUI.LayoutGroup<VerticalLayoutGroup>()))
                .With(UGUI.Cmp(UGUI.ToggleGroup(allowSwitchOff: false)))
                .With(UGUI.Cmp(UGUI.Fitter()))
                .With(PopulateList(width, height, Options = values))
                .transform.parent.parent.gameObject
                .With(UGUI.Cmp<LayoutElement>(UnityEngine.Object.Destroy))
                .With(UGUI.Cmp<ObservablePointerExitTrigger>(ObserveCancel))
                .With(UGUI.Cmp(UGUI.Rt(sizeDelta: new(width, height * Math.Min(8, values.Length)))))
                .With(UGUI.Go(active: false));
        Action<GameObject> PopulateList(float width, float height, string[] values) =>
            parent => values.ForEach(value =>
                UGUI.Toggle(width, height, value, parent)
                    .With(UGUI.Cmp<Toggle, ToggleGroup>((ui, group) => ui.group = group))
                    .GetComponent<Toggle>().OnPointerClickAsObservable()
                    .Subscribe(OnComplete(value)));
        void ObserveCancel(ObservablePointerExitTrigger trigger) =>
            trigger.OnPointerExitAsObservable().Subscribe(OnCancel);
        Action<UnityEngine.EventSystems.PointerEventData> OnCancel => _ =>
            (State != null && Text != null).Maybe(Cancel);
        Action<UnityEngine.EventSystems.PointerEventData> OnComplete(string value) =>
            state => (state.button == 0).Maybe(F.Apply(Complete, value));
        void Complete(string value) =>
            CloseChoice
                .With(F.Apply(View.SetActive, false))
                .With(F.Apply(Text.SetText, value, true))
                .With(F.Apply(State.Set, false, true))();
        void Cancel() =>
            CloseChoice
                .With(F.Apply(View.SetActive, false))
                .With(F.Apply(State.Set, false, false))();
        public void Assign(GameObject go) =>
            go.GetComponent<Toggle>().OnValueChangedAsObservable().Subscribe(OnOpenChoce(go));
        Action<bool> OnOpenChoce(GameObject go) =>
            value => value.Maybe(F.Apply(OpenChoice, go));
        void OpenChoice(GameObject go) =>
            Relocate(View.With(UGUI.Go(active: true)).GetComponent<RectTransform>(),
                go.With(UGUI.ModifyAt($"{go.name}.State", $"{go.name}.Label")
                    (UGUI.Cmp<TextMeshProUGUI, Toggle>(Targets))).GetComponent<RectTransform>());
        Action CloseChoice =>
            () => (State, Text) = (null, null);
        void Relocate(RectTransform view, RectTransform item) =>
            view.position = item.position + Relocate(
                view.TransformVector(new Vector3(view.rect.width, view.rect.height, 0)),
                item.TransformVector(new Vector3(item.rect.width, item.rect.height, 0)));
        Vector3 Relocate(Vector3 view, Vector3 item) =>
            new((view.x - item.x) / 2, -(view.y + item.y) / 2, 0);
        void Targets(TextMeshProUGUI text, Toggle toggle) =>
            (State, Text) = (toggle, text.With(Initialize));
        void Initialize(TextMeshProUGUI text) =>
            View.GetComponentsInChildren<Toggle>()
                .Where(toggle => toggle.gameObject.name == text.text).First().Set(true, false);
    }
}