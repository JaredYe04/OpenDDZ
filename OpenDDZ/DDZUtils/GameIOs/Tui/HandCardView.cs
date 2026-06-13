using OpenDDZ.DDZUtils.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using Terminal.Gui;

namespace OpenDDZ.DDZUtils.GameIOs.Tui
{
    /// <summary>
    /// 手牌区：单击切换选中，拖动对区间内牌取反选中（一次 drag 仅取反一次，拖回起点取消）。
    /// </summary>
    public class HandCardView : View
    {
        private List<Card> _hand = new List<Card>();
        private readonly HashSet<int> _selected = new HashSet<int>();
        private readonly HashSet<int> _hint = new HashSet<int>();

        private bool _canSelect = true;
        private bool _mouseDown;
        private bool _didDrag;
        private bool _dragVisitedAway;
        private int _dragAnchor = -1;
        private int _dragCurrent = -1;
        private HashSet<int> _dragBaseSelected = new HashSet<int>();
        private int _hoverIndex = -1;
        private int _focusIndex;
        private int _appliedSelectionVersion = -1;

        public event Action SelectionChanged;

        public bool CanSelect
        {
            get => _canSelect;
            set
            {
                _canSelect = value;
                if (!value)
                    ResetMouseState();
            }
        }

        public bool SingleSelectMode { get; set; }

        public HandCardView()
        {
            CanFocus = true;
            Height = CardFaceRenderer.CardTotalHeight;
            ColorScheme = new ColorScheme
            {
                Normal = Application.Driver.MakeAttribute(Color.White, Color.Green)
            };
        }

        public void ClearAll()
        {
            _hand.Clear();
            ClearSelectionVisual();
            _appliedSelectionVersion = -1;
            SetNeedsDisplay();
        }

        public void ClearSelectionVisual()
        {
            _selected.Clear();
            _hint.Clear();
            ResetMouseState();
            SetNeedsDisplay();
        }

        public void UpdateHand(IList<Card> hand)
        {
            var sorted = CardRender.SortHand(hand ?? Enumerable.Empty<Card>());
            if (_hand.SequenceEqual(sorted))
                return;

            _hand = sorted;
            _selected.RemoveWhere(i => i >= _hand.Count);
            _hint.RemoveWhere(i => i >= _hand.Count);
            _focusIndex = Math.Min(_focusIndex, Math.Max(0, _hand.Count - 1));
            SetNeedsDisplay();
        }

        public void ApplySelectionEpoch(int version, ISet<int> selected, ISet<int> hint)
        {
            if (_mouseDown || version == _appliedSelectionVersion)
                return;

            ApplySelectionFromState(version, selected, hint);
        }

        public void ApplySelectionFromState(int version, ISet<int> selected, ISet<int> hint)
        {
            _appliedSelectionVersion = version;
            _selected.Clear();
            _hint.Clear();
            if (selected != null)
                foreach (var i in selected)
                    if (i >= 0 && i < _hand.Count) _selected.Add(i);
            if (hint != null)
                foreach (var i in hint)
                    if (i >= 0 && i < _hand.Count) _hint.Add(i);
            SetNeedsDisplay();
        }

        public void MarkSelectionApplied(int version) => _appliedSelectionVersion = version;

        public int GetAppliedSelectionVersion() => _appliedSelectionVersion;

        public ISet<int> GetSelectedIndices() => new HashSet<int>(_selected);

        public ISet<int> GetHintIndices() => new HashSet<int>(_hint);

        public IList<Card> GetSelectedCards()
        {
            if (_hint.Count > 0)
                return _hint.OrderBy(i => i).Select(i => _hand[i]).ToList();
            return _selected.OrderBy(i => i).Select(i => _hand[i]).ToList();
        }

        public void ToggleIndex(int index)
        {
            if (index < 0 || index >= _hand.Count) return;
            _hint.Clear();
            if (_selected.Contains(index))
                _selected.Remove(index);
            else
            {
                if (SingleSelectMode)
                    _selected.Clear();
                _selected.Add(index);
            }
            SetNeedsDisplay();
            SyncSelectionChanged();
        }

        public int ComputeRequiredWidth()
        {
            return CardFaceRenderer.ComputeHandWidth(_hand.Count);
        }

        public override bool OnMouseEvent(MouseEvent me) => ProcessMouseCore(me);

        public override void Redraw(Rect bounds)
        {
            var normal = ColorScheme?.Normal ?? Application.Driver.MakeAttribute(Color.White, Color.Green);
            Driver.SetAttribute(normal);
            Clear();

            if (_hand.Count == 0)
            {
                Move(0, 1);
                Driver.AddStr("（无手牌）");
                return;
            }

            int baseY = 1;
            for (int i = 0; i < _hand.Count; i++)
            {
                int x = i * CardFaceRenderer.CardStepX;
                if (x + CardFaceRenderer.CardBodyWidth > bounds.Width) break;

                bool isHint = _hint.Contains(i);
                bool isSelected = !isHint && _selected.Contains(i);
                bool isHover = !_mouseDown && i == _hoverIndex;
                bool isFocused = HasFocus && _canSelect && i == _focusIndex;

                var state = CardFaceRenderer.ResolveState(isSelected, isHint, isHover || isFocused, false, false);
                CardFaceRenderer.DrawCard(this, x, baseY, _hand[i], state);
            }
        }

        private bool ProcessMouseCore(MouseEvent me)
        {
            if (!_canSelect)
                return false;

            CanFocus = true;
            int idx = HitTest(me.X, me.Y);

            bool btn1Press = (me.Flags & MouseFlags.Button1Pressed) != 0;
            bool btn1Release = (me.Flags & MouseFlags.Button1Released) != 0;
            bool btn2Press = (me.Flags & MouseFlags.Button2Pressed) != 0;
            bool btn2Release = (me.Flags & MouseFlags.Button2Released) != 0;
            bool selectPress = btn1Press || btn2Press;
            bool selectRelease = btn1Release || btn2Release;
            bool moving = (me.Flags & MouseFlags.ReportMousePosition) != 0;

            if (moving && !selectPress && !selectRelease && !_mouseDown)
            {
                if (_hoverIndex != idx)
                {
                    _hoverIndex = idx;
                    SetNeedsDisplay();
                }
                return false;
            }

            if (selectPress && selectRelease)
            {
                SetFocus();
                if (idx >= 0)
                    ToggleIndex(idx);
                ResetMouseState();
                return true;
            }

            if (selectPress && !_mouseDown)
            {
                SetFocus();
                BeginDrag(idx);
                if (moving)
                    UpdateDrag(idx);
                return true;
            }

            if (_mouseDown)
            {
                if (selectRelease)
                    CommitDrag(idx);
                else if (moving || selectPress)
                    UpdateDrag(idx);
                return true;
            }

            if (selectRelease && idx >= 0)
            {
                SetFocus();
                ToggleIndex(idx);
                return true;
            }

            return false;
        }

        public override bool ProcessKey(KeyEvent kb)
        {
            if (!_canSelect || _hand.Count == 0) return base.ProcessKey(kb);

            switch (kb.Key)
            {
                case Key.CursorLeft:
                    _focusIndex = Math.Max(0, _focusIndex - 1);
                    SetNeedsDisplay();
                    return true;
                case Key.CursorRight:
                    _focusIndex = Math.Min(_hand.Count - 1, _focusIndex + 1);
                    SetNeedsDisplay();
                    return true;
                case Key.Space:
                    ToggleIndex(_focusIndex);
                    return true;
            }
            return base.ProcessKey(kb);
        }

        private void BeginDrag(int idx)
        {
            _mouseDown = true;
            _didDrag = false;
            _dragVisitedAway = false;
            _dragAnchor = idx >= 0 ? idx : -1;
            _dragCurrent = _dragAnchor;
            _dragBaseSelected = CaptureEffectiveSelection();
            _hint.Clear();
            ApplyDragPreview();
            SetNeedsDisplay();
        }

        private HashSet<int> CaptureEffectiveSelection()
        {
            var set = new HashSet<int>(_selected);
            foreach (var i in _hint)
                set.Add(i);
            return set;
        }

        private void UpdateDrag(int idx)
        {
            if (idx >= 0)
            {
                if (_dragAnchor >= 0 && idx != _dragAnchor)
                {
                    _dragVisitedAway = true;
                    _didDrag = true;
                }
                _dragCurrent = idx;
            }
            ApplyDragPreview();
            SetNeedsDisplay();
        }

        private void CommitDrag(int releaseIdx)
        {
            if (!_mouseDown)
                return;

            if (SingleSelectMode)
            {
                if (releaseIdx >= 0)
                {
                    _selected.Clear();
                    _selected.Add(releaseIdx);
                    SyncSelectionChanged();
                }
                ResetMouseState();
                return;
            }

            if (releaseIdx >= 0)
                _dragCurrent = releaseIdx;

            if (_didDrag)
            {
                if (_dragVisitedAway && _dragCurrent == _dragAnchor)
                    RestoreSelection(_dragBaseSelected);
                else
                    ApplyDragPreview();
            }
            else if (_dragAnchor >= 0)
            {
                RestoreSelection(_dragBaseSelected);
                FlipInSet(_selected, _dragAnchor);
            }

            SyncSelectionChanged();
            ResetMouseState();
        }

        private void ApplyDragPreview()
        {
            RestoreSelection(_dragBaseSelected);
            if (!_didDrag || _dragAnchor < 0 || _dragCurrent < 0)
                return;

            if (_dragVisitedAway && _dragCurrent == _dragAnchor)
                return;

            int lo = Math.Min(_dragAnchor, _dragCurrent);
            int hi = Math.Max(_dragAnchor, _dragCurrent);
            for (int i = lo; i <= hi; i++)
                FlipInSet(_selected, i);
        }

        private void RestoreSelection(HashSet<int> source)
        {
            _selected.Clear();
            foreach (var i in source)
                _selected.Add(i);
        }

        private static void FlipInSet(HashSet<int> set, int index)
        {
            if (set.Contains(index)) set.Remove(index);
            else set.Add(index);
        }

        private void ResetMouseState()
        {
            _mouseDown = false;
            _didDrag = false;
            _dragVisitedAway = false;
            _dragAnchor = -1;
            _dragCurrent = -1;
            _dragBaseSelected.Clear();
            _hoverIndex = -1;
            SetNeedsDisplay();
        }

        private void SyncSelectionChanged()
        {
            SelectionChanged?.Invoke();
        }

        private int HitTest(int x, int y)
        {
            if (x < 0 || y < 0) return -1;

            int maxY = Math.Max(1, Frame.Height - 1);
            for (int i = _hand.Count - 1; i >= 0; i--)
            {
                int cx = i * CardFaceRenderer.CardStepX;
                if (x >= cx && x < cx + CardFaceRenderer.CardStepX)
                {
                    bool lifted = _hint.Contains(i) || _selected.Contains(i);
                    int top = lifted ? 0 : 1;
                    int bottom = top + CardFaceRenderer.CardBodyHeight;
                    if (y >= top && y <= bottom)
                        return i;
                    if (y >= 0 && y <= maxY)
                        return i;
                }
            }
            return -1;
        }
    }
}
