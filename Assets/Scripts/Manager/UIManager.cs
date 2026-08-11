using System;
using System.Collections.Generic;
using Thesis.Patterns;
using Thesis.UI;
using UnityEngine;

namespace Thesis.Managers
{
    public class UIManager : Singleton<UIManager>
    {
        public GameObject cScreen, cPopup, cNotify, cOverlap;

        private Dictionary<string, BaseScreen>  _screens  = new Dictionary<string, BaseScreen>();
        private Dictionary<string, BasePopup>   _popups   = new Dictionary<string, BasePopup>();
        private Dictionary<string, BaseNotify>  _notifies = new Dictionary<string, BaseNotify>();
        private Dictionary<string, BaseOverlap> _overlaps = new Dictionary<string, BaseOverlap>();

        public IReadOnlyDictionary<string, BaseScreen>  Screens  => _screens;
        public IReadOnlyDictionary<string, BasePopup>   Popups   => _popups;
        public IReadOnlyDictionary<string, BaseNotify>  Notifies => _notifies;
        public IReadOnlyDictionary<string, BaseOverlap> Overlaps => _overlaps;

        private BaseScreen  _curScreen;
        private BasePopup   _curPopup;
        private BaseNotify  _curNotify;
        private BaseOverlap _curOverlap;

        public BaseScreen  CurScreen  => _curScreen;
        public BasePopup   CurPopup   => _curPopup;
        public BaseNotify  CurNotify  => _curNotify;
        public BaseOverlap CurOverlap => _curOverlap;

        private const string SCREEN_PATH  = "Prefabs/UI/Screen/";
        private const string POPUP_PATH   = "Prefabs/UI/Popup/";
        private const string NOTIFY_PATH  = "Prefabs/UI/Notify/";
        private const string OVERLAP_PATH = "Prefabs/UI/Overlap/";

        #region Screen

        public void ShowScreen<T>(object data = null, bool forceShow = false) where T : BaseScreen
        {
            string name = typeof(T).Name;
            BaseScreen result = null;

            if (_curScreen != null)
            {
                var curName = _curScreen.GetType().Name;
                if (curName.Equals(name))
                    result = _curScreen;
                else
                    RemoveScreen(curName);  // animate hide → then destroy
            }

            if (result == null)
            {
                if (!_screens.ContainsKey(name))
                {
                    var scr = GetNewScreen<T>();
                    if (scr != null) _screens.Add(name, scr);
                }
                if (_screens.ContainsKey(name)) result = _screens[name];
            }

            if (result != null && (forceShow || result.IsHide))
            {
                _curScreen = result;
                result.transform.SetAsLastSibling();
                result.Show(data);
            }
        }

        public void HideAllScreens()
        {
            foreach (var item in _screens)
                if (!item.Value.IsHide) item.Value.Hide();
        }

        public T GetExistScreen<T>() where T : BaseScreen
        {
            string name = typeof(T).Name;
            return _screens.ContainsKey(name) ? _screens[name] as T : null;
        }

        private BaseScreen GetNewScreen<T>() where T : BaseScreen
        {
            string name = typeof(T).Name;
            var prefab = GetUIPrefab(UIType.Screen, name);
            if (prefab == null || !prefab.GetComponent<BaseScreen>())
                throw new MissingReferenceException($"Cannot find screen prefab: {name}");
            var ob = Instantiate(prefab, cScreen.transform);
            ob.transform.localScale    = Vector3.one;
            ob.transform.localPosition = Vector3.zero;
#if UNITY_EDITOR
            ob.name = "SCREEN_" + name;
#endif
            var scr = ob.GetComponent<BaseScreen>();
            scr.Init();
            return scr;
        }

        private void RemoveScreen(string name)
        {
            if (!_screens.ContainsKey(name)) return;
            var screen = _screens[name];
            _screens.Remove(name);
            screen.Hide(() =>
            {
                if (screen != null) Destroy(screen.gameObject);
                Resources.UnloadUnusedAssets();
                GC.Collect();
            });
        }

        #endregion

        #region Popup

        public void ShowPopup<T>(object data = null, bool forceShow = false) where T : BasePopup
        {
            string name = typeof(T).Name;
            BasePopup result = null;

            if (_curPopup != null)
            {
                var curName = _curPopup.GetType().Name;
                if (curName.Equals(name))
                    result = _curPopup;
                else
                    RemovePopup(curName);
            }

            if (result == null)
            {
                if (!_popups.ContainsKey(name))
                {
                    var scr = GetNewPopup<T>();
                    if (scr != null) _popups.Add(name, scr);
                }
                if (_popups.ContainsKey(name)) result = _popups[name];
            }

            if (result != null && (forceShow || result.IsHide))
            {
                _curPopup = result;
                result.transform.SetAsLastSibling();
                result.Show(data);
            }
        }

        public void HideAllPopups()
        {
            foreach (var item in _popups)
                if (!item.Value.IsHide) item.Value.Hide();
        }

        public T GetExistPopup<T>() where T : BasePopup
        {
            string name = typeof(T).Name;
            return _popups.ContainsKey(name) ? _popups[name] as T : null;
        }

        private BasePopup GetNewPopup<T>() where T : BasePopup
        {
            string name = typeof(T).Name;
            var prefab = GetUIPrefab(UIType.Popup, name);
            if (prefab == null || !prefab.GetComponent<BasePopup>())
                throw new MissingReferenceException($"Cannot find popup prefab: {name}");
            var ob = Instantiate(prefab, cPopup.transform);
            ob.transform.localScale    = Vector3.one;
            ob.transform.localPosition = Vector3.zero;
#if UNITY_EDITOR
            ob.name = "POPUP_" + name;
#endif
            var scr = ob.GetComponent<BasePopup>();
            scr.Init();
            return scr;
        }

        private void RemovePopup(string name)
        {
            if (!_popups.ContainsKey(name)) return;
            var popup = _popups[name];
            _popups.Remove(name);
            popup.Hide(() =>
            {
                if (popup != null) Destroy(popup.gameObject);
                Resources.UnloadUnusedAssets();
                GC.Collect();
            });
        }

        #endregion

        #region Notify

        public void ShowNotify<T>(object data = null, bool forceShow = false) where T : BaseNotify
        {
            string name = typeof(T).Name;
            BaseNotify result = null;

            if (_curNotify != null)
            {
                var curName = _curNotify.GetType().Name;
                if (curName.Equals(name))
                    result = _curNotify;
                else
                    RemoveNotify(curName);
            }

            if (result == null)
            {
                if (!_notifies.ContainsKey(name))
                {
                    var scr = GetNewNotify<T>();
                    if (scr != null) _notifies.Add(name, scr);
                }
                if (_notifies.ContainsKey(name)) result = _notifies[name];
            }

            if (result != null && (forceShow || result.IsHide))
            {
                _curNotify = result;
                result.transform.SetAsLastSibling();
                result.Show(data);
            }
        }

        public void HideAllNotifies()
        {
            foreach (var item in _notifies)
                if (!item.Value.IsHide) item.Value.Hide();
        }

        public T GetExistNotify<T>() where T : BaseNotify
        {
            string name = typeof(T).Name;
            return _notifies.ContainsKey(name) ? _notifies[name] as T : null;
        }

        private BaseNotify GetNewNotify<T>() where T : BaseNotify
        {
            string name = typeof(T).Name;
            var prefab = GetUIPrefab(UIType.Notify, name);
            if (prefab == null || !prefab.GetComponent<BaseNotify>())
                throw new MissingReferenceException($"Cannot find notify prefab: {name}");
            var ob = Instantiate(prefab, cNotify.transform);
            ob.transform.localScale    = Vector3.one;
            ob.transform.localPosition = Vector3.zero;
#if UNITY_EDITOR
            ob.name = "NOTIFY_" + name;
#endif
            var scr = ob.GetComponent<BaseNotify>();
            scr.Init();
            return scr;
        }

        private void RemoveNotify(string name)
        {
            if (!_notifies.ContainsKey(name)) return;
            var notify = _notifies[name];
            _notifies.Remove(name);
            notify.Hide(() =>
            {
                if (notify != null) Destroy(notify.gameObject);
                Resources.UnloadUnusedAssets();
                GC.Collect();
            });
        }

        #endregion

        #region Overlap

        public void ShowOverlap<T>(object data = null, bool forceShow = false) where T : BaseOverlap
        {
            string name = typeof(T).Name;
            BaseOverlap result = null;

            if (_curOverlap != null)
            {
                var curName = _curOverlap.GetType().Name;
                if (curName.Equals(name))
                    result = _curOverlap;
                else
                    RemoveOverlap(curName);
            }

            if (result == null)
            {
                if (!_overlaps.ContainsKey(name))
                {
                    var scr = GetNewOverlap<T>();
                    if (scr != null) _overlaps.Add(name, scr);
                }
                if (_overlaps.ContainsKey(name)) result = _overlaps[name];
            }

            if (result != null && (forceShow || result.IsHide))
            {
                _curOverlap = result;
                result.transform.SetAsLastSibling();
                result.Show(data);
            }
        }

        public void HideAllOverlaps()
        {
            foreach (var item in _overlaps)
                if (!item.Value.IsHide) item.Value.Hide();
        }

        public T GetExistOverlap<T>() where T : BaseOverlap
        {
            string name = typeof(T).Name;
            return _overlaps.ContainsKey(name) ? _overlaps[name] as T : null;
        }

        private BaseOverlap GetNewOverlap<T>() where T : BaseOverlap
        {
            string name = typeof(T).Name;
            var prefab = GetUIPrefab(UIType.Overlap, name);
            if (prefab == null || !prefab.GetComponent<BaseOverlap>())
                throw new MissingReferenceException($"Cannot find overlap prefab: {name}");
            var ob = Instantiate(prefab, cOverlap.transform);
            ob.transform.localScale    = Vector3.one;
            ob.transform.localPosition = Vector3.zero;
#if UNITY_EDITOR
            ob.name = "OVERLAP_" + name;
#endif
            var scr = ob.GetComponent<BaseOverlap>();
            scr.Init();
            return scr;
        }

        private void RemoveOverlap(string name)
        {
            if (!_overlaps.ContainsKey(name)) return;
            var overlap = _overlaps[name];
            _overlaps.Remove(name);
            overlap.Hide(() =>
            {
                if (overlap != null) Destroy(overlap.gameObject);
                Resources.UnloadUnusedAssets();
                GC.Collect();
            });
        }

        #endregion

        private GameObject GetUIPrefab(UIType type, string uiName)
        {
            string path = type switch
            {
                UIType.Screen  => SCREEN_PATH  + uiName,
                UIType.Popup   => POPUP_PATH   + uiName,
                UIType.Notify  => NOTIFY_PATH  + uiName,
                UIType.Overlap => OVERLAP_PATH + uiName,
                _              => ""
            };
            return Resources.Load<GameObject>(path);
        }
    }
}
