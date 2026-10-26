using System;
using UnityEngine;

[Serializable]
public class Rule<T>
{
    public T value;
    public string friendlyName;
    public string suffix;

    [HideInInspector] public BaseTextSetter textSetter;

    public Action OnValueChanged;

    public Rule(string friendlyName, T value, string suffix = "")
    {
        this.friendlyName = friendlyName;
        this.suffix = suffix;
        this.value = value;
    }

    public virtual void SetValue(T newValue)
    {
        value = newValue;

        if (textSetter != null)
        {
            textSetter.ChangeValue(newValue);
        }
    }

    public void OnDisplay(bool canControlPanel)
    {
        if (textSetter != null)
        {
            textSetter.OnDisplay(canControlPanel);
        }
    }
}

[Serializable]
public class LimitedRule<T> : Rule<T>
{
    public T min;
    public T max;

    public LimitedRule(string friendlyName, T value, T min, T max, string suffix = "") : base(friendlyName, value, suffix)
    {
        this.min = min;
        this.max = max;
    }
}

[Serializable]
public class ListRule<L> : Rule<int>
{
    [HideInInspector] public L listValue;
    [HideInInspector] public L[] options;

    public ListRule(string friendlyName, L[] options, int value, string suffix = "") : base(friendlyName, value, suffix)
    {
        this.options = options;
    }

    public override void SetValue(int newValue)
    {
        listValue = options[value];

        base.SetValue(newValue);
    }
}

[Serializable]
public class EnumRule<E> : Rule<E>
{
    [HideInInspector] public int enumIndex;
    private Array enumValues;

    public EnumRule(string friendlyName, E value, string suffix = "") : base(friendlyName, value, suffix)
    {
        enumValues = Enum.GetValues(typeof(E));
    }

    public override void SetValue(E newValue)
    {
        if (GetIndexForValue(newValue, out enumIndex))
        {
            base.SetValue(newValue);
        }
    }

    public void SetValue(int index)
    {
        SetValue((E)enumValues.GetValue(index));
    }

    private bool GetIndexForValue(E value, out int index)
    {
        index = 0;
        if (enumValues == null) return false;

        for (int i = 0; i < enumValues.Length; i++)
        {
            if (enumValues.GetValue(i).Equals(value))
            {
                index = i;
                return true;
            }
        }

        return false;
    }
}
