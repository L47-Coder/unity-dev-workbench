using System;

namespace DevWorkbench
{
    [Serializable]
    public struct ComponentRef
    {
        public string TypeKey;

        public bool IsValid => !string.IsNullOrWhiteSpace(TypeKey);

        public static implicit operator string(ComponentRef r) => r.TypeKey;
        public static implicit operator ComponentRef(string s) => new() { TypeKey = s };

        public override string ToString() => TypeKey ?? string.Empty;
    }
}
