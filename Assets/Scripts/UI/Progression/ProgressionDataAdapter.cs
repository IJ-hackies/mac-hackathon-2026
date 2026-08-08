using System;
using System.Reflection;
using UnityEngine;

namespace Player.UI.Progression
{
    /// <summary>
    /// Adapter kept on UI roots. It supports the UI contract above and the runtime's progression
    /// component by convention, without making the gameplay assembly depend on UI.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProgressionDataAdapter : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour source;
        [SerializeField, Min(0.02f)] private float refreshInterval = 0.12f;

        private float _nextRefresh;
        private object _resolved;
        private IProgressionUiSource _contractSource;
        private Type _sourceType;

        public event Action Refreshed;
        public bool HasSource => Resolve() != null;
        public int Gold => ReadInt("Gold");
        public int MaxLevel => Mathf.Max(1, ReadInt("MaxLevel", 10));

        public void Bind(MonoBehaviour target)
        {
            source = target;
            _resolved = null;
            _contractSource = null;
            _sourceType = null;
            RefreshNow();
        }

        private void OnEnable() => RefreshNow();

        private void Update()
        {
            if (Time.unscaledTime < _nextRefresh) return;
            RefreshNow();
        }

        public void RefreshNow()
        {
            Resolve();
            _nextRefresh = Time.unscaledTime + refreshInterval;
            Refreshed?.Invoke();
        }

        public int GetLevel(ProgressionStat stat)
        {
            if (_contractSource != null) return _contractSource.GetLevel(stat);
            return ReadIntMethod("GetLevel", ToRuntimeName(stat), 1);
        }

        public float GetPurchasedValue(ProgressionStat stat)
        {
            if (_contractSource != null) return _contractSource.GetPurchasedValue(stat);
            return ReadFloatMethod("GetPurchasedValue", ToRuntimeName(stat), 0f);
        }

        public bool CanUpgrade(ProgressionStat stat)
        {
            if (_contractSource != null) return _contractSource.CanUpgrade(stat);
            return ReadBoolMethod("CanUpgrade", ToRuntimeName(stat));
        }

        public bool TryUpgrade(ProgressionStat stat)
        {
            if (_contractSource != null) return _contractSource.TryUpgrade(stat);
            return InvokePurchase("TryUpgrade", ToRuntimeName(stat));
        }

        public bool CanPurchaseSupply(ProgressionSupply supply)
        {
            if (_contractSource != null) return _contractSource.CanPurchaseSupply(supply);
            return ReadBoolMethod("CanPurchaseSupply", supply.ToString());
        }

        public bool TryPurchaseSupply(ProgressionSupply supply)
        {
            if (_contractSource != null) return _contractSource.TryPurchaseSupply(supply);
            return InvokePurchase("TryPurchaseSupply", supply.ToString());
        }

        public bool OwnsSpecial(ProgressionSpecialSkill skill)
        {
            if (_contractSource != null) return _contractSource.OwnsSpecial(skill);
            return ReadBoolMethod("OwnsSpecial", skill.ToString());
        }

        public bool CanPurchaseSpecial(ProgressionSpecialSkill skill)
        {
            if (_contractSource != null) return _contractSource.CanPurchaseSpecial(skill);
            return ReadBoolMethod("CanPurchaseSpecial", skill.ToString());
        }

        public bool TryPurchaseSpecial(ProgressionSpecialSkill skill)
        {
            if (_contractSource != null) return _contractSource.TryPurchaseSpecial(skill);
            return InvokePurchase("TryPurchaseSpecial", skill.ToString());
        }

        private object Resolve()
        {
            if (_resolved != null) return _resolved;
            if (source == null)
            {
                MonoBehaviour[] candidates = FindObjectsByType<MonoBehaviour>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (MonoBehaviour candidate in candidates)
                {
                    if (candidate == null) continue;
                    if (candidate is IProgressionUiSource ||
                        candidate.GetType().FullName == "Player.UI.Progression.PlayerProgression")
                    {
                        source = candidate;
                        break;
                    }
                }
            }

            _resolved = source;
            _contractSource = source as IProgressionUiSource;
            _sourceType = source != null ? source.GetType() : null;
            return _resolved;
        }

        private int ReadInt(string propertyName, int fallback = 0)
        {
            object value = ReadProperty(propertyName);
            return value == null ? fallback : Convert.ToInt32(value);
        }

        private object ReadProperty(string propertyName)
        {
            Resolve();
            PropertyInfo property = _sourceType?.GetProperty(propertyName,
                BindingFlags.Instance | BindingFlags.Public);
            FieldInfo field = property == null ? _sourceType?.GetField(propertyName,
                BindingFlags.Instance | BindingFlags.Public) : null;
            return property != null ? property.GetValue(_resolved) : field?.GetValue(_resolved);
        }

        private bool ReadBoolMethod(string name, string runtimeArgument)
        {
            object value = InvokeWithEnum(name, runtimeArgument);
            return value is bool result && result;
        }

        private int ReadIntMethod(string name, string runtimeArgument, int fallback)
        {
            object value = InvokeWithEnum(name, runtimeArgument);
            return value == null ? fallback : Convert.ToInt32(value);
        }

        private float ReadFloatMethod(string name, string runtimeArgument, float fallback)
        {
            object value = InvokeWithEnum(name, runtimeArgument);
            return value == null ? fallback : Convert.ToSingle(value);
        }

        private bool InvokePurchase(string name, string runtimeArgument)
        {
            object result = InvokeWithEnum(name, runtimeArgument);
            // Runtime PurchaseResult intentionally stays a gameplay type. "Success" is the
            // only result the presentation needs to distinguish from all disabled/error states.
            return result != null && string.Equals(result.ToString(), "Success",
                StringComparison.OrdinalIgnoreCase);
        }

        private object InvokeWithEnum(string name, string runtimeArgument)
        {
            Resolve();
            if (_sourceType == null) return null;
            foreach (MethodInfo method in _sourceType.GetMethods(BindingFlags.Instance | BindingFlags.Public))
            {
                if (method.Name != name) continue;
                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length != 1 || !parameters[0].ParameterType.IsEnum) continue;
                try
                {
                    object argument = Enum.Parse(parameters[0].ParameterType, runtimeArgument, true);
                    return method.Invoke(_resolved, new[] { argument });
                }
                catch (ArgumentException)
                {
                    return null;
                }
            }
            return null;
        }

        private static string ToRuntimeName(ProgressionStat stat)
        {
            return stat.ToString();
        }
    }
}
