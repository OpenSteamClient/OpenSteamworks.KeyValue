using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using OpenSteamworks.KeyValue.Deserializers;

namespace OpenSteamworks.KeyValue.ObjectGraph;

public class KVObject : IEquatable<KVObject>, ICloneable {
    private string name;

    public string Name {
        get => string.Intern(name);
        [MemberNotNull(nameof(name))] set => name = string.Intern(value);
    }

    public object Value { get; internal set; }
    public bool HasChildren => Value is List<KVObject>;
    public List<KVObject> Children {
        get {
            ThrowIfNotList();
            return (Value as List<KVObject>)!;
        }
    }

    public void SetChild(KVObject kv) {
        ThrowIfNotList();
        
        var existingObj = GetChild(kv.Name);
        if (existingObj != null) {
            existingObj.Value = kv.Value;
            return;
        }

        Children.Add(kv);
    }

    public bool RemoveChild(string key) {
        ThrowIfNotList();
        return this.Children.RemoveAll((obj) => obj.Name == key) > 0;
    }

    public KVObject? GetChild(string key) {
        ThrowIfNotList();
        
        foreach (var item in Children)
        {
            if (item.Name == key) {
                return item;
            }
        }

        return null;
    }

    /// <summary>
    /// Access data.
    /// When getting, the child will be created as a list if it doesn't exist or if the key exists, the existing child will be returned
    /// </summary>
    public KVObject this[string key] {
        get {
            ThrowIfNotList();

            if (TryGetChild(key, out KVObject? child)) {
                return child;
            }

            child = new KVObject(key, new List<KVObject>());
            this.SetChild(child);
            return child;
        }

        set {
            ThrowIfNotList();
            SetChild(value);
        }
    }

    public KVChildrenDictionary GetChildrenAsTrackingDictionary() {
        return new KVChildrenDictionary(this);
    }
    
    private void ThrowIfNotList() {
        if (Value is not List<KVObject>) {
            throw new InvalidOperationException("This operation is invalid on KVObjects where Value is not List<KVObject>");
        }
    }

    private KVObject(string name, object val)
    {
        this.Name = name;
        this.Value = val;
    }

    internal static KVObject Create(string name, object val)
    {
        return new(name, val);
    }

    public KVObject(string name, List<KVObject> children) {
        this.Name = name;
        this.Value = children;
    }

    public KVObject(string name, KVObject value) {
        this.Name = name;
        this.Value = new List<KVObject>() { value };
    }

    public KVObject(string name, string value) {
        this.Name = name;
        this.Value = value;
    }

    public KVObject(string name, bool value) {
        this.Name = name;
        this.Value = Convert.ToInt32(value);
    }

    public KVObject(string name, int value) {
        this.Name = name;
        this.Value = value;
    }

    public KVObject(string name, uint value) {
        this.Name = name;
        this.Value = value.ToString();
    }

    public KVObject(string name, float value) {
        this.Name = name;
        this.Value = value;
    }

    public KVObject(string name, ulong value) {
        this.Name = name;
        this.Value = value;
    }

    public KVObject(string name, long value) {
        this.Name = name;
        this.Value = value;
    }

    private T GetValueAs<T>() where T: IParsable<T> {
        if (Value is string str) {
            return T.Parse(str, null);
        } else if (Value is T t) {
            return t;
        }

        throw new InvalidOperationException("Could not get value of type " + typeof(T).Name + "; type of Value is " + Value.GetType().Name);
    }

    private void SetValue<T>(T val, bool allowChangeType) where T: IFormattable {
        if (Value is string) {
            var asStr = val.ToString();
            if (asStr == null) {
                throw new NullReferenceException("IFormattable object returned null.");
            }

            Value = asStr;
        } else if (Value is T) {
            Value = val;
        }

        if (allowChangeType) {
            Value = val;
            return;
        }

        throw new InvalidOperationException("Attempting to change type of value from " + Value?.GetType().Name + " to " + typeof(T).Name);
    }

    public bool GetValueAsBool() => Convert.ToBoolean(GetValueAs<int>());
    public int GetValueAsInt() => GetValueAs<int>();
    public uint GetValueAsUInt() {
        if (Value is string str) {
            return uint.Parse(str);
        } else if (Value is int i) {
            unchecked
            {
                return (uint)i;
            }
        }

        throw new InvalidOperationException("Could not get value of type uint");
    }

    public float GetValueAsFloat() => GetValueAs<float>();
    public ulong GetValueAsULong() => GetValueAs<ulong>();
    public long GetValueAsLong() => GetValueAs<long>();
    
    public void SetValue(bool val, bool allowChangeType = false) => SetValue<int>(Convert.ToInt32(val), allowChangeType);
    public void SetValue(int val, bool allowChangeType = false) => SetValue<int>(val, allowChangeType);
    public void SetValue(uint val, bool allowChangeType = false) {
        if (Value is string) {
            Value = val.ToString();
        } else if (Value is int) {
            unchecked
            {
                Value = (int)val;
            }
        }

        if (allowChangeType) {
            Value = val;
            return;
        }

        throw new InvalidOperationException("Attempting to change type of value from " + Value?.GetType().Name + " to uint");
    }

    public void SetValue(float val, bool allowChangeType = false) => SetValue<float>(val, allowChangeType);
    public void SetValue(ulong val, bool allowChangeType = false) => SetValue<ulong>(val, allowChangeType);
    public void SetValue(long val, bool allowChangeType = false) => SetValue<long>(val, allowChangeType);
    public void SetValue(string val, bool allowChangeType = false) {
        if (Value is string) {
            Value = val;
            return;
        }

        if (allowChangeType) {
            Value = val;
            return;
        }

        throw new InvalidOperationException("Attempting to change type of value from " + Value?.GetType().Name + " to string");
    }

    public string GetValueAsString() {
        if (Value is string str) {
            return str;
        } else if (Value is IFormattable c) {
            string? asStr = c.ToString();
            if (asStr == null) {
                throw new NullReferenceException("IFormattable object returned null.");
            }

            return asStr;
        }

        throw new InvalidOperationException("Could not get value of type string");
    }

    /// <summary>
    /// Tries to get the value as a string.
    /// If the value is not a string, it is attempted to be formatted to a string.
    /// If it cannot be formatted to a string, null is returned.
    /// </summary>
    /// <returns></returns>
    public string? TryGetValueAsString() {
        if (Value is string str) {
            return str;
        } else if (Value is IFormattable c) {
            return c.ToString();
        } else {
            return null;
        }
    }

    public bool HasChild(string key) {
        return GetChild(key) != null;
    }

    public bool TryGetChild(string key, [NotNullWhen(true)] out KVObject? obj)
    {
        var child = GetChild(key);
        if (child == null) {
            obj = null;
            return false;
        }

        obj = child;
        return true;
    }

    public override bool Equals(object? obj)
    {
        return Equals((KVObject?)obj, true);
    }

    public bool Equals(KVObject? other, bool valueTypeEqual = false)
    {
        if (object.ReferenceEquals(this, other)) {
            return true;
        }

        if (other is null) {
            return false;
        }

        if (other.HasChildren != this.HasChildren) {
            return false;
        }

        if (this.HasChildren && other.HasChildren) {
            if (this.Children.Count != other.Children.Count) {
                return false;
            }

            foreach (var item in this.Children)
            {
                if (!item.Equals(other.GetChild(item.Name), valueTypeEqual)) {
                    return false;
                }
            }
        } else {
            if (valueTypeEqual && this.Value.GetType() != other.Value.GetType()) {
                return false;
            }

            if (this.GetValueAsString() != other.GetValueAsString()) {
                return false;
            }
        }
        
        return true;
    }

    /// <summary>
    /// Deep clone this KVObject
    /// </summary>
    public KVObject Clone()
    {
        if (HasChildren) {
            var cloned = new KVObject(Name, new List<KVObject>());
            foreach (var item in Children)
            {
                cloned.Children.Add(item.Clone());
            }

            return cloned;
        } else {
            return new KVObject(Name, Value);
        }
    }

    public override int GetHashCode()
    {
        unchecked {
            int hash = Name.GetHashCode();
            if (HasChildren) {
                foreach (var item in Children)
                {
                    hash *= item.GetHashCode();
                }
            } else {
                hash *= Value.GetHashCode();
            }

            return hash;
        }
    }

    object ICloneable.Clone()
    {
        return this.Clone();
    }

    public bool Equals(KVObject? other)
    {
        return Equals(other, true);
    }

    public void RemoveAllChildren()
    {
        ThrowIfNotList();
        Children.Clear();
    }
}