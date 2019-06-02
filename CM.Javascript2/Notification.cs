using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Retyped;
using SichboUI;
using CM.JS.Controls;

namespace CM.JS {
    internal class Notification : Element {
        public string Key;
        private static List<Notification> m_Queue = new List<Notification>();
        private Action m_OnCancel;
        private Action m_OnOK;
        public Notification(string title, string msg, string OKText = null, Action onOK = null, Action onCancel = null) {
            Key = title + msg;
            Class = "notification";
            VerticalAlignment = Alignment.Top;
            MaxWidth.Value = 600;

            m_OnOK = onOK;
            m_OnCancel = onCancel;
            var inner = new StackPanel();
            inner.HorizontalAlignment = Alignment.Stretch;
            inner.Margin.Value = new Thickness(20);
            inner.Orientation = Orientation.Vertical;
            var div = inner.Div();
            div.Html.Heading(5, text: title);
            div.Html.Div(text: msg);

            var buttons = new StackPanel();
            buttons.Orientation = Orientation.Horizontal;
            buttons.HorizontalAlignment = Alignment.Right;
            buttons.Margin.Value = new Thickness(10, 0);
            var ok = new Button(ButtonStyle.BlackFill, OKText ?? SR.LABEL_STATUS_OK, OnOK);
            buttons.Add(ok);
            if (onCancel != null) {
                var cancel = new Button(ButtonStyle.BlackOutline, SR.LABEL_CANCEL, OnCancel);
                cancel.Margin.Value = new Thickness(0, 0, 0, 20);
                buttons.Add(cancel);
            }
            inner.Add(buttons);
            this.Add(inner);
            this.RelativeTransform.OnState(ElementState.Adding,
                new SichboUI.RelativeTransform(2, 2, 0, 0, 0, 0, -1),
                SichboUI.RelativeTransform.Empty, Times.Normal, Easing.ElasticOut1);
            this.RelativeTransform.OnState(ElementState.Removing,
               SichboUI.RelativeTransform.Empty,
               new SichboUI.RelativeTransform(1, 1, 0, 0, 0, 0, -1),
               Times.Normal, Easing.CubicIn);
            this.Opacity.OnState(ElementState.Adding, 0, 1, Times.Normal, Easing.Linear);
            this.Opacity.OnState(ElementState.Removing, 1, 0, Times.Normal, Easing.Linear);
        }

        public static void Show(string title, string msg) {
            var key = title + msg;
            for (int i = 0; i < m_Queue.Count; i++) {
                if (m_Queue[i].Key == key)
                    return; // already there.
            }
            bool show = m_Queue.Count == 0;
            m_Queue.Add(new Notification(title, msg));
            if (show)
                ShowNext();
        }
        public static void Prompt(string title, string msg, string okText, Action onOK, Action onCancel) {
            var el = new Notification(title, msg, okText, onOK, onCancel);
            dom.document.body.appendChild(el.Html);
            el.Play();
            el.BringToFront();
        }

        private static void ShowNext() {
            if (m_Queue.Count > 0) {
                var next = m_Queue[0];

                dom.document.body.appendChild(next.Html);
                next.Play();
                next.BringToFront();
            }
        }

        private void OnCancel() {
            this.Remove();
            m_Queue.Remove(this);
            ShowNext();
            m_OnCancel?.Invoke();
        }

        private void OnOK() {
            this.Remove();
            m_Queue.Remove(this);
            ShowNext();
            m_OnOK?.Invoke();
        }
    }
}
